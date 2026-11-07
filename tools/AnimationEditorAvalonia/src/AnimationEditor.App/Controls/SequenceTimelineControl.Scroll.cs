using AnimationEditor.Core.Sequences;

namespace AnimationEditor.App.Controls;

// What the timeline's scrollbars read and drive. The bars live beside the control, not over it, so Bounds is always the
// visible lanes and hit-testing needs no allowance for them (see TimelineScrollBinder).
public sealed partial class SequenceTimelineControl
{
    /// <summary>Raised when the zoom, either scroll offset, the lane heights or the control's size change.</summary>
    public event System.Action? ViewChanged;

    /// <summary>The horizontal scrollbar's range, in seconds.</summary>
    public TimelineScrollRange HorizontalScroll =>
        TimelineScrollBars.Horizontal(Viewport.ScrollSeconds, Viewport.PixelsPerSecond, Bounds.Width, SequenceLength);

    /// <summary>The vertical scrollbar's range, in pixels.</summary>
    public TimelineScrollRange VerticalScroll =>
        TimelineScrollBars.Vertical(Viewport.ScrollY, Viewport.ContentHeight(TrackCount), Bounds.Height);

    /// <summary>Scrolls so the lanes start at <paramref name="seconds"/>.</summary>
    public void ScrollToSeconds(double seconds)
    {
        ClearHover();
        Viewport.SetScrollSeconds(seconds);
        InvalidateVisual();
    }

    /// <summary>Scrolls the lanes up by <paramref name="y"/> pixels, kept within the content.</summary>
    public void ScrollToY(double y)
    {
        ClearHover();
        Viewport.SetScrollY(y, TrackCount, Bounds.Height);
        InvalidateArrange();
        InvalidateVisual();
    }
}
