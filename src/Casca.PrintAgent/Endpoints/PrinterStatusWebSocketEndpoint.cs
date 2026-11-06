using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Casca.PrintAgent.Services;

namespace Casca.PrintAgent.Endpoints;

public static class PrinterStatusWebSocketEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Registra el WebSocket como middleware (antes del endpoint routing) para que el
    /// handshake 101 no sea interceptado por el pipeline de Minimal APIs.
    /// Ruta: ws://localhost:9001/printers/{name}/status/ws?interval=5
    /// </summary>
    public static IApplicationBuilder UseWebSocketPrinterStatus(
        this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (context.WebSockets.IsWebSocketRequest &&
                TryExtractPrinterName(context.Request.Path.Value, out var printerName))
            {
                var printerService = context.RequestServices
                    .GetRequiredService<IPrinterService>();

                var intervalSeconds = context.Request.Query.TryGetValue("interval", out var v)
                    && int.TryParse(v, out var parsed)
                    ? Math.Clamp(parsed, 1, 60)
                    : 5;

                await HandleAsync(context, printerService, printerName,
                    TimeSpan.FromSeconds(intervalSeconds));
            }
            else
            {
                await next(context);
            }
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryExtractPrinterName(string? path, out string printerName)
    {
        // Acepta ambas rutas:
        //   /printers/{name}/status       ← lo que usa el frontend
        //   /printers/{name}/status/ws    ← alias explícito
        var segments = path?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        printerName = string.Empty;

        if (segments is null || segments.Length < 3 ||
            !segments[0].Equals("printers", StringComparison.OrdinalIgnoreCase) ||
            !segments[2].Equals("status", StringComparison.OrdinalIgnoreCase))
            return false;

        var validLength = segments.Length == 3 ||
            (segments.Length == 4 &&
             segments[3].Equals("ws", StringComparison.OrdinalIgnoreCase));

        if (!validLength) return false;

        printerName = Uri.UnescapeDataString(segments[1]);
        return true;
    }

    private static async Task HandleAsync(
        HttpContext context,
        IPrinterService printerService,
        string printerName,
        TimeSpan pollInterval)
    {
        var ct = context.RequestAborted;
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var receiveBuffer = new byte[256];

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            try
            {
                var status = await printerService.GetPrinterStatusAsync(printerName, ct);
                var payload = Encoding.UTF8.GetBytes(
                    JsonSerializer.Serialize(status, JsonOptions));

                await ws.SendAsync(
                    new ArraySegment<byte>(payload),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: ct);

                // Esperar el intervalo; si el cliente envía un mensaje (ping/close), actuar
                var receiveTask = ws.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), ct);
                var delayTask = Task.Delay(pollInterval, ct);

                var completed = await Task.WhenAny(receiveTask, delayTask);

                if (completed == receiveTask)
                {
                    var result = await receiveTask;
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Cliente cerró la conexión",
                            CancellationToken.None);
                        return;
                    }
                    // Ping recibido → el próximo ciclo envía estado inmediatamente
                }
            }
            catch (OperationCanceledException) { break; }
            catch (WebSocketException) { break; }
        }

        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await ws.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Conexión cerrada",
                CancellationToken.None);
        }
    }
}
