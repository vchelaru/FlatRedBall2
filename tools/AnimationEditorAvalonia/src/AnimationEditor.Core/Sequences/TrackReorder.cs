namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Reordering tracks by dragging a lane header. The lanes show the file's track order top to bottom and the preview
/// draws them in reverse, so the top lane is in front and the bottom lane is at the back, like layers in an image editor. A move is only a move in the
/// list: no other property of any track changes.
/// </summary>
public static class TrackReorder
{
    /// <summary>
    /// The gap a pointer at <paramref name="y"/> points at, as a slot from 0 (above the first lane) to
    /// <paramref name="trackCount"/> (below the last). A lane's upper half points at the gap above it, its lower half
    /// at the gap below, whatever each lane's height (a lane with an event row is taller).
    /// </summary>
    public static int SlotAt(TimelineViewport viewport, int trackCount, double y)
    {
        for (int lane = 0; lane < trackCount; lane++)
            if (y < viewport.LaneTop(lane) + viewport.LaneHeightOf(lane) / 2) return lane;
        return trackCount;
    }

    /// <summary>The y of the line that marks <paramref name="slot"/>: the top edge of the lane below it, or the bottom edge of the last lane.</summary>
    public static double SlotY(TimelineViewport viewport, int slot) => viewport.LaneTop(slot);

    /// <summary>
    /// The gap that moves track <paramref name="from"/> one lane up (<paramref name="delta"/> -1) or down (+1), for Alt+Up and
    /// Alt+Down, or null at the top or bottom lane. The same slot a header drag would drop on.
    /// </summary>
    public static int? StepSlot(int from, int trackCount, int delta) =>
        delta < 0 ? (from > 0 ? from - 1 : null) : (from < trackCount - 1 ? from + 2 : null);

    /// <summary>
    /// The lane track <paramref name="from"/> ends up in when dropped on gap <paramref name="slot"/>, or null when the drop
    /// changes nothing (the gap just above or below the track itself).
    /// </summary>
    public static int? TargetLane(int from, int slot)
    {
        if (slot == from || slot == from + 1) return null;
        return slot > from ? slot - 1 : slot;
    }
}
