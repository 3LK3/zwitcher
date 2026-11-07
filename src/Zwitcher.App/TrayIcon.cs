using System.Drawing;
using System.Windows.Forms;

namespace Zwitcher.App;

/// <summary>Notification-area icon and its menu. WPF has no tray API, so this wraps NotifyIcon.</summary>
public sealed class TrayIcon : IDisposable
{
    private const string IconResourceName = "Zwitcher.App.Assets.zwitcher.ico";

    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _image;
    private readonly ToolStripMenuItem _enabled;
    private readonly ToolStripMenuItem _update;

    public TrayIcon()
    {
        _menu = new ContextMenuStrip();
#if DEBUG
        _menu.Items.Add(
            "Post test commands",
            null,
            (_, _) => PostTestCommandsRequested?.Invoke(this, EventArgs.Empty)
        );
        _menu.Items.Add(
            "Dump windows",
            null,
            (_, _) => DumpWindowsRequested?.Invoke(this, EventArgs.Empty)
        );
#endif
        _update = new ToolStripMenuItem("Update") { Visible = false };
        _update.Click += (_, _) => UpdateRequested?.Invoke(this, EventArgs.Empty);
        _menu.Items.Add(_update);
        _enabled = new ToolStripMenuItem("Enabled") { Checked = true, CheckOnClick = true };
        _enabled.CheckedChanged += (_, _) => EnabledChanged?.Invoke(this, _enabled.Checked);
        _menu.Items.Add(_enabled);
        _menu.Items.Add(
            "Settings",
            null,
            (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty)
        );
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
        _icon.BalloonTipClicked += (_, _) =>
        {
            if (_update.Visible && _update.Enabled)
            {
                UpdateRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

#if DEBUG
    public event EventHandler? PostTestCommandsRequested;

    public event EventHandler? DumpWindowsRequested;
#endif

    public event EventHandler<bool>? EnabledChanged;

    public event EventHandler? SettingsRequested;

    public event EventHandler? AboutRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? UpdateRequested;

    public void SetEnabled(bool enabled) => _enabled.Checked = enabled;

    /// <summary>Shows the update menu item and a one-time balloon the user can click.</summary>
    public void SetUpdateAvailable(string version)
    {
        _update.Text = $"Update to {version}";
        _update.Enabled = true;
        _update.Visible = true;
        _icon.ShowBalloonTip(
            10_000,
            "Zwitcher update available",
            $"Version {version} is ready. Click to install.",
            ToolTipIcon.Info
        );
    }

    public void SetUpdateBusy(string text)
    {
        _update.Text = text;
        _update.Enabled = false;
    }

    public void SetUpdateRetry(string text)
    {
        _update.Text = text;
        _update.Enabled = true;
    }

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
