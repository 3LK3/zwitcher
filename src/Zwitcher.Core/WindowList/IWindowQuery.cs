namespace Zwitcher.Core.WindowList;

/// <summary>Reads window properties, so that filtering can be tested with fake data.</summary>
public interface IWindowQuery
{
    /// <summary>All top-level windows in z-order, topmost first.</summary>
    IReadOnlyList<nint> EnumerateTopLevel();

    /// <summary>The foreground window, or 0 if there is none.</summary>
    nint GetForegroundWindow();

    /// <summary>The top-level window that contains this one; the window itself if it is top-level.</summary>
    nint GetRoot(nint window);

    /// <summary>The direct owner, or 0 if there is none.</summary>
    nint GetOwner(nint window);

    bool IsVisible(nint window);

    bool IsCloaked(nint window);

    bool IsShellWindow(nint window);

    WindowExStyle GetExStyle(nint window);

    /// <summary>The end of the owner chain; the window itself if it has no owner.</summary>
    nint GetRootOwner(nint window);

    /// <summary>The owned window that was active last; the window itself if there is none.</summary>
    nint GetLastActivePopup(nint window);

    string GetTitle(nint window);

    int GetProcessId(nint window);
}
