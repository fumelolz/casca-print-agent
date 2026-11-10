using Casca.PrintAgent.Contracts;

namespace Casca.PrintAgent.Services;

public interface IPrinterService
{
    IEnumerable<string> GetPrinters();
    Task<bool> PrintTestAsync(string printerName, CancellationToken cancellationToken = default);
    Task<bool> PrintSaleTicketAsync(PrintSaleTicketRequest ticket, CancellationToken cancellationToken = default);
    Task<bool> PrintProductLabelAsync(PrintProductLabelRequest label, CancellationToken cancellationToken = default);
    Task<PrinterStatusResponse> GetPrinterStatusAsync(string printerName, CancellationToken cancellationToken = default);
}
