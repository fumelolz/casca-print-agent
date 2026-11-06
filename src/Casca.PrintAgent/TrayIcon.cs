using System.Drawing;
using System.Windows.Forms;

namespace Casca.PrintAgent;

/// <summary>Ícono de la bandeja del sistema: indica que el agente está activo y permite cerrarlo.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;

    public TrayIcon(string url, Action exit)
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add(new ToolStripMenuItem($"Casca Print Agent ({url})") { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Salir", null, (_, _) => exit());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Casca Print Agent: en ejecución",
            ContextMenuStrip = _menu,
            Visible = true
        };
    }

    private static Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } path &&
                Icon.ExtractAssociatedIcon(path) is { } icon)
                return icon;
        }
        catch (Exception)
        {
            // Si no se puede leer el ícono del ejecutable se usa el genérico.
        }

        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
