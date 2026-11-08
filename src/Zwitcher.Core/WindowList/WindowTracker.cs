using Microsoft.Extensions.Logging;

namespace Zwitcher.Core.WindowList;

/// <summary>
/// Lists the eligible windows, most recently used first. Events only maintain the order.
/// Snapshot enumerates, so titles and eligibility can't go stale, and a missed event costs
/// order at worst, not a wrong list.
/// </summary>
public sealed class WindowTracker(
    IWindowQuery query,
    IWindowEventSource events,
    ILogger<WindowTracker> logger
) : IWindowTracker
{
    // Real owner chains are short. The cap only stops odd data from looping forever.
    private const int MaxOwnerWalk = 32;

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
                OnForeground(window);
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
            case WindowEventKind.Hidden:
                // Like minimizing: a window that hid itself (e.g. to the tray) and comes back
                // without focus must not sit in front of the window that was really used last.
                // Only known windows, because hide events also fire for menus and tooltips.
                _mru.MoveToBack(window);
                RaiseIfTopLevel(window);
                break;
            case WindowEventKind.Changed:
                RaiseIfTopLevel(window);
                break;
        }
    }

    /// <summary>
    /// Focus counts for exactly one window: the listed one. Moving its owner too would push the
    /// previously used window out of the second place.
    /// </summary>
    private void OnForeground(nint window)
    {
        var credited = ListedWindowFor(window);
        _mru.MoveToFront(credited);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Foreground {Handle} credited to {Credited} \"{Title}\"",
                window,
                credited,
                query.GetTitle(credited)
            );
        }
    }

    /// <summary>
    /// The first eligible window up the owner chain, starting at the root of the reported
    /// window. A dialog isn't listed but its owner is, so using the dialog counts for the owner.
    /// Falls back to the window itself, which may become eligible later (e.g. once it has a title).
    /// </summary>
    private nint ListedWindowFor(nint window)
    {
        var root = query.GetRoot(window);
        var start = root != 0 ? root : window;

        var current = start;
        for (var i = 0; i < MaxOwnerWalk && current != 0; i++)
        {
            if (_filter.IsEligible(current))
            {
                return current;
            }

            current = query.GetOwner(current);
        }

        return start;
    }

    private void RaiseIfTopLevel(nint window)
    {
        if (query.GetRoot(window) == window)
        {
            WindowsChanged?.Invoke();
        }
    }
}
