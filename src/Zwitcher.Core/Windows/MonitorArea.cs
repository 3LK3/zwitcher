namespace Zwitcher.Core.Windows;

/// <summary>A monitor's work area in physical pixels, and its DPI.</summary>
public sealed record MonitorArea(int Left, int Top, int Width, int Height, uint Dpi)
{
    /// <summary>The top-left corner that centers a size. A larger size starts at the edge.</summary>
    public (int X, int Y) Center(int width, int height) =>
        (Left + Math.Max(0, (Width - width) / 2), Top + Math.Max(0, (Height - height) / 2));
}
