using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Casca.PrintAgent.Services;

public static class RawPrinter
{
    public static void Send(string printerName, string data)
    {
        if (!OpenPrinter(printerName, out var handle, IntPtr.Zero))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            var docInfo = new DocInfo { DocName = "Etiqueta Casca", DataType = "RAW" };

            if (!StartDocPrinter(handle, 1, docInfo))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                if (!StartPagePrinter(handle))
                    throw new Win32Exception(Marshal.GetLastWin32Error());

                var bytes = Encoding.UTF8.GetBytes(data);
                var written = WritePrinter(handle, bytes, bytes.Length, out _);
                EndPagePrinter(handle);

                if (!written)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                EndDocPrinter(handle);
            }
        }
        finally
        {
            ClosePrinter(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private sealed class DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string? DocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? DataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool OpenPrinter(string printerName, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr handle, int level, [In] DocInfo docInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr handle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr handle, byte[] buffer, int count, out int written);
}
