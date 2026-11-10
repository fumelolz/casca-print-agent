namespace Casca.PrintAgent.Contracts;

public record PrintProductLabelRequest(
    string PrinterName,
    string ProductName,
    string Sku,
    string? Barcode,
    decimal Price,
    int Copies = 1,
    int WidthMm = 51,
    int HeightMm = 25,
    bool ShowName = true,
    bool ShowSku = true,
    bool ShowPrice = true,
    bool ShowBarcode = true
);
