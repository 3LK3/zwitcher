namespace Zwitcher.Core.WindowList;

public enum WindowEventKind
{
    /// <summary>The window became the foreground window.</summary>
    Foreground,

    /// <summary>The window is being minimized.</summary>
    MinimizeStart,

    /// <summary>The window was destroyed.</summary>
    Destroyed,

    /// <summary>The window was hidden.</summary>
    Hidden,

    /// <summary>The window was shown, cloaked, uncloaked or got a new title.</summary>
    Changed,
}
