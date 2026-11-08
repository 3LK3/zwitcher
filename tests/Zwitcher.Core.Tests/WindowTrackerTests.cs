using Zwitcher.Core.WindowList;

namespace Zwitcher.Core.Tests;

public sealed class WindowTrackerTests
{
    private readonly FakeWindowEventSource _events = new();

    private WindowTracker Started(FakeWindowQuery query)
    {
        var tracker = new WindowTracker(query, _events);
        tracker.Start();
        return tracker;
    }

    private static IEnumerable<nint> Handles(WindowTracker tracker) =>
        tracker.Snapshot().Select(w => w.Handle);

    private static FakeWindowQuery ThreeWindows() =>
        new(new FakeWindow(1), new FakeWindow(2), new FakeWindow(3));

    [Fact]
    public void Snapshot_keeps_z_order_and_drops_ineligible_windows()
    {
        var query = new FakeWindowQuery(
            new FakeWindow(30),
            new FakeWindow(10) { Visible = false },
            new FakeWindow(20)
        );

        Assert.Equal([30, 20], Handles(Started(query)));
    }

    [Fact]
    public void Snapshot_maps_the_window_properties()
    {
        var query = new FakeWindowQuery(new FakeWindow(7) { Title = "Notes", ProcessId = 42 });

        var window = Assert.Single(Started(query).Snapshot());

        Assert.Equal(new WindowInfo(7, "Notes", 42), window);
    }

    [Fact]
    public void Snapshot_reflects_later_changes()
    {
        var query = new FakeWindowQuery(new FakeWindow(1));
        var tracker = Started(query);
        Assert.Single(tracker.Snapshot());

        query.Windows.Add(new FakeWindow(2));

        Assert.Equal(2, tracker.Snapshot().Count);
    }

    [Fact]
    public void Snapshot_is_empty_without_windows()
    {
        Assert.Empty(Started(new FakeWindowQuery()).Snapshot());
    }

    [Fact]
    public void Start_uses_the_z_order_as_the_initial_guess()
    {
        Assert.Equal([1, 2, 3], Handles(Started(ThreeWindows())));
    }

    [Fact]
    public void Foreground_events_set_the_recency_order()
    {
        var tracker = Started(ThreeWindows());

        _events.Raise(WindowEventKind.Foreground, 3);
        _events.Raise(WindowEventKind.Foreground, 2);

        Assert.Equal([2, 3, 1], Handles(tracker));
    }

    [Fact]
    public void Alternating_between_two_windows_keeps_them_on_top()
    {
        var tracker = Started(ThreeWindows());

        _events.Raise(WindowEventKind.Foreground, 2);
        _events.Raise(WindowEventKind.Foreground, 1);
        _events.Raise(WindowEventKind.Foreground, 2);

        Assert.Equal([2, 1, 3], Handles(tracker));
    }

    [Fact]
    public void A_foreground_dialog_counts_for_its_owner()
    {
        var query = new FakeWindowQuery(
            new FakeWindow(1),
            new FakeWindow(2) { LastActivePopup = 9 },
            new FakeWindow(9) { RootOwner = 2 }
        );
        var tracker = Started(query);

        _events.Raise(WindowEventKind.Foreground, 9);

        Assert.Equal([2, 1], Handles(tracker));
    }

    [Fact]
    public void Foreground_of_an_unlisted_window_keeps_the_order()
    {
        var query = ThreeWindows();
        query.Windows.Add(new FakeWindow(9) { ExStyle = WindowExStyle.ToolWindow });
        var tracker = Started(query);

        _events.Raise(WindowEventKind.Foreground, 9);

        Assert.Equal([1, 2, 3], Handles(tracker));
    }

    [Fact]
    public void Minimizing_moves_a_window_to_the_back()
    {
        var tracker = Started(ThreeWindows());

        _events.Raise(WindowEventKind.MinimizeStart, 1);

        Assert.Equal([2, 3, 1], Handles(tracker));
    }

    [Fact]
    public void A_new_window_that_never_had_focus_goes_last()
    {
        var query = ThreeWindows();
        var tracker = Started(query);

        query.Windows.Insert(0, new FakeWindow(9));

        Assert.Equal([1, 2, 3, 9], Handles(tracker));
    }

    [Fact]
    public void A_reused_handle_does_not_inherit_the_old_rank()
    {
        var tracker = Started(ThreeWindows());

        _events.Raise(WindowEventKind.Destroyed, 1);

        Assert.Equal([2, 3, 1], Handles(tracker));
    }

    [Fact]
    public void Destroying_a_known_window_raises_WindowsChanged()
    {
        var tracker = Started(ThreeWindows());
        var raised = 0;
        tracker.WindowsChanged += () => raised++;

        _events.Raise(WindowEventKind.Destroyed, 2);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Destroying_an_unknown_window_does_not_raise_WindowsChanged()
    {
        var tracker = Started(ThreeWindows());
        var raised = 0;
        tracker.WindowsChanged += () => raised++;

        _events.Raise(WindowEventKind.Destroyed, 9);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Changes_of_top_level_windows_raise_WindowsChanged()
    {
        var tracker = Started(ThreeWindows());
        var raised = 0;
        tracker.WindowsChanged += () => raised++;

        _events.Raise(WindowEventKind.Changed, 2);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Changes_of_child_windows_do_not_raise_WindowsChanged()
    {
        var query = ThreeWindows();
        query.Windows.Add(new FakeWindow(9) { TopLevel = false });
        var tracker = Started(query);
        var raised = 0;
        tracker.WindowsChanged += () => raised++;

        _events.Raise(WindowEventKind.Changed, 9);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void Foreground_alone_does_not_raise_WindowsChanged()
    {
        var tracker = Started(ThreeWindows());
        var raised = 0;
        tracker.WindowsChanged += () => raised++;

        _events.Raise(WindowEventKind.Foreground, 2);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void IsForeground_is_true_for_the_foreground_window()
    {
        var query = ThreeWindows();
        query.Foreground = 2;
        var tracker = Started(query);

        Assert.True(tracker.IsForeground(2));
        Assert.False(tracker.IsForeground(1));
    }

    [Fact]
    public void IsForeground_is_true_for_the_owner_of_a_foreground_dialog()
    {
        var query = new FakeWindowQuery(new FakeWindow(1), new FakeWindow(9) { RootOwner = 1 });
        query.Foreground = 9;
        var tracker = Started(query);

        Assert.True(tracker.IsForeground(1));
    }

    [Fact]
    public void IsForeground_is_false_without_a_foreground_window()
    {
        var tracker = Started(ThreeWindows());

        Assert.False(tracker.IsForeground(1));
    }
}
