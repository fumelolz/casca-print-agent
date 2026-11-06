using Casca.PrintAgent.Services;

namespace Casca.PrintAgent.Endpoints;

public static class PrintTestPageEndpoint
{
    public static IEndpointRouteBuilder MapPrintTestPage(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/printers/{name}/test",
            async (string name, IPrinterService printerService, CancellationToken ct) =>
            {
                var printerName = Uri.UnescapeDataString(name);
                try
                {
                    var success = await printerService.PrintTestAsync(printerName, ct);

                    return success
                        ? Results.Ok(new { message = $"Página de prueba enviada a '{printerName}'." })
                        : Results.NotFound(new { message = $"Impresora '{printerName}' no encontrada." });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Results.Json(
                        new { message = $"No se pudo imprimir en '{printerName}': {ex.Message}" },
                        statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .WithName("PrintTestPage")
            .WithSummary("Imprime una página de prueba en la impresora indicada.");

        return app;
    }
}
