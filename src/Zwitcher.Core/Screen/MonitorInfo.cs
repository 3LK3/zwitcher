namespace Zwitcher.Core.Screen;

/// <summary>
/// A monitor in physical pixels: its full bounds, its work area (without the taskbar) and its
/// DPI.
/// </summary>
public sealed record MonitorInfo(PixelRect Bounds, PixelRect WorkArea, uint Dpi)
{
    /// <summary>
    /// The top-left corner that centers a size in the work area. A larger size starts at the
    /// edge.
    /// </summary>
    public (int X, int Y) Center(int width, int height) =>
        (
            WorkArea.Left + Math.Max(0, (WorkArea.Width - width) / 2),
            WorkArea.Top + Math.Max(0, (WorkArea.Height - height) / 2)
        );
}
