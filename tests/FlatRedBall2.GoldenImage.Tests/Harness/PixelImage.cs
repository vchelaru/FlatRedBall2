using System;
using Microsoft.Xna.Framework;

namespace FlatRedBall2.GoldenImage;

/// <summary>
/// A decoded RGBA image, row-major from the top-left. Plain data with no GL dependency, so the
/// comparison and PNG code can be tested on a machine that cannot create a graphics device.
/// </summary>
/// <remarks>
/// Pixels are exactly what a render target read-back returns: premultiplied alpha. They are
/// written to and read from PNG without conversion, so semi-transparent pixels look darker in an
/// image viewer than they composite on screen. Render onto an opaque background when a golden
/// needs to be reviewed by eye.
/// </remarks>
public sealed class PixelImage
{
    public PixelImage(int width, int height, Color[] pixels)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (pixels.Length != width * height)
            throw new ArgumentException($"Expected {width * height} pixels for {width}x{height}, got {pixels.Length}.", nameof(pixels));

        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public Color[] Pixels { get; }

    public Color this[int x, int y] => Pixels[y * Width + x];
}
