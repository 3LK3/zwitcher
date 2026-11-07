using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Tests;

public sealed class WindowTrackerTests
{
    [Fact]
    public void Snapshot_keeps_z_order_and_drops_ineligible_windows()
    {
        var query = new FakeWindowQuery(
            new FakeWindow(30),
            new FakeWindow(10) { Visible = false },
            new FakeWindow(20)
        );

        var handles = new WindowTracker(query).Snapshot().Select(w => w.Handle);

        Assert.Equal([30, 20], handles);
    }

    [Fact]
    public void Snapshot_maps_the_window_properties()
    {
        var query = new FakeWindowQuery(new FakeWindow(7) { Title = "Notes", ProcessId = 42 });

        var window = Assert.Single(new WindowTracker(query).Snapshot());

        Assert.Equal(new WindowInfo(7, "Notes", 42, null), window);
    }

    [Fact]
    public void Snapshot_reflects_later_changes()
    {
        var query = new FakeWindowQuery(new FakeWindow(1));
        var tracker = new WindowTracker(query);
        Assert.Single(tracker.Snapshot());

        query.Windows.Add(new FakeWindow(2));

        Assert.Equal(2, tracker.Snapshot().Count);
    }

    [Fact]
    public void Snapshot_is_empty_without_windows()
    {
        Assert.Empty(new WindowTracker(new FakeWindowQuery()).Snapshot());
    }
}
