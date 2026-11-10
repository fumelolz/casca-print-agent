using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Drawing.Text;
using System.Management;
using System.Net;
using System.Net.Sockets;
using Casca.PrintAgent.Contracts;

namespace Casca.PrintAgent.Services;

public class PrinterService : IPrinterService
{
    public IEnumerable<string> GetPrinters()
    {
        var printers = new List<string>();

        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            printers.Add(printer);
        }

        return printers;
    }

    public Task<bool> PrintTestAsync(string printerName, CancellationToken cancellationToken = default)
    {
        var installed = PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Any(p => string.Equals(p, printerName, StringComparison.OrdinalIgnoreCase));

        if (!installed)
            return Task.FromResult(false);

        return Task.Run(() =>
        {
            using var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = printerName;

            doc.PrintPage += (_, e) =>
            {
                if (e.Graphics is null) return;

                // Configuración para texto nítido en impresora
                e.Graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                e.Graphics.SmoothingMode = SmoothingMode.None;
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.None;

                var font = new Font("Courier New", 10, GraphicsUnit.Point);
                var bold = new Font("Courier New", 12, FontStyle.Bold, GraphicsUnit.Point);
                var brush = Brushes.Black;
                float x = 10f;
                float y = 10f;
                float lineHeight = font.GetHeight(e.Graphics);

                e.Graphics.DrawString("*** PRUEBA DE IMPRESORA ***", bold, brush, x, y);
                y += bold.GetHeight(e.Graphics) + 8;
                e.Graphics.DrawString($"Nombre:  {printerName}", font, brush, x, y);
                y += lineHeight;
                e.Graphics.DrawString($"Fecha:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}", font, brush, x, y);
                y += lineHeight * 2;
                e.Graphics.DrawString("Casca POS - Print Agent", font, brush, x, y);

                font.Dispose();
                bold.Dispose();
            };

            doc.Print();
            return true;
        }, cancellationToken);
    }

    public Task<bool> PrintSaleTicketAsync(PrintSaleTicketRequest ticket, CancellationToken cancellationToken = default)
    {
        var installed = PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Any(p => string.Equals(p, ticket.PrinterName, StringComparison.OrdinalIgnoreCase));

        if (!installed)
            return Task.FromResult(false);

        return Task.Run(() =>
        {
            using var doc = new PrintDocument();
            doc.PrinterSettings.PrinterName = ticket.PrinterName;
            doc.DefaultPageSettings.PaperSize = new PaperSize("80mm Receipt", 280, 2000);
            doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

            doc.PrintPage += (_, e) =>
            {
                if (e.Graphics is null) return;

                SaleTicketRenderer.Render(e.Graphics, ticket);
                e.HasMorePages = false;
            };

            doc.Print();
            return true;
        }, cancellationToken);
    }

    public Task<bool> PrintProductLabelAsync(PrintProductLabelRequest label, CancellationToken cancellationToken = default)
    {
        var installed = PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Any(p => string.Equals(p, label.PrinterName, StringComparison.OrdinalIgnoreCase));

        if (!installed)
            return Task.FromResult(false);

        return Task.Run(() =>
        {
            RawPrinter.Send(label.PrinterName, ProductLabelZplBuilder.Build(label));
            return true;
        }, cancellationToken);
    }

    public async Task<PrinterStatusResponse> GetPrinterStatusAsync(
        string printerName,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Verificar que esté instalada ──────────────────────────────────
        var isInstalled = PrinterSettings.InstalledPrinters
            .Cast<string>()
            .Any(p => string.Equals(p, printerName, StringComparison.OrdinalIgnoreCase));

        if (!isInstalled)
        {
            return new PrinterStatusResponse(
                Name: printerName,
                IsInstalled: false,
                IsOnline: false,
                IsReachable: false,
                StatusLabel: "No instalada",
                WmiStatus: null,
                PortName: null,
                HostAddress: null);
        }

        // ── 2. Consultar WMI para estado real ────────────────────────────────
        int? wmiStatus = null;
        bool isOnline = false;
        string statusLabel = "Desconocido";
        string? portName = null;
        string? hostAddress = null;
        var usedTcpProbe = false;

        try
        {
            var escapedName = printerName.Replace("\\", "\\\\").Replace("'", "\\'");
            using var printerSearcher = new ManagementObjectSearcher(
                $"SELECT PrinterStatus, WorkOffline, PortName, PrinterState, DetectedErrorState FROM Win32_Printer WHERE Name = '{escapedName}'");

            using var printerResults = printerSearcher.Get();

            foreach (ManagementObject obj in printerResults)
            {
                wmiStatus = Convert.ToInt32(obj["PrinterStatus"]);
                var workOffline = obj["WorkOffline"] is bool b && b;
                var printerState = Convert.ToInt64(obj["PrinterState"] ?? 0L);
                var detectedError = Convert.ToInt32(obj["DetectedErrorState"] ?? 0);
                portName = obj["PortName"]?.ToString();

                // Bit 0x80 de PrinterState = offline; DetectedErrorState 9 = offline
                var isOffline = workOffline
                    || wmiStatus == 7
                    || (printerState & 0x80L) != 0
                    || detectedError == 9;

                isOnline = !isOffline;
                statusLabel = ResolveStatusLabel(wmiStatus.Value, isOffline);

                obj.Dispose();
                break;
            }

            // ── 3. Para impresoras de red: verificar socket TCP ──────────────
            if (portName is not null)
            {
                using var portSearcher = new ManagementObjectSearcher(
                    $"SELECT HostAddress, PortNumber FROM Win32_TCPIPPrinterPort WHERE Name = '{portName}'");

                using var portResults = portSearcher.Get();

                foreach (ManagementObject port in portResults)
                {
                    hostAddress = port["HostAddress"]?.ToString();
                    var portNumber = port["PortNumber"] is not null
                        ? Convert.ToInt32(port["PortNumber"])
                        : 9100;

                    port.Dispose();

                    if (!string.IsNullOrWhiteSpace(hostAddress))
                    {
                        usedTcpProbe = true;
                        isOnline = await CheckTcpConnectionAsync(hostAddress, portNumber, cancellationToken);

                        if (!isOnline)
                            statusLabel = "Sin conexión (red)";
                    }

                    break;
                }
            }
        }
        catch (ManagementException)
        {
            statusLabel = "Error al consultar WMI";
        }


        var isReachable = usedTcpProbe
            ? isOnline
            : isInstalled && isOnline;

        var localIp = GetLocalIpAddress();

        hostAddress = $"{localIp}:9001";

        return new PrinterStatusResponse(
            Name: printerName,
            IsInstalled: true,
            IsOnline: isOnline,
            IsReachable: isReachable,
            StatusLabel: statusLabel,
            WmiStatus: wmiStatus,
            PortName: portName,
            HostAddress: hostAddress);
    }

    private static string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());

        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(ip))
            {
                return ip.ToString();
            }
        }

        return "127.0.0.1";
    }

    private static async Task<bool> CheckTcpConnectionAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            await socket.ConnectAsync(host, port, cts.Token);
            return socket.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveStatusLabel(int status, bool isOffline) =>
        isOffline ? "Sin conexión" : status switch
        {
            3 => "Disponible",
            4 => "Imprimiendo",
            5 => "Calentando",
            6 => "Detenida",
            7 => "Sin conexión",
            _ => "Desconocido"
        };
}
