using Casca.PrintAgent.Contracts;
using Casca.PrintAgent.Services;

namespace Casca.PrintAgent.Endpoints;

public static class PrintSaleTicketEndpoint
{
    public static IEndpointRouteBuilder MapPrintSaleTicket(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/tickets/sale",
            async (PrintSaleTicketRequest request, IPrinterService printerService, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.PrinterName))
                    return Results.BadRequest(new { message = "El nombre de la impresora es requerido." });

                if (request.Items is null || request.Items.Count == 0)
                    return Results.BadRequest(new { message = "El ticket debe contener al menos una partida." });

                try
                {
                    var success = await printerService.PrintSaleTicketAsync(request, ct);

                    return success
                        ? Results.Ok(new { message = $"Ticket '{request.SaleNumber}' enviado a '{request.PrinterName}'." })
                        : Results.NotFound(new { message = $"Impresora '{request.PrinterName}' no encontrada." });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Impresora apagada, sin papel o con el driver en error.
                    return Results.Json(
                        new { message = $"No se pudo imprimir en '{request.PrinterName}': {ex.Message}" },
                        statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .WithName("PrintSaleTicket")
            .WithSummary("Imprime el ticket de una venta en la impresora indicada.");

        return app;
    }
}
