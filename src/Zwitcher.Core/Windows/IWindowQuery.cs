namespace Zwitcher.Core.Windows;

/// <summary>Reads window properties, so that filtering can be tested with fake data.</summary>
public interface IWindowQuery
{
    /// <summary>All top-level windows in z-order, topmost first.</summary>
    IReadOnlyList<nint> EnumerateTopLevel();

    /// <summary>The foreground window, or 0 if there is none.</summary>
    nint GetForegroundWindow();

    bool IsTopLevel(nint window);

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
