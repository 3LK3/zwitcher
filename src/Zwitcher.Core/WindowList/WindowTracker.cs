namespace Zwitcher.Core.WindowList;

/// <summary>
/// Lists the eligible windows, most recently used first. Events only maintain the order.
/// Snapshot enumerates, so titles and eligibility can't go stale, and a missed event costs
/// order at worst, not a wrong list.
/// </summary>
public sealed class WindowTracker(IWindowQuery query, IWindowEventSource events) : IWindowTracker
{
    private readonly WindowFilter _filter = new(query);
    private readonly MruList _mru = new();

    /// <summary>The list may have changed. Raised during message pumping, so only post from here.</summary>
    public event Action? WindowsChanged;

    /// <summary>Takes the z-order as the initial guess and starts following events.</summary>
    public void Start()
    {
        _mru.Seed(Enumerate().Select(w => w.Handle));
        events.WindowEvent += OnWindowEvent;
    }

    public IReadOnlyList<WindowInfo> Snapshot() => _mru.Sort(Enumerate());

    public bool IsForeground(nint window)
    {
        var foreground = query.GetForegroundWindow();
        return foreground != 0
            && (
                foreground == window || query.GetRootOwner(foreground) == query.GetRootOwner(window)
            );
    }

    private List<WindowInfo> Enumerate()
    {
        var windows = new List<WindowInfo>();
        foreach (var window in query.EnumerateTopLevel())
        {
            if (_filter.IsEligible(window))
            {
                windows.Add(
                    new WindowInfo(window, query.GetTitle(window), query.GetProcessId(window))
                );
            }
        }

        return windows;
    }

    private void OnWindowEvent(WindowEventKind kind, nint window)
    {
        switch (kind)
        {
            case WindowEventKind.Foreground:
                // A dialog isn't listed, its owner is. So using the dialog counts for the owner.
                _mru.MoveToFront(query.GetRootOwner(window));
                _mru.MoveToFront(window);
                break;
            case WindowEventKind.MinimizeStart:
                _mru.MoveToBack(window);
                break;
            case WindowEventKind.Destroyed:
                // Windows reuses handles, and a new window must not inherit this rank.
                if (_mru.Remove(window))
                {
                    WindowsChanged?.Invoke();
                }

                break;
            case WindowEventKind.Changed:
                if (query.IsTopLevel(window))
                {
                    WindowsChanged?.Invoke();
                }

                break;
        }
    }
}
