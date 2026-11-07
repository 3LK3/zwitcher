using Zwitcher.Core.Windows;

namespace Zwitcher.App.Debugging;

/// <summary>Temporary stand-in with fixed fake windows. M2 replaces it.</summary>
public sealed class StubWindowTracker : IWindowTracker
{
    private static readonly WindowInfo[] Windows =
    [
        new(1, "Fake window 1", 1, null),
        new(2, "Fake window 2", 2, null),
        new(3, "Fake window 3", 3, null),
        new(4, "Fake window 4", 4, null),
    ];

    public IReadOnlyList<WindowInfo> Snapshot() => Windows;
}
