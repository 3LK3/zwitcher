namespace Zwitcher.Core;

public enum ShowOn
{
    PrimaryMonitor,
    CursorMonitor,
    ForegroundMonitor,
}

/// <summary>A new layout needs a value here and its resources in OverlayWindow.xaml.</summary>
public enum OverlayLayout
{
    List,
    Grid,
}

/// <summary>
/// User settings. Start with Windows isn't here: the registry is the only source of truth for
/// it, see StartupRegistration.
/// </summary>
public sealed record Settings
{
    public bool Enabled { get; init; } = true;

    public ShowOn ShowOn { get; init; } = ShowOn.PrimaryMonitor;

    public OverlayLayout Layout { get; init; } = OverlayLayout.List;
}
