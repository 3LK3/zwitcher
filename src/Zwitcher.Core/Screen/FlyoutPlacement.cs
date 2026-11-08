namespace Zwitcher.Core.Screen;

/// <summary>
/// Places a flyout in the corner of the work area at the tray end of the taskbar, flush
/// against the taskbar: bottom-right for the usual bottom taskbar. Pure math, so it's testable
/// without a screen. Not on a hot path: it runs once per opening of the settings flyout.
/// </summary>
public static class FlyoutPlacement
{
    /// <summary>The top-left corner for a flyout of the given size, kept inside the work area.</summary>
    /// <param name="anchor">
    /// The clicked tray icon. Only needed when the work area doesn't tell the taskbar edge.
    /// </param>
    public static (int X, int Y) Compute(
        PixelPoint anchor,
        PixelRect monitor,
        PixelRect work,
        int width,
        int height
    )
    {
        var (x, y) = EdgeOf(anchor, monitor, work) switch
        {
            TaskbarEdge.Top => (work.Right - width, work.Top),
            TaskbarEdge.Left => (work.Left, work.Bottom - height),
            _ => (work.Right - width, work.Bottom - height),
        };

        // A flyout larger than the work area starts at its top-left edge.
        return (Math.Max(work.Left, x), Math.Max(work.Top, y));
    }

    /// <summary>The side where the work area is smaller than the monitor.</summary>
    private static TaskbarEdge EdgeOf(PixelPoint anchor, PixelRect monitor, PixelRect work)
    {
        if (work.Bottom < monitor.Bottom)
        {
            return TaskbarEdge.Bottom;
        }
        if (work.Top > monitor.Top)
        {
            return TaskbarEdge.Top;
        }
        if (work.Left > monitor.Left)
        {
            return TaskbarEdge.Left;
        }
        if (work.Right < monitor.Right)
        {
            return TaskbarEdge.Right;
        }

        // The work area fills the monitor, e.g. with an auto-hidden taskbar. The tray icon that
        // was clicked sits on the taskbar, so the monitor edge nearest to it is the taskbar edge.
        var distances = new[]
        {
            (Distance: monitor.Bottom - anchor.Y, Edge: TaskbarEdge.Bottom),
            (Distance: anchor.Y - monitor.Top, Edge: TaskbarEdge.Top),
            (Distance: anchor.X - monitor.Left, Edge: TaskbarEdge.Left),
            (Distance: monitor.Right - anchor.X, Edge: TaskbarEdge.Right),
        };
        return distances.MinBy(d => d.Distance).Edge;
    }
}
