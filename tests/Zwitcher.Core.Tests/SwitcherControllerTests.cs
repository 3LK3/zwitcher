using Zwitcher.Core.Activation;
using Zwitcher.Core.Input;
using Zwitcher.Core.Windows;
using static Zwitcher.Core.Input.SwitcherCommand;

namespace Zwitcher.Core.Tests;

public sealed class SwitcherControllerTests
{
    private readonly FakeView _view = new();
    private readonly FakeActivator _activator = new();
    private readonly FakeState _state = new() { IsActive = true };

    private SwitcherController CreateController(int windowCount)
    {
        var windows = Enumerable
            .Range(0, windowCount)
            .Select(i => new WindowInfo(100 + i, $"Window {i}", i, null))
            .ToList();

        return new SwitcherController(new FakeTracker(windows), _view, _activator, _state);
    }

    private static void Send(SwitcherController controller, params SwitcherCommand[] commands)
    {
        foreach (var command in commands)
        {
            controller.Handle(command);
        }
    }

    [Fact]
    public void Activate_shows_the_windows_with_the_second_one_selected()
    {
        var controller = CreateController(4);

        Send(controller, Activate);

        Assert.Equal(["Show 4 1"], _view.Calls);
    }

    [Fact]
    public void Activate_Next_Next_Confirm_activates_the_fourth_window()
    {
        var controller = CreateController(4);

        Send(controller, Activate, Next, Next, Confirm);

        Assert.Equal(["Show 4 1", "Select 2", "Select 3", "Hide"], _view.Calls);
        Assert.Equal([103], _activator.Activated);
    }

    [Fact]
    public void Quick_tap_activates_the_second_window()
    {
        var controller = CreateController(4);

        Send(controller, Activate, Confirm);

        Assert.Equal([101], _activator.Activated);
    }

    [Fact]
    public void Next_wraps_from_the_last_window_to_the_first()
    {
        var controller = CreateController(3);

        Send(controller, Activate, Next, Next, Confirm);

        Assert.Equal([100], _activator.Activated);
    }

    [Fact]
    public void Prev_moves_the_selection_back()
    {
        var controller = CreateController(3);

        Send(controller, Activate, Next, Prev, Confirm);

        Assert.Equal([101], _activator.Activated);
    }

    [Fact]
    public void Prev_wraps_from_the_first_window_to_the_last()
    {
        var controller = CreateController(3);

        Send(controller, Activate, Prev, Prev, Confirm);

        Assert.Equal([102], _activator.Activated);
    }

    [Fact]
    public void ActivatePrev_selects_the_last_window()
    {
        var controller = CreateController(4);

        Send(controller, ActivatePrev);

        Assert.Equal(["Show 4 3"], _view.Calls);
    }

    [Theory]
    [InlineData(Activate)]
    [InlineData(ActivatePrev)]
    public void Single_window_is_selected_for_both_activations(SwitcherCommand command)
    {
        var controller = CreateController(1);

        Send(controller, command, Next, Prev, Confirm);

        Assert.Equal("Show 1 0", _view.Calls[0]);
        Assert.Equal([100], _activator.Activated);
    }

    [Fact]
    public void Cancel_hides_and_activates_nothing()
    {
        var controller = CreateController(3);

        Send(controller, Activate, Next, Cancel);

        Assert.Equal("Hide", _view.Calls[^1]);
        Assert.Empty(_activator.Activated);
        Assert.False(_state.IsActive);
    }

    [Theory]
    [InlineData(Activate)]
    [InlineData(ActivatePrev)]
    public void Empty_list_shows_nothing_and_stays_idle(SwitcherCommand command)
    {
        var controller = CreateController(0);

        Send(controller, command, Next, Confirm);

        Assert.Empty(_view.Calls);
        Assert.Empty(_activator.Activated);
        Assert.False(_state.IsActive);
    }

    [Theory]
    [InlineData(Next)]
    [InlineData(Prev)]
    [InlineData(Confirm)]
    [InlineData(Cancel)]
    public void Commands_are_ignored_in_idle(SwitcherCommand command)
    {
        var controller = CreateController(3);

        Send(controller, command);

        Assert.Empty(_view.Calls);
        Assert.Empty(_activator.Activated);
    }

    [Theory]
    [InlineData(Activate)]
    [InlineData(ActivatePrev)]
    public void Activating_again_while_active_is_ignored(SwitcherCommand command)
    {
        var controller = CreateController(3);

        Send(controller, Activate, command);

        Assert.Equal(["Show 3 1"], _view.Calls);
    }

    [Fact]
    public void Unknown_command_is_ignored()
    {
        var controller = CreateController(3);

        Send(controller, Activate, (SwitcherCommand)99);

        Assert.Equal(["Show 3 1"], _view.Calls);
    }

    [Fact]
    public void Switcher_can_be_opened_again_after_closing()
    {
        var controller = CreateController(3);

        Send(controller, Activate, Confirm, Activate, Next, Confirm);

        Assert.Equal([101, 102], _activator.Activated);
    }

    [Fact]
    public void Flag_is_cleared_after_confirm()
    {
        var controller = CreateController(3);

        Send(controller, Activate);
        Assert.True(_state.IsActive);

        Send(controller, Confirm);
        Assert.False(_state.IsActive);
    }

    [Fact]
    public void ItemClicked_activates_that_window()
    {
        var controller = CreateController(4);
        Send(controller, Activate);

        _view.Click(3);

        Assert.Equal("Hide", _view.Calls[^1]);
        Assert.Equal([103], _activator.Activated);
        Assert.False(_state.IsActive);
    }

    [Fact]
    public void ItemClicked_is_ignored_in_idle()
    {
        CreateController(4);

        _view.Click(1);

        Assert.Empty(_view.Calls);
        Assert.Empty(_activator.Activated);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void ItemClicked_out_of_range_is_ignored(int index)
    {
        var controller = CreateController(4);
        Send(controller, Activate);

        _view.Click(index);

        Assert.Equal(["Show 4 1"], _view.Calls);
        Assert.Empty(_activator.Activated);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    public void Opening_prepares_the_activator_once(int windowCount)
    {
        var controller = CreateController(windowCount);

        Send(controller, Activate);

        Assert.Equal(1, _activator.PrepareCount);
    }

    [Fact]
    public void Commands_while_active_do_not_prepare_again()
    {
        var controller = CreateController(4);

        Send(controller, Activate, Next, Activate, Confirm);

        Assert.Equal(1, _activator.PrepareCount);
    }

    private sealed class FakeTracker(IReadOnlyList<WindowInfo> windows) : IWindowTracker
    {
        public IReadOnlyList<WindowInfo> Snapshot() => windows;
    }

    private sealed class FakeActivator : IWindowActivator
    {
        public List<nint> Activated { get; } = [];

        public int PrepareCount { get; private set; }

        public void Prepare() => PrepareCount++;

        public void Activate(nint handle) => Activated.Add(handle);
    }

    private sealed class FakeState : ISwitcherState
    {
        public bool IsActive { get; set; }
    }

    private sealed class FakeView : IOverlayView
    {
        public event Action<int>? ItemClicked;

        public List<string> Calls { get; } = [];

        public void Click(int index) => ItemClicked?.Invoke(index);

        public void Show(IReadOnlyList<WindowInfo> windows, int selectedIndex) =>
            Calls.Add($"Show {windows.Count} {selectedIndex}");

        public void UpdateSelection(int selectedIndex) => Calls.Add($"Select {selectedIndex}");

        public void UpdateWindows(IReadOnlyList<WindowInfo> windows, int selectedIndex) =>
            Calls.Add($"Update {windows.Count} {selectedIndex}");

        public void Hide() => Calls.Add("Hide");
    }
}
