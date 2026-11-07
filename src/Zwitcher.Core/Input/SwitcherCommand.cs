namespace Zwitcher.Core.Input;

/// <summary>Commands posted to the UI thread. The value travels in the message's WPARAM.</summary>
public enum SwitcherCommand
{
    /// <summary>Alt+Tab pressed while idle.</summary>
    Activate,

    /// <summary>Alt+Shift+Tab pressed while idle.</summary>
    ActivatePrev,

    /// <summary>Tab while active.</summary>
    Next,

    /// <summary>Shift+Tab while active.</summary>
    Prev,

    /// <summary>Alt released while active.</summary>
    Confirm,

    /// <summary>Esc while active.</summary>
    Cancel,
}
