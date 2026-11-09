using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>Where a dragged library chip would land.</summary>
public enum TimelineDropKind
{
    /// <summary>Not over a droppable row.</summary>
    None,

    /// <summary>On an existing track's lane.</summary>
    Track,

    /// <summary>On the dashed row above the first lane that makes a new track in front.</summary>
    NewTrack,
}

/// <summary>
/// What dropping a library chip at a point on the timeline would do, and the ghost block that previews it. The
/// timeline draws the ghost from this and the drop itself reads <see cref="Kind"/>, <see cref="Row"/> and
/// <see cref="Start"/> from it, so the preview and the result cannot disagree.
/// </summary>
/// <param name="Kind">The target.</param>
/// <param name="Row">The lane index: the track for <see cref="TimelineDropKind.Track"/>, <see cref="TimelineViewport.NewTrackLane"/> for <see cref="TimelineDropKind.NewTrack"/>.</param>
/// <param name="Start">The chain's start in seconds, snapped.</param>
/// <param name="Length">The ghost's length in seconds.</param>
/// <param name="Left">The ghost's left edge in pixels.</param>
/// <param name="Width">The ghost's width in pixels.</param>
/// <param name="Snapped">The target the start or end snapped to, or null when it did not.</param>
public readonly record struct TimelineDropPlan(TimelineDropKind Kind, int Row, double Start, double Length, double Left, double Width,
    SnapTarget? Snapped = null)
{
    public static readonly TimelineDropPlan None = default;

    /// <summary>The start as shown beside the ghost, such as <c>0.85 s</c>.</summary>
    public string TimeLabel => FormatTime(Start);

    public static string FormatTime(double seconds) =>
        seconds.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " s";

    /// <summary>
    /// The plan for a chip released at (<paramref name="x"/>, <paramref name="y"/>) in the timeline's own coordinates.
    /// <paramref name="cycleLength"/> is the chip's chain length in seconds, 0 when it is missing. Either end of the ghost
    /// snaps to <paramref name="targets"/> (see <see cref="TimelineSnapTargets.Others"/>) unless <paramref name="bypass"/>.
    /// </summary>
    public static TimelineDropPlan For(TimelineViewport viewport, double viewWidth, int trackCount, double x, double y, double cycleLength,
        IReadOnlyList<SnapTarget>? targets = null, bool bypass = false)
    {
        if (x < viewport.ColumnWidth || x > viewWidth) return None;
        var row = viewport.RowAt(y, trackCount);
        if (row.Kind is not (TimelineRowKind.Track or TimelineRowKind.NewTrack)) return None;

        double length = cycleLength > 0 ? cycleLength : TimelineLayout.MissingLength;
        var snap = TimelineSnap.Move(viewport.TimeAt(x), length, targets ?? Array.Empty<SnapTarget>(), viewport.PixelsPerSecond, bypass);
        double start = snap.Time;
        return new TimelineDropPlan(
            row.Kind == TimelineRowKind.Track ? TimelineDropKind.Track : TimelineDropKind.NewTrack,
            row.Kind == TimelineRowKind.Track ? row.Track : TimelineViewport.NewTrackLane,
            start, length, viewport.XAt(start), length * viewport.PixelsPerSecond, snap.Target);
    }

    /// <summary>
    /// The plan for an existing chain being dragged with the pointer at height <paramref name="y"/>. Only the new-track row
    /// is a drop here (a lane is the normal move, which the drag handles itself), so any other row is <see cref="None"/>.
    /// The chain's already-snapped <paramref name="start"/> and its <paramref name="length"/> in seconds place the ghost,
    /// as a chip's do; <paramref name="snapped"/> is the target the drag snapped to.
    /// </summary>
    public static TimelineDropPlan ForMove(TimelineViewport viewport, int trackCount, double y, double start, double length, SnapTarget? snapped = null)
    {
        if (viewport.RowAt(y, trackCount).Kind != TimelineRowKind.NewTrack) return None;
        return new TimelineDropPlan(TimelineDropKind.NewTrack, TimelineViewport.NewTrackLane, start, length, viewport.XAt(start),
            length * viewport.PixelsPerSecond, snapped);
    }
}
