using Zwitcher.Core.Screen;

namespace Zwitcher.App.SettingsUi;

/// <summary>A request to open the settings flyout at the tray icon (physical pixels).</summary>
/// <param name="FromIconClick">A left-click on the icon, as opposed to a menu item.</param>
public sealed record SettingsRequest(SettingsPage Page, PixelPoint Anchor, bool FromIconClick);
