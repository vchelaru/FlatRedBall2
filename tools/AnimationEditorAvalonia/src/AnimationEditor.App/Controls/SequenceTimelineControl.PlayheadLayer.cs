using Avalonia;
using Avalonia.Media;

namespace AnimationEditor.App.Controls;

// The playhead can be drawn by a layer of its own (TimelinePlayheadOverlay) so that moving it does not repaint the lanes.
public sealed partial class SequenceTimelineControl
{
    /// <summary>The layer that draws the playhead, or null when the timeline draws it itself.</summary>
    internal TimelinePlayheadOverlay? PlayheadLayer { get; set; }

    /// <summary>Draws the playhead line and head for a surface <paramref name="size"/> big, as the timeline would.</summary>
    internal void DrawPlayheadOn(DrawingContext context, Size size) => DrawPlayhead(context, size);

    // The layer repaints on anything that moves or recolors the playhead besides its own time.
    private void RefreshPlayheadLayer() => PlayheadLayer?.InvalidateVisual();
}
