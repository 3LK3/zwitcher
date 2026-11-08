using System.Drawing;
using System.Windows.Forms;
using Zwitcher.App.SettingsUi;
using Zwitcher.Core.Screen;

namespace Zwitcher.App.Tray;

/// <summary>Notification-area icon and its menu. WPF has no tray API, so this wraps NotifyIcon.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly Icon _image;
    private readonly ToolStripMenuItem _enabled;
    private readonly ToolStripMenuItem _update;

    private int _addedItems;

    /// <summary>Where the icon was last clicked, in physical pixels. Kept for keyboard use.</summary>
    private Point _anchor = Cursor.Position;

    public TrayIcon()
    {
        _menu = new ContextMenuStrip();
        _update = new ToolStripMenuItem("Update") { Visible = false };
        _update.Click += (_, _) => UpdateRequested?.Invoke(this, EventArgs.Empty);
        _menu.Items.Add(_update);
        _enabled = new ToolStripMenuItem("Replace Alt+Tab") { Checked = true, CheckOnClick = true };
        _enabled.CheckedChanged += (_, _) => EnabledChanged?.Invoke(this, _enabled.Checked);
        _menu.Items.Add(_enabled);
        _menu.Items.Add("Settings", null, (_, _) => RequestSettings(SettingsPage.General, false));
        _menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _image = LoadIcon();
        _icon = new NotifyIcon
        {
            Icon = _image,
            Text = "Zwitcher",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        // The cursor is on the icon when the button goes down. By the time a menu item is
        // clicked it's on the menu, so the anchor is captured here. NotifyIcon doesn't expose
        // its rectangle (Shell_NotifyIconGetRect needs its private window handle).
        _icon.MouseDown += (_, _) => _anchor = Cursor.Position;
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                RequestSettings(SettingsPage.General, true);
            }
        };
        _icon.BalloonTipClicked += (_, _) =>
        {
            if (_update.Visible && _update.Enabled)
            {
                UpdateRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler<bool>? EnabledChanged;

    public event EventHandler<SettingsRequest>? SettingsRequested;

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

    /// <summary>Progress or failure of an update the user asked for.</summary>
    public void SetUpdateState(string text, bool enabled)
    {
        _update.Text = text;
        _update.Enabled = enabled;
    }

    /// <summary>Adds an item above the regular ones, e.g. for debug commands.</summary>
    public void AddMenuItem(string text, Action onClick) =>
        _menu.Items.Insert(_addedItems++, new ToolStripMenuItem(text, null, (_, _) => onClick()));

    public void Dispose()
    {
        // Hide first, otherwise Windows leaves a ghost icon until the mouse hovers over it.
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        // NotifyIcon does not take ownership of the Icon assigned to it.
        _image.Dispose();
    }

    private void RequestSettings(SettingsPage page, bool fromIconClick) =>
        SettingsRequested?.Invoke(
            this,
            new SettingsRequest(page, new PixelPoint(_anchor.X, _anchor.Y), fromIconClick)
        );

    private static Icon LoadIcon()
    {
        using var stream = AppInfo.OpenIconStream();

        // Requesting the tray size picks the closest frame in the .ico, so the icon stays
        // sharp. Icon.ExtractAssociatedIcon would only give us a downscaled 32 px image.
        return new Icon(stream, SystemInformation.SmallIconSize);
    }
}
