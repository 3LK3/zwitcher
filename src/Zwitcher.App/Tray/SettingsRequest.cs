using Zwitcher.App.Settings;

namespace Zwitcher.App.Tray;

/// <summary>A request to open the settings flyout at the tray icon (physical pixels).</summary>
/// <param name="FromIconClick">A left-click on the icon, as opposed to a menu item.</param>
public sealed record SettingsRequest(
    SettingsPage Page,
    int AnchorX,
    int AnchorY,
    bool FromIconClick
);
