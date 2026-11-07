namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// What a snap target is. Declared in priority order: when two targets would crowd each other, the earlier kind stays.
/// </summary>
public enum SnapKind
{
    /// <summary>The start or end of another chain on any track.</summary>
    ChainEdge,

    /// <summary>The playhead.</summary>
    Playhead,

    /// <summary>Time 0.</summary>
    Zero,

    /// <summary>A whole-cycle mark of the chain being resized.</summary>
    OwnCycle,

    /// <summary>A frame boundary inside a cycle of the chain being resized.</summary>
    FrameBoundary,
}

/// <summary>A time a dragged edge can snap to.</summary>
/// <param name="Time">Seconds from the sequence start.</param>
/// <param name="Kind">What the target is; also its priority.</param>
/// <param name="Track">The owning chain's track for a <see cref="SnapKind.ChainEdge"/>, -1 otherwise.</param>
/// <param name="Chain">The owning chain's index on that track, -1 otherwise.</param>
public readonly record struct SnapTarget(double Time, SnapKind Kind, int Track = -1, int Chain = -1);

/// <summary>The outcome of a snap: the time to use, and the target it was pulled to (null when it moved freely).</summary>
/// <param name="Time">The time to use.</param>
/// <param name="Target">The target the time was pulled to, or null.</param>
public readonly record struct SnapResult(double Time, SnapTarget? Target)
{
    /// <summary>Whether <see cref="Target"/> pulled the time.</summary>
    public bool IsSnapped => Target is not null;
}

/// <summary>
/// The one snapping mechanism of the sequence timeline, for resizing a chain's right edge, moving a chain and dropping
/// a chip. Pure time math, so a runtime or another tool can reuse it.
/// <para>
/// The snap distance is in pixels, so it follows the zoom. Before snapping, targets are thinned: any two closer than
/// twice the snap distance keep only the higher-priority one (<see cref="SnapKind"/> order; the earlier time on a tie),
/// so snap points never crowd each other. The dragged time then snaps to the nearest surviving target within the snap
/// distance. Nothing snaps when <c>bypass</c> is set.
/// </para>
/// </summary>
public static class TimelineSnap
{
    /// <summary>How far, in pixels, a dragged edge is pulled to a target.</summary>
    public const double SnapPixels = 8;

    /// <summary>The step a moved chain's start rounds to when no target is near.</summary>
    public const double GridStep = 0.05;

    /// <summary>The grid only applies while one <see cref="GridStep"/> is at most this many pixels wide. Zoomed in further, the start is free between targets.</summary>
    public const double GridMaxPixels = 24;

    /// <summary>
    /// <paramref name="targets"/> without the ones that crowd a higher-priority target: of two closer than twice
    /// <paramref name="snapPixels"/> at <paramref name="pixelsPerSecond"/>, the higher <see cref="SnapKind"/> stays, and
    /// of equal kinds the earlier time. Sorted by time.
    /// </summary>
    public static IReadOnlyList<SnapTarget> Thin(IReadOnlyList<SnapTarget> targets, double pixelsPerSecond, double snapPixels = SnapPixels)
    {
        double gap = 2 * snapPixels / pixelsPerSecond;
        var ordered = targets.Select((target, index) => (target, index))
            .OrderBy(pair => pair.target.Kind).ThenBy(pair => pair.target.Time).ThenBy(pair => pair.index)
            .Select(pair => pair.target);

        var kept = new List<SnapTarget>();
        foreach (var target in ordered)
        {
            int at = IndexAfter(kept, target.Time);
            if (at > 0 && target.Time - kept[at - 1].Time < gap) continue;
            if (at < kept.Count && kept[at].Time - target.Time < gap) continue;
            kept.Insert(at, target);
        }
        return kept;
    }

    /// <summary>
    /// Snaps a dragged right edge at <paramref name="time"/>. Targets before <paramref name="minTime"/> are ignored and
    /// the result never goes below it, so the edge always leaves a legal loop count.
    /// </summary>
    public static SnapResult Edge(double time, IReadOnlyList<SnapTarget> targets, double pixelsPerSecond, bool bypass, double minTime = double.NegativeInfinity)
    {
        if (!bypass)
        {
            var legal = minTime == double.NegativeInfinity ? targets : targets.Where(t => t.Time >= minTime).ToList();
            if (Nearest(time, Thin(legal, pixelsPerSecond), pixelsPerSecond, double.NegativeInfinity, 0) is { } hit)
                return new SnapResult(hit.Time, hit);
        }
        return new SnapResult(Math.Max(time, minTime), null);
    }

    /// <summary>
    /// Snaps a chain being moved to <paramref name="start"/>. Either end of the chain, its start or the point
    /// <paramref name="length"/> seconds later, snaps to a target; the nearer one wins, the start on a tie. Time is
    /// returned as the chain's start, never before 0. With nothing in reach, the start rounds to <see cref="GridStep"/>
    /// at low zoom and is free at high zoom.
    /// </summary>
    public static SnapResult Move(double start, double length, IReadOnlyList<SnapTarget> targets, double pixelsPerSecond, bool bypass)
    {
        if (bypass) return new SnapResult(Math.Max(0, start), null);

        var thinned = Thin(targets, pixelsPerSecond);
        var byStart = Nearest(start, thinned, pixelsPerSecond, 0, 0);
        var byEnd = Nearest(start + length, thinned, pixelsPerSecond, 0, length);
        double startDistance = byStart is { } s ? Math.Abs(s.Time - start) : double.PositiveInfinity;
        double endDistance = byEnd is { } e ? Math.Abs(e.Time - (start + length)) : double.PositiveInfinity;
        if (byStart is { } startHit && startDistance <= endDistance) return new SnapResult(startHit.Time, startHit);
        if (byEnd is { } endHit) return new SnapResult(endHit.Time - length, endHit);

        double free = Math.Max(0, start);
        return new SnapResult(GridIsUsable(pixelsPerSecond) ? Math.Max(0, Math.Round(start / GridStep) * GridStep) : free, null);
    }

    /// <summary>Whether the <see cref="GridStep"/> grid applies at this zoom.</summary>
    public static bool GridIsUsable(double pixelsPerSecond) => GridStep * pixelsPerSecond <= GridMaxPixels;

    /// <summary>
    /// The whole-cycle marks of a chain starting at <paramref name="start"/> near <paramref name="time"/>, as targets.
    /// When cycles are narrower than twice the snap distance, only every Nth mark is offered (N counted from the chain's
    /// start, so the pattern does not shift as the pointer moves).
    /// </summary>
    public static IEnumerable<SnapTarget> CycleMarks(double start, double cycleLength, double time, double pixelsPerSecond, double snapPixels = SnapPixels)
    {
        if (cycleLength <= 0) yield break;
        double cyclePixels = cycleLength * pixelsPerSecond;
        long stride = Math.Max(1, (long)Math.Ceiling(2 * snapPixels / cyclePixels));
        long nearest = (long)Math.Round((time - start) / cycleLength / stride);
        for (long k = Math.Max(1, nearest - 1); k <= nearest + 1; k++)
            yield return new SnapTarget(start + k * stride * cycleLength, SnapKind.OwnCycle);
    }

    /// <summary>
    /// Frame boundaries of a chain starting at <paramref name="start"/> in the cycles near <paramref name="time"/>, as
    /// targets. <paramref name="offsets"/> are the boundaries inside one cycle, in seconds from the cycle start. Empty
    /// when a cycle is narrower than twice the snap distance, where the cycle marks already cover it.
    /// </summary>
    public static IEnumerable<SnapTarget> FrameMarks(double start, double cycleLength, IReadOnlyList<double> offsets, double time,
        double pixelsPerSecond, double snapPixels = SnapPixels)
    {
        if (cycleLength <= 0 || cycleLength * pixelsPerSecond < 2 * snapPixels || offsets.Count == 0) yield break;
        long cycle = (long)Math.Floor((time - start) / cycleLength);
        for (long k = Math.Max(0, cycle - 1); k <= cycle + 1; k++)
            foreach (double offset in offsets)
                yield return new SnapTarget(start + k * cycleLength + offset, SnapKind.FrameBoundary);
    }

    // The nearest target to edgeTime within the snap distance; ties go to the higher priority, then the earlier time.
    // A target is skipped when the chain's start it would give (target - offset) is before minStart.
    private static SnapTarget? Nearest(double edgeTime, IReadOnlyList<SnapTarget> thinned, double pixelsPerSecond, double minStart, double offset)
    {
        double reach = SnapPixels / pixelsPerSecond;
        SnapTarget? best = null;
        double bestDistance = double.PositiveInfinity;
        foreach (var target in thinned)
        {
            if (target.Time - offset < minStart) continue;
            double distance = Math.Abs(target.Time - edgeTime);
            if (distance > reach) continue;
            if (distance < bestDistance || (distance == bestDistance && best is { } b && target.Kind < b.Kind))
            {
                best = target;
                bestDistance = distance;
            }
        }
        return best;
    }

    private static int IndexAfter(List<SnapTarget> sorted, double time)
    {
        int low = 0, high = sorted.Count;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (sorted[mid].Time <= time) low = mid + 1;
            else high = mid;
        }
        return low;
    }
}
