using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// One chain reference as the timeline draws it. <see cref="Played"/> is what plays after the cut rule;
/// <see cref="Full"/> is what its loop count asks for, so the part past <see cref="Played"/> is the hatched cut tail.
/// </summary>
public sealed record TimelineBlock(
    int Track, int Chain, SequenceChainReferenceSave Reference, double Played, double Full, double CycleLength, bool IsMissing)
{
    public double Start => Reference.Start;
    public double PlayedEnd => Start + Played;
    public double FullEnd => Start + Full;
    // Float loop counts can land a chain a rounding error past the next start; that is not a cut anyone can see.
    public bool IsCut => Full > Played + 1e-4;

    /// <summary>Whether dragging the right edge changes the loop count. A missing or empty chain has no cycle to count.</summary>
    public bool CanResize => !IsMissing && CycleLength > 0;
}

public enum TimelineHitPart { Body, Edge }

public readonly record struct TimelineHit(TimelineBlock Block, TimelineHitPart Part);

/// <summary>Where the sequence timeline draws each chain, and what a pointer position on it lands on.</summary>
public static class TimelineLayout
{
    /// <summary>
    /// Seconds a missing chain is drawn. It never plays, so its length means nothing; the file stores none, and this
    /// only keeps the reference visible and grabbable.
    /// </summary>
    public const double MissingLength = 1;

    /// <summary>Starts snap to this many seconds when a chain is dropped or moved.</summary>
    public const double StartStep = 0.05;

    public const double EdgeGrabPixels = 6;

    /// <summary>How close, in pixels, a dragged edge must come to a whole cycle or the next chain to be pulled there.</summary>
    public const double MagnetPixels = 8;

    // Vertical bands inside a lane: a chain's block is a thin label strip over a filmstrip row, and its cut tail sits in a
    // strip below so a tail stays visible and grabbable under the chain that cuts it.
    public const double BodyTop = 3;
    public const double LabelHeight = 14;
    public const double FilmHeight = 25;
    public const double BodyBottom = BodyTop + LabelHeight + FilmHeight;
    public const double FilmTop = BodyTop + LabelHeight;
    public const double TailTop = BodyBottom + 2;
    public const double TailBottom = TailTop + 3;

    /// <summary>
    /// The event row sits under the tail strip, at the bottom of a lane that has events. A lane with none stays
    /// <see cref="TimelineViewport.LaneHeight"/> tall; one with events is <see cref="EventRowHeight"/> taller.
    /// </summary>
    public const double EventRowTop = TimelineViewport.LaneHeight;
    public const double EventRowHeight = 12;

    /// <summary>
    /// Each track's lane height: the base height, plus the event row for a track where <paramref name="hasEvents"/> is
    /// true for at least one of its blocks.
    /// </summary>
    public static double[] LaneHeights(IReadOnlyList<TimelineBlock> blocks, int trackCount, Func<TimelineBlock, bool> hasEvents)
    {
        var heights = new double[trackCount];
        Array.Fill(heights, TimelineViewport.LaneHeight);
        foreach (var block in blocks)
            if (block.Track >= 0 && block.Track < trackCount && heights[block.Track] == TimelineViewport.LaneHeight && hasEvents(block))
                heights[block.Track] += EventRowHeight;
        return heights;
    }

    /// <summary>
    /// The eye toggle in a track header: centered on the top band of the lane (the label strip's row), so it sits in
    /// the same place whatever the lane's height. The name starts at <see cref="HeaderNameLeft"/>, past it.
    /// </summary>
    public const double EyeCenterX = 11;
    public const double EyeCenterY = BodyTop + LabelHeight / 2;
    public const double EyeHitHalf = 7;
    public const double HeaderNameLeft = 24;

    /// <summary>Whether a point in a track's header (<paramref name="yInLane"/> from the lane's top) is on the eye toggle.</summary>
    public static bool IsOverEye(double x, double yInLane) =>
        Math.Abs(x - EyeCenterX) <= EyeHitHalf && Math.Abs(yInLane - EyeCenterY) <= EyeHitHalf;

    /// <summary>The narrowest block that still draws a label: about three characters plus the ellipsis.</summary>
    public const double MinLabelPixels = 36;

    /// <summary>Whether a block <paramref name="blockWidth"/> pixels wide draws its name. Below that the strip stays blank.</summary>
    public static bool ShowsLabel(double blockWidth) => blockWidth >= MinLabelPixels;

    /// <summary>
    /// Every chain on <paramref name="sequence"/>, track by track in playback order.
    /// <paramref name="cycleLength"/> returns a reference's cycle in seconds, or null when its chain is missing.
    /// </summary>
    public static IReadOnlyList<TimelineBlock> Blocks(SequenceSave sequence, Func<SequenceChainReferenceSave, double?> cycleLength)
    {
        var blocks = new List<TimelineBlock>();
        for (int t = 0; t < sequence.Tracks.Count; t++)
        {
            var track = sequence.Tracks[t];
            var ordered = SequenceTimeline.Ordered(track);
            for (int i = 0; i < ordered.Count; i++)
            {
                var reference = ordered[i];
                double? cycle = cycleLength(reference);
                double full = cycle is > 0 ? SequenceTimeline.FullLength(reference.Loops, cycle.Value) : MissingLength;
                double? next = i + 1 < ordered.Count ? ordered[i + 1].Start : null;
                double played = SequenceTimeline.PlayedLength(reference.Start, full, next);
                blocks.Add(new TimelineBlock(t, track.Chains.IndexOf(reference), reference, played, full, cycle ?? 0, cycle is null));
            }
        }
        return blocks;
    }

    /// <summary>
    /// The chain under (<paramref name="x"/>, <paramref name="y"/>), and whether the point is on its resize edge. In the
    /// tail strip a cut tail wins over the body of the chain that cuts it; elsewhere a later chain wins, as it draws on top.
    /// </summary>
    public static TimelineHit? HitTest(TimelineViewport viewport, IReadOnlyList<TimelineBlock> blocks, int trackCount, double x, double y)
    {
        var row = viewport.RowAt(y, trackCount);
        if (row.Kind != TimelineRowKind.Track) return null;
        double inLane = y - viewport.LaneTop(row.Track);
        var lane = blocks.Where(b => b.Track == row.Track).Reverse().ToList();

        // The tail strip, not the event row below it: a cut tail's far end is a resize edge, and the event row is read-only.
        if (inLane >= TailTop && inLane < EventRowTop)
        {
            foreach (var block in lane.Where(b => b.IsCut))
            {
                double end = viewport.XAt(block.FullEnd);
                if (x < viewport.XAt(block.PlayedEnd) || x > end + EdgeGrabPixels) continue;
                return new TimelineHit(block, block.CanResize && x >= end - EdgeGrabPixels ? TimelineHitPart.Edge : TimelineHitPart.Body);
            }
        }

        foreach (var block in lane)
        {
            double left = viewport.XAt(block.Start);
            double right = viewport.XAt(block.PlayedEnd);
            // A narrow chain keeps most of its width for moving.
            double grab = Math.Min(EdgeGrabPixels, (right - left) / 3);
            bool resizable = block.CanResize && !block.IsCut;
            if (resizable && x >= right - grab && x <= right + EdgeGrabPixels)
                return new TimelineHit(block, TimelineHitPart.Edge);
            if (x >= left && x <= right)
                return new TimelineHit(block, TimelineHitPart.Body);
        }
        return null;
    }

    /// <summary>A start time for a drop or move: snapped to <see cref="StartStep"/>, never before 0.</summary>
    public static double SnapStart(double time) => Math.Max(0, Math.Round(time / StartStep) * StartStep);

    /// <summary>The loop count for dragging <paramref name="block"/>'s right edge to <paramref name="edgeTime"/>.</summary>
    public static float EdgeDragLoops(TimelineBlock block, double edgeTime, double pixelsPerSecond, double? nextStart) =>
        SequenceTimeline.LoopsForEdgeDrag(block.Start, edgeTime, block.CycleLength, nextStart, MagnetPixels / pixelsPerSecond);
}
