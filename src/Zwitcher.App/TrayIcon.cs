using System.Drawing;
using System.Windows.Forms;

namespace Zwitcher.App;

/// <summary>Notification-area icon with an Exit menu. WPF has no tray API, so this wraps NotifyIcon.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;

    public TrayIcon()
    {
        _menu = new ContextMenuStrip();
        _menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _icon = new NotifyIcon
        {
            // Placeholder until we have an app icon.
            Icon = SystemIcons.Application,
            Text = "Zwitcher",
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

    public event EventHandler? ExitRequested;

    public void Dispose()
    {
        // Hide first, otherwise Windows leaves a ghost icon until the mouse hovers over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
