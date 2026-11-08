using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Draws a <see cref="SequenceTimelineControl"/>'s popups (the hover card and the eye tooltip) on the topmost layer, sitting over
/// the timeline and its <see cref="TimelinePlayheadOverlay"/> in the same grid cell, so nothing the timeline draws can cover them.
/// Declare it after the playhead overlay and attach it with <see cref="Attach"/>; it takes no input.
/// </summary>
public sealed class TimelinePopupOverlay : Control
{
    private SequenceTimelineControl? _timeline;

    public TimelinePopupOverlay() => IsHitTestVisible = false;

    /// <summary>Makes <paramref name="timeline"/> draw its popups through this layer.</summary>
    public void Attach(SequenceTimelineControl timeline)
    {
        _timeline = timeline;
        timeline.PopupLayer = this;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context) => _timeline?.DrawPopupsOn(context, Bounds.Size);
}
