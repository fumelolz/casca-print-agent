namespace Casca.PrintAgent.Contracts;

public record PrinterStatusResponse(
    string Name,
    bool IsInstalled,
    bool IsOnline,          // WMI: WorkOffline == false
    bool IsReachable,       // TCP para red; en puertos locales/no TCP sigue IsOnline
    string StatusLabel,     // Descripción legible del estado WMI
    int? WmiStatus,         // Valor crudo de Win32_Printer.PrinterStatus
    string? PortName,       // Puerto configurado (ej. "IP_192.168.1.50", "USB001")
    string? HostAddress     // IP/hostname si existe; si no, usa el nombre del puerto
);
