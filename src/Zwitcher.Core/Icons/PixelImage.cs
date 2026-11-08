namespace Zwitcher.Core.Icons;

/// <summary>Premultiplied 32-bit BGRA pixels, top row first.</summary>
public sealed record PixelImage(int Width, int Height, byte[] Pixels);
