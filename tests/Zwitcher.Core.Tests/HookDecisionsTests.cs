using Zwitcher.Core.Input;

namespace Zwitcher.Core.Tests;

public sealed class HookDecisionsTests
{
    private const uint Tab = 0x09;
    private const uint Escape = 0x1B;
    private const uint LeftAlt = 0xA4;
    private const uint RightAlt = 0xA5;
    private const uint LetterA = 0x41;
    private const uint ArrowLeft = 0x25;
    private const uint ArrowUp = 0x26;
    private const uint ArrowRight = 0x27;
    private const uint ArrowDown = 0x28;

    private static readonly HookDecision PassThrough = new(null, false);

    private static HookDecision Down(uint key, bool alt, bool shift, bool active) =>
        HookDecisions.Decide(key, false, alt, shift, active);

    private static HookDecision Up(uint key, bool active) =>
        HookDecisions.Decide(key, true, false, false, active);

    [Fact]
    public void Alt_Tab_while_idle_activates_and_swallows()
    {
        var decision = Down(Tab, alt: true, shift: false, active: false);

        Assert.Equal(new HookDecision(SwitcherCommand.Activate, true), decision);
    }

    [Fact]
    public void Alt_Shift_Tab_while_idle_activates_in_reverse_and_swallows()
    {
        var decision = Down(Tab, alt: true, shift: true, active: false);

        Assert.Equal(new HookDecision(SwitcherCommand.ActivatePrev, true), decision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tab_without_Alt_passes_through_while_idle(bool shift)
    {
        Assert.Equal(PassThrough, Down(Tab, alt: false, shift, active: false));
    }

    [Fact]
    public void Tab_while_active_moves_next_and_swallows()
    {
        var decision = Down(Tab, alt: true, shift: false, active: true);

        Assert.Equal(new HookDecision(SwitcherCommand.Next, true), decision);
    }

    [Fact]
    public void Shift_Tab_while_active_moves_back_and_swallows()
    {
        var decision = Down(Tab, alt: true, shift: true, active: true);

        Assert.Equal(new HookDecision(SwitcherCommand.Prev, true), decision);
    }

    [Fact]
    public void Esc_while_active_cancels_and_swallows()
    {
        var decision = Down(Escape, alt: true, shift: false, active: true);

        Assert.Equal(new HookDecision(SwitcherCommand.Cancel, true), decision);
    }

    [Fact]
    public void Esc_while_idle_passes_through()
    {
        Assert.Equal(PassThrough, Down(Escape, alt: true, shift: false, active: false));
    }

    [Theory]
    [InlineData(LeftAlt)]
    [InlineData(RightAlt)]
    public void Alt_up_while_active_confirms_and_is_never_swallowed(uint alt)
    {
        Assert.Equal(new HookDecision(SwitcherCommand.Confirm, false), Up(alt, active: true));
    }

    [Theory]
    [InlineData(LeftAlt)]
    [InlineData(RightAlt)]
    public void Alt_up_while_idle_passes_through(uint alt)
    {
        Assert.Equal(PassThrough, Up(alt, active: false));
    }

    [Theory]
    [InlineData(Tab)]
    [InlineData(Escape)]
    public void Other_key_up_passes_through_while_active(uint key)
    {
        Assert.Equal(PassThrough, Up(key, active: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Any_other_key_passes_through(bool active)
    {
        Assert.Equal(PassThrough, Down(LetterA, alt: true, shift: false, active));
    }

    [Theory]
    [InlineData(ArrowLeft)]
    [InlineData(ArrowUp)]
    public void Left_and_up_while_active_move_back_and_swallow(uint key)
    {
        var decision = Down(key, alt: true, shift: false, active: true);

        Assert.Equal(new HookDecision(SwitcherCommand.Prev, true), decision);
    }

    [Theory]
    [InlineData(ArrowRight)]
    [InlineData(ArrowDown)]
    public void Right_and_down_while_active_move_next_and_swallow(uint key)
    {
        var decision = Down(key, alt: true, shift: false, active: true);

        Assert.Equal(new HookDecision(SwitcherCommand.Next, true), decision);
    }

    [Theory]
    [InlineData(ArrowLeft, false)]
    [InlineData(ArrowLeft, true)]
    [InlineData(ArrowUp, true)]
    [InlineData(ArrowRight, true)]
    [InlineData(ArrowDown, true)]
    public void Arrows_pass_through_while_idle(uint key, bool alt)
    {
        Assert.Equal(PassThrough, Down(key, alt, shift: false, active: false));
    }

    [Theory]
    [InlineData(ArrowLeft)]
    [InlineData(ArrowDown)]
    public void Arrow_key_up_passes_through_while_active(uint key)
    {
        Assert.Equal(PassThrough, Up(key, active: true));
    }
}
