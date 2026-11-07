namespace Zwitcher.Core.Windows;

public enum WindowEventKind
{
    /// <summary>The window became the foreground window.</summary>
    Foreground,

    /// <summary>The window is being minimized.</summary>
    MinimizeStart,

    /// <summary>The window was destroyed.</summary>
    Destroyed,

    /// <summary>The window was shown, hidden, cloaked, uncloaked or got a new title.</summary>
    Changed,
}

/// <summary>Reports what happens to windows, so that the tracker can be tested with fake events.</summary>
public interface IWindowEventSource
{
    event Action<WindowEventKind, nint>? WindowEvent;
}
