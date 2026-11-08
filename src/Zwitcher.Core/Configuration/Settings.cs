namespace Zwitcher.Core.Configuration;

/// <summary>
/// User settings. Start with Windows isn't here: the registry is the only source of truth for
/// it, see StartupRegistration.
/// </summary>
public sealed record Settings
{
    public bool Enabled { get; init; } = true;

    public ShowOnMonitor ShowOn { get; init; } = ShowOnMonitor.Primary;

    public OverlayLayout Layout { get; init; } = OverlayLayout.List;
}
