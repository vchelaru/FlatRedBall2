using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Media;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// The frame-event flag glyph (a pole topped by a small right-pointing triangle), defined once for every place that
/// marks an event: the sequence timeline's event row and the frame cells of the chain timeline.
/// </summary>
public static class EventFlag
{
    /// <summary>Height of the triangle in pixels.</summary>
    public const double Height = 7;

    /// <summary>Draws the triangle with its left edge on <paramref name="poleX"/> and its top at <paramref name="flagTop"/>.</summary>
    public static void DrawTriangle(DrawingContext context, double poleX, double flagTop, IBrush fill, IBrush outline)
    {
        var flag = new StreamGeometry();
        using (var g = flag.Open())
        {
            g.BeginFigure(new Point(poleX, flagTop), true);
            g.LineTo(new Point(poleX + EventMarkers.MarkerPixels - 1, flagTop + Height / 2));
            g.LineTo(new Point(poleX, flagTop + Height));
            g.EndFigure(true);
        }
        context.DrawGeometry(fill, new Pen(outline, 1), flag);
    }
}
