using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>The loop count an edge drag asks for, and the snap that produced the edge time.</summary>
public readonly record struct EdgeDragResult(float Loops, SnapResult Snap);

/// <summary>
/// Builds the targets <see cref="TimelineSnap"/> snaps to from what the timeline shows, and applies it to a resize.
/// A drag takes its <see cref="Others"/> once, when it starts, so a chain that moves with the pointer never snaps to
/// itself or to a neighbour's end that it is currently cutting.
/// </summary>
public static class TimelineSnapTargets
{
    /// <summary>
    /// The targets every gesture shares: the start, played end and full end of each chain except
    /// <paramref name="dragged"/>, the <paramref name="playhead"/>, and time 0. A chain that <paramref name="dragged"/>
    /// cuts offers no played end, since that end is only where the dragged chain happens to start.
    /// </summary>
    public static IReadOnlyList<SnapTarget> Others(IReadOnlyList<TimelineBlock> blocks, SequenceChainReferenceSave? dragged, double playhead)
    {
        double? draggedStart = null;
        foreach (var block in blocks)
            if (ReferenceEquals(block.Reference, dragged)) draggedStart = block.Start;

        var targets = new List<SnapTarget>(blocks.Count * 3 + 2);
        foreach (var block in blocks)
        {
            if (ReferenceEquals(block.Reference, dragged)) continue;
            targets.Add(new SnapTarget(block.Start, SnapKind.ChainEdge, block.Track, block.Chain));
            bool cutByDragged = block.IsCut && draggedStart is { } cutAt && Math.Abs(block.PlayedEnd - cutAt) < 1e-9;
            if (!cutByDragged)
                targets.Add(new SnapTarget(block.PlayedEnd, SnapKind.ChainEdge, block.Track, block.Chain));
            if (block.IsCut)
                targets.Add(new SnapTarget(block.FullEnd, SnapKind.ChainEdge, block.Track, block.Chain));
        }
        targets.Add(new SnapTarget(playhead, SnapKind.Playhead));
        targets.Add(new SnapTarget(0, SnapKind.Zero));
        return targets;
    }

    /// <summary>The frame boundaries inside one cycle of <paramref name="chain"/>, in seconds from the cycle start. The cycle's start and end are not boundaries.</summary>
    public static IReadOnlyList<double> FrameBoundaries(AnimationChainSave chain)
    {
        var offsets = new List<double>();
        int count = chain.Frames.Count;
        int visits = PlaybackTimeline.VisitCount(count, chain.PingPong);
        double time = 0;
        int previous = -1;
        for (int v = 0; v < visits; v++)
        {
            int frame = PlaybackTimeline.FrameOfVisit(v, count, chain.PingPong, chain.Reverse);
            // A ping-pong shows its end frames twice in a row; there is no boundary between the two showings.
            if (v > 0 && frame != previous) offsets.Add(time);
            time += PlaybackController.FrameLength(chain.Frames[frame]);
            previous = frame;
        }
        return offsets;
    }

    /// <summary>
    /// The right-edge drag of <paramref name="block"/> to <paramref name="edgeTime"/>: snaps to <paramref name="others"/>,
    /// the block's own whole-cycle marks, and its frame boundaries (<paramref name="frameBoundaries"/>, from
    /// <see cref="FrameBoundaries"/>, or null), then converts to a loop count. The edge never goes below the minimum loops.
    /// </summary>
    public static EdgeDragResult ResizeEdge(TimelineBlock block, double edgeTime, IReadOnlyList<SnapTarget> others,
        IReadOnlyList<double>? frameBoundaries, double pixelsPerSecond, bool bypass)
    {
        var targets = new List<SnapTarget>(others);
        targets.AddRange(TimelineSnap.CycleMarks(block.Start, block.CycleLength, edgeTime, pixelsPerSecond));
        if (frameBoundaries is not null)
            targets.AddRange(TimelineSnap.FrameMarks(block.Start, block.CycleLength, frameBoundaries, edgeTime, pixelsPerSecond));

        double minEdge = SequenceTimeline.MinimumEdge(block.Start, block.CycleLength);
        var snap = TimelineSnap.Edge(edgeTime, targets, pixelsPerSecond, bypass, minEdge);
        return new EdgeDragResult(SequenceTimeline.LoopsForEdge(block.Start, snap.Time, block.CycleLength), snap);
    }
}
