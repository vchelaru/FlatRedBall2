using Avalonia;
using Avalonia.Media;
using SkiaSharp;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Everything a block draws besides its text, as plain values for <see cref="FilmstripOp"/>: the rounded fill, the
/// filmstrip's backdrop, the outline and the whole-cycle marks. Handing these to one operation replaces about eight
/// Avalonia draw nodes per block. Rectangles are in layout units; colors already carry their opacity.
/// </summary>
internal sealed record BlockPaint(
    Rect Body, double Radius, SKColor Fill,
    Rect? Film, SKColor FilmBackground,
    SKColor Outline, float OutlineThickness,
    SKColor Mark, double[] MarkXs, double MarkTop, double MarkBottom);

/// <summary>Turns solid brushes into Skia colors the way Avalonia's own Skia drawing does, so a block drawn by hand matches one drawn through the drawing context.</summary>
internal static class SkiaColors
{
    /// <summary>The Skia color of <paramref name="brush"/> at an extra <paramref name="opacity"/>, or null when it is not a plain solid brush.</summary>
    public static SKColor? Of(IBrush? brush, double opacity = 1)
    {
        if (brush is not ISolidColorBrush solid) return null;
        var color = solid.Color;
        return new SKColor(color.R, color.G, color.B, (byte)(color.A * solid.Opacity * opacity));
    }
}
