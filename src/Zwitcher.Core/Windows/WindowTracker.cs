namespace Zwitcher.Core.Windows;

/// <summary>
/// Lists the eligible windows in z-order. It enumerates on every call for now; M6 adds the
/// cached MRU list fed by WinEvents.
/// </summary>
public sealed class WindowTracker(IWindowQuery query) : IWindowTracker
{
    private readonly WindowFilter _filter = new(query);

    public IReadOnlyList<WindowInfo> Snapshot()
    {
        var windows = new List<WindowInfo>();
        foreach (var window in query.EnumerateTopLevel())
        {
            if (_filter.IsEligible(window))
            {
                windows.Add(
                    new WindowInfo(
                        window,
                        query.GetTitle(window),
                        query.GetProcessId(window),
                        AppId: null
                    )
                );
            }
        }

        return windows;
    }
}
