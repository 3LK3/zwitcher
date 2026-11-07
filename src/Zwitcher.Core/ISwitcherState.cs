namespace Zwitcher.Core;

/// <summary>
/// The switcherActive flag, the only state shared between the hook thread and the UI thread.
/// The hook sets it when it posts an activate command; the controller clears it in Idle.
/// </summary>
public interface ISwitcherState
{
    bool IsActive { get; set; }
}
