namespace Zwitcher.Core.Screen;

/// <summary>A rectangle in physical screen pixels. Right and Bottom are exclusive, like RECT.</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;
}
