using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// How a dragged multi-selection moves. Every chain gets the same time shift and the same lane shift, so the
/// selection keeps its shape. Neither shift may push a chain off the timeline, so both are clamped: the time shift
/// until the earliest chain reaches 0, the lane shift until the first or last occupied lane reaches the edge. A lane
/// move is clamped, never dropped, so dragging a selection sideways and down still slides it as far as it can go.
/// </summary>
public static class TimelineMultiMove
{
    /// <summary>Where a dragged chain was when the drag began.</summary>
    public readonly record struct Origin(int Track, double Start);

    public readonly record struct Result(double TimeDelta, int TrackDelta);

    /// <param name="origins">The dragged chains where they started.</param>
    /// <param name="requestedTime">Seconds the primary chain moved (already snapped).</param>
    /// <param name="requestedTracks">Lanes the pointer is from the primary chain's starting lane.</param>
    /// <param name="trackCount">How many lanes the sequence has.</param>
    public static Result Plan(IReadOnlyList<Origin> origins, double requestedTime, int requestedTracks, int trackCount)
    {
        if (origins.Count == 0) return new Result(0, 0);
        double time = Math.Max(requestedTime, -origins.Min(origin => origin.Start));
        int lowest = origins.Min(origin => origin.Track);
        int highest = origins.Max(origin => origin.Track);
        int tracks = Math.Clamp(requestedTracks, -lowest, Math.Max(-lowest, trackCount - 1 - highest));
        return new Result(time, tracks);
    }
}
