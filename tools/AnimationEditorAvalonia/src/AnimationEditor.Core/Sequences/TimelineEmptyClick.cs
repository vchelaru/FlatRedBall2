namespace AnimationEditor.Core.Sequences;

/// <summary>What a press on the timeline's empty lane space means once it is released.</summary>
public static class TimelineEmptyClick
{
    /// <summary>
    /// Whether a press on <paramref name="row"/> is on empty lane space when nothing is hit on it: a lane, or the blank area
    /// under the last lane. The ruler and the dashed new-track row are not.
    /// </summary>
    public static bool IsEmptyLaneSpace(TimelineRow row) => row.Kind is TimelineRowKind.Track or TimelineRowKind.None;

    /// <summary>
    /// The playhead time a release should seek to, or null when it should not. Only a plain click (no Ctrl or Shift, no
    /// rubber band) that was pressed on empty lane space seeks; the time is the one under <paramref name="pressX"/>,
    /// clamped to the sequence like a ruler scrub, with no snapping.
    /// </summary>
    public static double? PlayheadTime(TimelineViewport viewport, double sequenceLength,
        bool pressedOnEmptyLane, bool modified, bool dragged, double pressX) =>
        pressedOnEmptyLane && !modified && !dragged ? viewport.ScrubTime(pressX, sequenceLength) : null;
}
