using Zwitcher.Core.Windows;

namespace Zwitcher.Core.Tests;

public sealed class MruListTests
{
    private static WindowInfo[] Windows(params nint[] handles) =>
        [.. handles.Select(h => new WindowInfo(h, "Title", 1, null))];

    private static IEnumerable<nint> Sorted(MruList list, params nint[] handles) =>
        list.Sort(Windows(handles)).Select(w => w.Handle);

    private static MruList Seeded(params nint[] handles)
    {
        var list = new MruList();
        list.Seed(handles);
        return list;
    }

    [Fact]
    public void Sort_follows_the_seeded_order()
    {
        Assert.Equal([3, 1, 2], Sorted(Seeded(3, 1, 2), 1, 2, 3));
    }

    [Fact]
    public void MoveToFront_moves_a_known_window()
    {
        var list = Seeded(1, 2, 3);

        list.MoveToFront(3);

        Assert.Equal([3, 1, 2], Sorted(list, 1, 2, 3));
    }

    [Fact]
    public void MoveToFront_adds_an_unknown_window()
    {
        var list = Seeded(1, 2);

        list.MoveToFront(9);

        Assert.Equal([9, 1, 2], Sorted(list, 1, 2, 9));
    }

    [Fact]
    public void MoveToBack_moves_a_known_window()
    {
        var list = Seeded(1, 2, 3);

        list.MoveToBack(1);

        Assert.Equal([2, 3, 1], Sorted(list, 1, 2, 3));
    }

    [Fact]
    public void Remove_reports_whether_the_window_was_known()
    {
        var list = Seeded(1, 2);

        Assert.True(list.Remove(1));
        Assert.False(list.Remove(1));
    }

    [Fact]
    public void Removed_window_loses_its_rank()
    {
        var list = Seeded(1, 2);

        list.Remove(1);

        Assert.Equal([2, 1], Sorted(list, 1, 2));
    }

    [Fact]
    public void Unknown_windows_go_last_and_keep_their_order()
    {
        Assert.Equal([1, 8, 7], Sorted(Seeded(1), 8, 1, 7));
    }

    [Fact]
    public void Handles_missing_from_the_input_are_skipped()
    {
        Assert.Equal([3, 1], Sorted(Seeded(3, 2, 1), 1, 3));
    }
}
