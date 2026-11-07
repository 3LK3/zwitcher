using System.Drawing;
using System.Windows.Forms;

namespace Zwitcher.App;

/// <summary>Notification-area icon with About and Exit items. WPF has no tray API, so this wraps NotifyIcon.</summary>
public sealed class TrayIcon : IDisposable
{
    private const string IconResourceName = "Zwitcher.App.Assets.zwitcher.ico";

    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _image;

    public TrayIcon()
    {
        _menu = new ContextMenuStrip();
#if DEBUG
        _menu.Items.Add(
            "Post test commands",
            null,
            (_, _) => PostTestCommandsRequested?.Invoke(this, EventArgs.Empty)
        );
#endif
        _menu.Items.Add("About", null, (_, _) => AboutRequested?.Invoke(this, EventArgs.Empty));
        _menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _image = LoadIcon();
        _icon = new NotifyIcon
        {
            Icon = _image,
            Text = "Zwitcher",
            ContextMenuStrip = _menu,
            Visible = true,
        };
    }

#if DEBUG
    public event EventHandler? PostTestCommandsRequested;
#endif

    public event EventHandler? AboutRequested;

    public event EventHandler? ExitRequested;

    public void Dispose()
    {
        // Hide first, otherwise Windows leaves a ghost icon until the mouse hovers over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        // NotifyIcon does not take ownership of the Icon assigned to it.
        _image.Dispose();
    }

    private static Icon LoadIcon()
    {
        using var stream =
            typeof(TrayIcon).Assembly.GetManifestResourceStream(IconResourceName)
            ?? throw new InvalidOperationException($"Missing resource '{IconResourceName}'.");

        // Requesting the tray size picks the closest frame in the .ico, so the icon stays
        // sharp. Icon.ExtractAssociatedIcon would only give us a downscaled 32 px image.
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
