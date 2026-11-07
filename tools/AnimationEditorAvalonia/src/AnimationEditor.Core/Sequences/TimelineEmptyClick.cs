namespace AnimationEditor.Core.Sequences;

/// <summary>What a press on the timeline's empty lane space means once it is released.</summary>
public static class TimelineEmptyClick
{
    /// <summary>
    /// The playhead time a release should seek to, or null when it should not. Only a plain click (no Ctrl or Shift, no
    /// rubber band) that was pressed on empty lane space seeks; the time is the one under <paramref name="pressX"/>,
    /// clamped to the sequence like a ruler scrub, with no snapping.
    /// </summary>
    public static double? PlayheadTime(TimelineViewport viewport, double sequenceLength,
        bool pressedOnEmptyLane, bool modified, bool dragged, double pressX) =>
        pressedOnEmptyLane && !modified && !dragged ? viewport.ScrubTime(pressX, sequenceLength) : null;
}
