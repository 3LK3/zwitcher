using Zwitcher.Core.Switching;
using Zwitcher.Core.WindowList;
using static Zwitcher.Core.Switching.SwitcherCommand;

namespace Zwitcher.Core.Tests;

public sealed class SwitcherControllerTests
{
    private readonly FakeView _view = new();
    private readonly FakeActivator _activator = new();
    private readonly SwitcherActiveFlag _activeFlag = new() { IsSet = true };
    private readonly FakeTracker _tracker = new();

    private static WindowInfo Window(int index) => new(100 + index, $"Window {index}", index);

    private SwitcherController CreateController(int windowCount)
    {
        _tracker.Windows = [.. Enumerable.Range(0, windowCount).Select(Window)];

        return new SwitcherController(_tracker, _view, _activator, _activeFlag);
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
        Assert.False(_activeFlag.IsSet);
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
        Assert.False(_activeFlag.IsSet);
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
        Assert.True(_activeFlag.IsSet);

        Send(controller, Confirm);
        Assert.False(_activeFlag.IsSet);
    }

    [Fact]
    public void ItemClicked_activates_that_window()
    {
        var controller = CreateController(4);
        Send(controller, Activate);

        _view.Click(3);

        Assert.Equal("Hide", _view.Calls[^1]);
        Assert.Equal([103], _activator.Activated);
        Assert.False(_activeFlag.IsSet);
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

    [Fact]
    public void Activate_selects_the_first_window_if_it_is_not_in_the_foreground()
    {
        var controller = CreateController(4);
        _tracker.FirstIsForeground = false;

        Send(controller, Activate);

        Assert.Equal(["Show 4 0"], _view.Calls);
    }

    [Fact]
    public void ActivatePrev_selects_the_last_window_if_the_first_is_not_in_the_foreground()
    {
        var controller = CreateController(4);
        _tracker.FirstIsForeground = false;

        Send(controller, ActivatePrev);

        Assert.Equal(["Show 4 3"], _view.Calls);
    }

    [Fact]
    public void WindowsChanged_keeps_the_same_window_selected()
    {
        var controller = CreateController(3);
        Send(controller, Activate);

        _tracker.Windows = [Window(9), .. _tracker.Windows];
        Send(controller, WindowsChanged);

        Assert.Equal("Update 4 2", _view.Calls[^1]);
        Send(controller, Confirm);
        Assert.Equal([101], _activator.Activated);
    }

    [Fact]
    public void WindowsChanged_selects_the_nearest_index_if_the_selected_window_closed()
    {
        var controller = CreateController(4);
        Send(controller, Activate);

        _tracker.Windows = [Window(0), Window(2), Window(3)];
        Send(controller, WindowsChanged);

        Assert.Equal("Update 3 1", _view.Calls[^1]);
        Send(controller, Confirm);
        Assert.Equal([102], _activator.Activated);
    }

    [Fact]
    public void WindowsChanged_clamps_to_the_end_if_the_last_window_closed()
    {
        var controller = CreateController(3);
        Send(controller, Activate, Next);

        _tracker.Windows = [Window(0), Window(1)];
        Send(controller, WindowsChanged);

        Assert.Equal("Update 2 1", _view.Calls[^1]);
    }

    [Fact]
    public void Next_after_WindowsChanged_uses_the_new_list()
    {
        var controller = CreateController(2);
        Send(controller, Activate);

        _tracker.Windows = [Window(0), Window(1), Window(2)];
        Send(controller, WindowsChanged, Next, Confirm);

        Assert.Equal([102], _activator.Activated);
    }

    [Fact]
    public void WindowsChanged_shows_a_new_title()
    {
        var controller = CreateController(2);
        Send(controller, Activate);

        _tracker.Windows = [Window(0), Window(1) with { Title = "Renamed" }];
        Send(controller, WindowsChanged);

        Assert.Equal("Update 2 1", _view.Calls[^1]);
    }

    [Fact]
    public void WindowsChanged_with_an_unchanged_list_does_not_touch_the_view()
    {
        var controller = CreateController(3);
        Send(controller, Activate);

        Send(controller, WindowsChanged);

        Assert.Equal(["Show 3 1"], _view.Calls);
    }

    [Fact]
    public void WindowsChanged_with_no_windows_left_closes_without_activating()
    {
        var controller = CreateController(2);
        Send(controller, Activate);

        _tracker.Windows = [];
        Send(controller, WindowsChanged);

        Assert.Equal("Hide", _view.Calls[^1]);
        Assert.Empty(_activator.Activated);
        Assert.False(_activeFlag.IsSet);
    }

    [Fact]
    public void WindowsChanged_is_ignored_in_idle()
    {
        var controller = CreateController(3);

        Send(controller, WindowsChanged);

        Assert.Empty(_view.Calls);
    }

    private sealed class FakeTracker : IWindowTracker
    {
        public IReadOnlyList<WindowInfo> Windows { get; set; } = [];

        public bool FirstIsForeground { get; set; } = true;

        public IReadOnlyList<WindowInfo> Snapshot() => Windows;

        public bool IsForeground(nint window) => FirstIsForeground;
    }

    private sealed class FakeActivator : IWindowActivator
    {
        public List<nint> Activated { get; } = [];

        public int PrepareCount { get; private set; }

        public void Prepare() => PrepareCount++;

        public void Activate(nint handle) => Activated.Add(handle);
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
