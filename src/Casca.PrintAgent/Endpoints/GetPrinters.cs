using Casca.PrintAgent.Services;

namespace Casca.PrintAgent.Endpoints;

public static class GetPrinters
{
    public static IEndpointRouteBuilder MapGetPrinters(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/printers",
            (IPrinterService printerService) =>
            {
                var printers = printerService.GetPrinters();

                return Results.Ok(printers);
            });

        return app;
    }
}