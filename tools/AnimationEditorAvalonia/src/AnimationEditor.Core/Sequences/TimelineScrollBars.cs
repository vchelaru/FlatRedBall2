using System;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// A scrollbar's range for one timeline axis: 0 to <see cref="Maximum"/>, the thumb at <see cref="Value"/>, as long as
/// <see cref="ViewportSize"/> (same units as the other two: seconds across, pixels down).
/// </summary>
public readonly record struct TimelineScrollRange(double Maximum, double Value, double ViewportSize)
{
    /// <summary>False when everything already fits, so the bar has nothing to do.</summary>
    public bool CanScroll => Maximum > 0;

    /// <summary>The thumb's share of the track, 0 to 1.</summary>
    public double ThumbFraction => ViewportSize + Maximum > 0 ? ViewportSize / (ViewportSize + Maximum) : 1;
}

/// <summary>
/// Maps the timeline's scroll offsets, content extents and view size to scrollbar ranges. The bars sit outside the
/// control, so the control's own size is the view and nothing here knows a bar's thickness.
/// </summary>
public static class TimelineScrollBars
{
    /// <summary>Room to scroll after the sequence ends, so the last chain can be dragged or a chip dropped past it.</summary>
    public const double EndMarginPixels = 120;

    /// <summary>
    /// The horizontal bar, in seconds. The extent is the sequence length plus <see cref="EndMarginPixels"/> at the
    /// current zoom; the view is the lanes' width. An offset beyond the extent (the wheel can scroll past the end) is
    /// kept on the track.
    /// </summary>
    public static TimelineScrollRange Horizontal(double scrollSeconds, double pixelsPerSecond, double controlWidth, double sequenceLength)
    {
        double view = Math.Max(0, controlWidth - TimelineViewport.HeaderWidth) / pixelsPerSecond;
        double content = Math.Max(0, sequenceLength) + EndMarginPixels / pixelsPerSecond;
        double value = Math.Max(0, scrollSeconds);
        return new TimelineScrollRange(Math.Max(Math.Max(0, content - view), value), value, view);
    }

    /// <summary>
    /// The vertical bar, in pixels, over the lanes (below the ruler). <paramref name="contentHeight"/> is
    /// <see cref="TimelineViewport.ContentHeight"/>, ruler included, and the maximum matches the viewport's own
    /// vertical scroll limit.
    /// </summary>
    public static TimelineScrollRange Vertical(double scrollY, double contentHeight, double controlHeight)
    {
        double max = Math.Max(0, contentHeight - controlHeight);
        return new TimelineScrollRange(max, Math.Clamp(scrollY, 0, max), Math.Max(0, controlHeight - TimelineViewport.RulerHeight));
    }
}
