namespace Zwitcher.Core.Switching;

/// <summary>
/// The only state shared between the hook thread and the UI thread. The hook sets it when it
/// posts an activate command, so that the following keys go to the switcher; the controller
/// clears it when it returns to Idle.
/// </summary>
public sealed class SwitcherActiveFlag
{
    // volatile rather than a lock: the hook callback reads this and must never block.
    private volatile bool _isSet;

    public bool IsSet
    {
        get => _isSet;
        set => _isSet = value;
    }
}
