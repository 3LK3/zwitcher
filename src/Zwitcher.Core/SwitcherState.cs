namespace Zwitcher.Core;

public sealed class SwitcherState : ISwitcherState
{
    // volatile rather than a lock: the hook callback reads this and must never block.
    private volatile bool _isActive;

    public bool IsActive
    {
        get => _isActive;
        set => _isActive = value;
    }
}
