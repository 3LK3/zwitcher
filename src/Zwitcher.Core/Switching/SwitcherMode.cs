namespace Zwitcher.Core.Switching;

/// <summary>The states of the controller's state machine, see ARCHITECTURE.md.</summary>
public enum SwitcherMode
{
    /// <summary>The overlay is hidden.</summary>
    Idle,

    /// <summary>The overlay is shown and a window is selected.</summary>
    Active,
}
