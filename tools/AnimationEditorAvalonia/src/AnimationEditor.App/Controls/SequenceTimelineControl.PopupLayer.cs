using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace AnimationEditor.App.Controls;

// Layers, bottom to top:
//   1. the timeline's own Render: lanes, blocks, headers, ruler, drag ghosts and guides
//   2. SelectionLayer (a child): selection outlines and the rubber band
//   3. TimelinePlayheadOverlay (a sibling in the host grid), when attached
//   4. popups: hover card and eye tooltip. A TimelinePopupOverlay sibling declared after the playhead when attached,
//      otherwise PopupSurface, a child above the selection layer.
// A popup is read in front of everything else, so a new tooltip-like card goes in DrawPopupsOn, never in DrawTimeline.
public sealed partial class SequenceTimelineControl
{
    private readonly PopupSurface _popupSurface;

    /// <summary>The layer that draws the popups over the playhead layer, or null when the timeline draws them over its own selection layer.</summary>
    internal TimelinePopupOverlay? PopupLayer { get; set; }

    /// <summary>Draws the popups for a surface <paramref name="size"/> big, as the timeline would.</summary>
    internal void DrawPopupsOn(DrawingContext context, Size size)
    {
        DrawHover(context, size);
        DrawEyeTooltip(context, size);
        DrawBlockTooltip(context, size);
    }

    // Popups change on their own (the card follows the pointer), so only their layer repaints, not the lanes under it.
    private void InvalidatePopups()
    {
        PopupLayer?.InvalidateVisual();
        _popupSurface.InvalidateVisual();
    }

    /// <summary>The child control that draws the popups when no <see cref="TimelinePopupOverlay"/> is attached. It takes no input.</summary>
    private sealed class PopupSurface : Control
    {
        private readonly SequenceTimelineControl _owner;

        public PopupSurface(SequenceTimelineControl owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
        }

        public override void Render(DrawingContext context)
        {
            if (_owner.PopupLayer is null) _owner.DrawPopupsOn(context, Bounds.Size);
        }
    }
}
