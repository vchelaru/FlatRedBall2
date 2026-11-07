using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The result of dragging a track to a new lane: where every track ends up and the Z each one has afterwards.
/// </summary>
/// <param name="Order">New lane order: <c>Order[lane]</c> is the old index of the track now in that lane.</param>
/// <param name="Z">The Z of the track in each new lane (same indexing as <see cref="Order"/>).</param>
/// <param name="To">The moved track's new lane.</param>
public sealed record TrackReorderPlan(int[] Order, int[] Z, int To);

/// <summary>
/// Reordering tracks by dragging a lane header. The lanes show the file's track order top to bottom, and the preview
/// draws by Z with equal Z in track order, so the bottom lane is in front.
/// </summary>
/// <remarks>
/// Rule: a move rewrites the tracks' list order, and draw order follows lane order afterwards. When the Zs already
/// ascend down the lanes (the normal state), only the moved track's Z can change: it takes the value nearest to its own
/// that sits between its new neighbours' Zs, so a move among equal Zs touches no Z at all. When the Zs did not ascend
/// before the move (a hand-set Z put a lane out of draw order), no single track fixes it, so every track's Z becomes its
/// new lane index.
/// </remarks>
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
    /// What dropping track <paramref name="from"/> on gap <paramref name="slot"/> does, or null when it changes nothing
    /// (the gap just above or below the track itself). <paramref name="zs"/> are the tracks' Zs in current lane order.
    /// </summary>
    public static TrackReorderPlan? Plan(IReadOnlyList<int> zs, int from, int slot)
    {
        if (slot == from || slot == from + 1) return null;
        int to = slot > from ? slot - 1 : slot;

        var order = Enumerable.Range(0, zs.Count).ToList();
        order.RemoveAt(from);
        order.Insert(to, from);
        var z = order.Select(old => zs[old]).ToArray();

        bool ascended = true;
        for (int i = 1; i < zs.Count; i++) ascended &= zs[i - 1] <= zs[i];
        if (!ascended)
        {
            for (int lane = 0; lane < z.Length; lane++) z[lane] = lane;
        }
        else
        {
            int low = to > 0 ? z[to - 1] : int.MinValue;
            int high = to < z.Length - 1 ? z[to + 1] : int.MaxValue;
            z[to] = Math.Clamp(z[to], low, high);
        }
        return new TrackReorderPlan(order.ToArray(), z, to);
    }
}
