namespace Zwitcher.Core.Windows;

/// <summary>Window handles in recency order, most recently used first.</summary>
public sealed class MruList
{
    private readonly List<nint> _order = [];

    public void Seed(IEnumerable<nint> windows)
    {
        _order.Clear();
        _order.AddRange(windows);
    }

    public void MoveToFront(nint window)
    {
        _order.Remove(window);
        _order.Insert(0, window);
    }

    public void MoveToBack(nint window)
    {
        _order.Remove(window);
        _order.Add(window);
    }

    /// <summary>Returns false if the window wasn't in the list.</summary>
    public bool Remove(nint window) => _order.Remove(window);

    /// <summary>Sorts by recency. Unknown windows go last and keep their relative order.</summary>
    public IReadOnlyList<WindowInfo> Sort(IReadOnlyList<WindowInfo> windows) =>
        [.. windows.OrderBy(Rank)];

    private int Rank(WindowInfo window)
    {
        var index = _order.IndexOf(window.Handle);
        return index < 0 ? int.MaxValue : index;
    }
}
