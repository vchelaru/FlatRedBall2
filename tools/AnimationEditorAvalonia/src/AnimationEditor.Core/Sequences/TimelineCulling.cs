using System;

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
        return right + OutlineSlack > TimelineViewport.HeaderWidth && left - OutlineSlack < viewWidth;
    }
}
