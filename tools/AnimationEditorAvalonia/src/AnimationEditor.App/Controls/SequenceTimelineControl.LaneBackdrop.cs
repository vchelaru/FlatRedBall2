using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Media;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;

namespace AnimationEditor.App.Controls;

public sealed partial class SequenceTimelineControl
{
    /// <summary>
    /// Draws the lane stripes, the line under each lane and the grid as one operation. False (nothing drawn) while a chip is
    /// dragged over a lane, which paints between them, or when a brush is not a plain color; the caller then draws them
    /// through the drawing context.
    /// </summary>
    private bool TryDrawLaneBackdrop(DrawingContext context, SequenceSave sequence, Size size, double step, double firstTick, TimelineDropPlan drop)
    {
        if (ProbeMode == TimelineProbeMode.LegacyDrawing || drop.Kind == TimelineDropKind.Track) return false;
        if (SkiaColors.Of(Brush("TreeRowStripe")) is not { } stripe || SkiaColors.Of(Brush("LineBrush")) is not { } line) return false;

        var lanes = new LaneBand[sequence.Tracks.Count];
        for (int lane = 0; lane < lanes.Length; lane++)
            lanes[lane] = new LaneBand(Viewport.LaneTop(lane), Viewport.LaneHeightOf(lane), lane % 2 == 1);
        var grid = new List<double>();
        for (double t = firstTick; Viewport.XAt(t) < size.Width; t += step)
        {
            Stats.GridLines++;
            grid.Add(Snap(Viewport.XAt(t)));
        }
        context.Custom(new LaneBackdropOp(lanes, grid.ToArray(), Viewport.ColumnWidth, TimelineViewport.RulerHeight,
            size.Width, size.Height, stripe, line));
        return true;
    }
}
