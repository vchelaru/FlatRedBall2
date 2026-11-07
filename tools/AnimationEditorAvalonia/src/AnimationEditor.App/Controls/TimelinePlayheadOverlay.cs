using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Draws a <see cref="SequenceTimelineControl"/>'s playhead on a layer of its own, sitting over the timeline in the same
/// grid cell. While a sequence plays the playhead moves every tick; drawn by the timeline it repainted every block and
/// filmstrip each time, and drawn here only the thin line and its head are repainted. Attach it with
/// <see cref="Attach"/>; it takes no input.
/// </summary>
public sealed class TimelinePlayheadOverlay : Control
{
    private SequenceTimelineControl? _timeline;

    public TimelinePlayheadOverlay() => IsHitTestVisible = false;

    /// <summary>How many times this layer has been drawn, for tests.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>Makes <paramref name="timeline"/> draw its playhead through this layer.</summary>
    public void Attach(SequenceTimelineControl timeline)
    {
        _timeline = timeline;
        timeline.PlayheadLayer = this;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        RenderCount++;
        _timeline?.DrawPlayheadOn(context, Bounds.Size);
    }
}
