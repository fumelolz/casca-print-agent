using Casca.PrintAgent.Contracts;
using Casca.PrintAgent.Services;

namespace Casca.PrintAgent.Endpoints;

public static class PrintProductLabelEndpoint
{
    public static IEndpointRouteBuilder MapPrintProductLabel(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/labels/product",
            async (PrintProductLabelRequest request, IPrinterService printerService, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.PrinterName))
                    return Results.BadRequest(new { message = "El nombre de la impresora es requerido." });

                if (string.IsNullOrWhiteSpace(request.ProductName))
                    return Results.BadRequest(new { message = "El nombre del producto es requerido." });

                if (!request.ShowName && !request.ShowSku && !request.ShowPrice && !request.ShowBarcode)
                    return Results.BadRequest(new { message = "La etiqueta requiere al menos un dato a mostrar." });

                try
                {
                    var success = await printerService.PrintProductLabelAsync(request, ct);

                    return success
                        ? Results.Ok(new { message = $"Etiqueta de '{request.ProductName}' enviada a '{request.PrinterName}'." })
                        : Results.NotFound(new { message = $"Impresora '{request.PrinterName}' no encontrada." });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return Results.Json(
                        new { message = $"No se pudo imprimir en '{request.PrinterName}': {ex.Message}" },
                        statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .WithName("PrintProductLabel")
            .WithSummary("Imprime la etiqueta de un producto en una impresora ZPL.");

        return app;
    }
}
