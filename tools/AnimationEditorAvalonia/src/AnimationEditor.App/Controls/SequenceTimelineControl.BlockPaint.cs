using Avalonia;
using AnimationEditor.Core.Sequences;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace AnimationEditor.App.Controls;

public sealed partial class SequenceTimelineControl
{
    private const double BlockCornerRadius = 3;

    /// <summary>
    /// What <paramref name="block"/> draws besides its text, as values for one Skia operation; null when a brush it needs is
    /// not a plain solid color, and the block draws through the drawing context instead.
    /// </summary>
    private BlockPaint? BlockPaintFor(TimelineBlock block, bool selected, Rect body, Rect film, Color color, double left, double pps)
    {
        if (SkiaColors.Of(new SolidColorBrush(color, 0.35)) is not { } fill
            || SkiaColors.Of(Brush("BgCanvas")) is not { } background
            || SkiaColors.Of(selected ? Brush("Ink") : new SolidColorBrush(color)) is not { } outline
            || SkiaColors.Of(new SolidColorBrush(color, 0.8)) is not { } mark)
            return null;

        var marks = new List<double>();
        if (block.CycleLength > 0)
            for (int k = 1; k * block.CycleLength < block.Played - 1e-9; k++)
                marks.Add(Snap(left + k * block.CycleLength * pps));
        return new BlockPaint(body, BlockCornerRadius, fill, film, background, outline, selected ? 2 : 1,
            mark, marks.ToArray(), film.Top, body.Bottom - 2);
    }
}
