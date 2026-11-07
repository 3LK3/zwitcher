namespace Zwitcher.Core.Input;

/// <summary>What the keyboard hook does with one key event.</summary>
public readonly record struct HookDecision(SwitcherCommand? Command, bool Swallow);

/// <summary>
/// The decision table from ARCHITECTURE.md as a pure function, so that it can be tested. This
/// runs on every keystroke system-wide: no allocation, logging or exceptions.
/// </summary>
public static class HookDecisions
{
    private const uint VkTab = 0x09;
    private const uint VkMenu = 0x12;
    private const uint VkEscape = 0x1B;
    private const uint VkLeft = 0x25;
    private const uint VkUp = 0x26;
    private const uint VkRight = 0x27;
    private const uint VkDown = 0x28;
    private const uint VkLeftMenu = 0xA4;
    private const uint VkRightMenu = 0xA5;

    private static readonly HookDecision PassThrough = new(null, false);

    public static HookDecision Decide(
        uint virtualKey,
        bool isKeyUp,
        bool altHeld,
        bool shiftHeld,
        bool switcherActive
    )
    {
        if (isKeyUp)
        {
            // Never swallowed, or the system would see a stuck Alt.
            return switcherActive && IsAlt(virtualKey)
                ? new HookDecision(SwitcherCommand.Confirm, false)
                : PassThrough;
        }

        if (virtualKey == VkTab && switcherActive)
        {
            return new HookDecision(shiftHeld ? SwitcherCommand.Prev : SwitcherCommand.Next, true);
        }

        if (virtualKey == VkTab && altHeld)
        {
            var command = shiftHeld ? SwitcherCommand.ActivatePrev : SwitcherCommand.Activate;
            return new HookDecision(command, true);
        }

        if (virtualKey == VkEscape && switcherActive)
        {
            return new HookDecision(SwitcherCommand.Cancel, true);
        }

        // Only while active: Alt+Left is "back" in every browser.
        if (virtualKey is VkLeft or VkUp && switcherActive)
        {
            return new HookDecision(SwitcherCommand.Prev, true);
        }

        if (virtualKey is VkRight or VkDown && switcherActive)
        {
            return new HookDecision(SwitcherCommand.Next, true);
        }

        return PassThrough;
    }

    private static bool IsAlt(uint virtualKey) => virtualKey is VkLeftMenu or VkRightMenu or VkMenu;
}
