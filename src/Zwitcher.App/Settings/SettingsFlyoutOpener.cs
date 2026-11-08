using Zwitcher.App.Tray;
using Zwitcher.Core.Configuration;

namespace Zwitcher.App.Settings;

/// <summary>
/// Opens the settings flyout for a tray request, or brings the open one forward. At most one
/// flyout exists at a time.
/// </summary>
public sealed class SettingsFlyoutOpener(SettingsStore settings, StartupRegistration startup)
{
    private const long ReopenAfterCloseMs = 300;

    private SettingsWindow? _window;
    private long _closedAt = long.MinValue / 2;

    public void Open(SettingsRequest request)
    {
        if (_window is { } open)
        {
            open.ShowPage(request.Page);
            open.Activate();
            return;
        }

        // Clicking the icon while the flyout is open first deactivates the flyout, which closes
        // it, and then delivers the click. Without this, the click would reopen it right away
        // instead of toggling it off, unlike the system flyouts.
        if (request.FromIconClick && Environment.TickCount64 - _closedAt < ReopenAfterCloseMs)
        {
            return;
        }

        var window = new SettingsWindow(settings, startup);
        window.Closed += (_, _) =>
        {
            _window = null;
            _closedAt = Environment.TickCount64;
        };
        _window = window;
        window.ShowPage(request.Page);
        window.ShowAt(request.AnchorX, request.AnchorY);
    }
}
