using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>What of the sequence timeline can land on screen, so drawing can skip the rest.</summary>
public static class TimelineCulling
{
    // The selected outline is 2 px wide, half of it outside the body.
    private const double OutlineSlack = 2;

    /// <summary>
    /// Whether anything of <paramref name="block"/>'s drawing (body, outline, hatched cut tail) can fall inside the lanes of a
    /// view <paramref name="viewWidth"/> by <paramref name="viewHeight"/> pixels, given the viewport's zoom and scroll.
    /// </summary>
    public static bool IsBlockVisible(TimelineViewport viewport, TimelineBlock block, double viewWidth, double viewHeight)
    {
        double top = viewport.LaneTop(block.Track);
        if (top + viewport.LaneHeightOf(block.Track) <= TimelineViewport.RulerHeight || top >= viewHeight) return false;
        double left = viewport.XAt(block.Start);
        double right = left + Math.Max(1, Math.Max(block.Played, block.Full) * viewport.PixelsPerSecond);
        return right + OutlineSlack > viewport.ColumnWidth && left - OutlineSlack < viewWidth;
    }

    /// <summary>
    /// The whole cycles (0 = the first) of a block or clip that starts at <paramref name="start"/>, plays
    /// <paramref name="played"/> seconds and repeats every <paramref name="cycleLength"/>, that fall inside a view
    /// <paramref name="viewWidth"/> pixels wide: what a long block draws per cycle is bounded by the view, not by its length.
    /// Cycles narrower than a pixel are thinned to one or two per pixel. None when there is no cycle.
    /// </summary>
    public static IEnumerable<int> VisibleCycles(TimelineViewport viewport, double start, double played, double cycleLength, double viewWidth)
    {
        if (cycleLength <= 0) yield break;
        double cyclePixels = cycleLength * viewport.PixelsPerSecond;
        long step = Math.Max(1, (long)(1 / cyclePixels));
        // On a multiple of the step, so the same cycles are picked wherever the view is scrolled.
        long first = (long)Math.Max(0, (viewport.ColumnWidth - viewport.XAt(start)) / cyclePixels) / step * step;
        for (long cycle = first; cycle <= int.MaxValue && cycle * cycleLength < played - 1e-9 && viewport.XAt(start + cycle * cycleLength) <= viewWidth; cycle += step)
            yield return (int)cycle;
    }
}
