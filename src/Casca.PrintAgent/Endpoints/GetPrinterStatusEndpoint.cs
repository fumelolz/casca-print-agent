using Casca.PrintAgent.Services;

namespace Casca.PrintAgent.Endpoints;

public static class GetPrinterStatusEndpoint
{
    public static IEndpointRouteBuilder MapGetPrinterStatus(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/printers/{name}/status",
            async (string name, IPrinterService printerService, CancellationToken ct) =>
            {
                var printerName = Uri.UnescapeDataString(name);
                var status = await printerService.GetPrinterStatusAsync(printerName, ct);

                return status.IsInstalled
                    ? Results.Ok(status)
                    : Results.NotFound(status);
            })
            .WithName("GetPrinterStatus")
            .WithSummary("Consulta el estado real de una impresora (WMI + socket TCP para impresoras de red).");

        return app;
    }
}
