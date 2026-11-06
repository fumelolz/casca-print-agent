namespace Casca.PrintAgent.Contracts;

/// <summary>
/// Datos necesarios para imprimir el ticket de una venta.
/// El frontend envía este objeto después de crear la venta exitosamente.
/// </summary>
public record PrintSaleTicketRequest(
    // ── Impresora ────────────────────────────────────────────────────────────
    string PrinterName,

    // ── Tienda ───────────────────────────────────────────────────────────────
    string StoreName,
    string? StoreAddress,
    string? StorePhone,
    string? StoreRfc,
    string? TenantName,
    string? CashierName,

    // ── Venta ────────────────────────────────────────────────────────────────
    string SaleNumber,
    DateTime CreatedOnUtc,
    string PaymentMethodLabel,
    string? CustomerName,
    string? Notes,

    // ── Partidas ─────────────────────────────────────────────────────────────
    List<TicketItemRequest> Items,

    // ── Totales ──────────────────────────────────────────────────────────────
    decimal Subtotal,
    decimal IvaTotal,
    decimal IepsTotal,
    decimal RoundingAdjustment,
    decimal Total
);

public record TicketItemRequest(
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,   // Precio unitario con IVA (lo que ve el cliente)
    decimal Total        // Total de la partida con IVA e IEPS
);
