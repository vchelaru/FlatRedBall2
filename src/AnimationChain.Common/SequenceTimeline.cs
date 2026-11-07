namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// The timing rules of a <see cref="SequenceSave"/>, shared by the editor and any runtime player so both agree.
/// <list type="bullet">
/// <item>A chain reference plays for <see cref="SequenceChainReferenceSave.Loops"/> cycles, or until the next chain on
/// its track starts, whichever is first. The cut part never plays and is not resumed. The cut is not stored: move the
/// next chain away and the earlier one plays in full again.</item>
/// <item>The next chain is the one with the next later start. On a tie, list order decides: of two chains starting
/// together, the earlier one in <see cref="SequenceTrackSave.Chains"/> plays for zero seconds.</item>
/// <item>A missing chain (cycle length 0) never plays, but it still cuts the chain before it.</item>
/// <item>Outside every chain, the track's sprite is hidden.</item>
/// </list>
/// Cycle lengths come from the caller (a chain's frame lengths summed, doubled for ping-pong), so this class needs no
/// chain data.
/// </summary>
public static class SequenceTimeline
{
    /// <summary>The smallest loop count <see cref="LoopsForEdgeDrag"/> returns, so a chain never shrinks to nothing.</summary>
    public const float MinimumLoops = 0.05f;

    /// <summary>Authored length in seconds: <paramref name="loops"/> cycles of <paramref name="cycleLength"/>. Never negative.</summary>
    public static double FullLength(float loops, double cycleLength) => Math.Max(0, loops) * cycleLength;

    /// <summary>
    /// Seconds a chain actually plays: <paramref name="fullLength"/>, cut where <paramref name="nextStart"/> begins.
    /// Pass null for <paramref name="nextStart"/> when the chain is last on its track.
    /// </summary>
    public static double PlayedLength(double start, double fullLength, double? nextStart) =>
        nextStart is { } next ? Math.Clamp(next - start, 0, fullLength) : fullLength;

    /// <summary>Seconds <paramref name="reference"/> plays on <paramref name="track"/> after the cut rule.</summary>
    public static double PlayedLength(SequenceTrackSave track, SequenceChainReferenceSave reference, double cycleLength) =>
        PlayedLength(reference.Start, FullLength(reference.Loops, cycleLength), NextStart(track, reference));

    /// <summary>The track's references in playback order: by start, ties kept in list order.</summary>
    public static IReadOnlyList<SequenceChainReferenceSave> Ordered(SequenceTrackSave track) =>
        track.Chains.Select((reference, index) => (reference, index))
            .OrderBy(pair => pair.reference.Start).ThenBy(pair => pair.index)
            .Select(pair => pair.reference).ToList();

    /// <summary>
    /// When the chain after <paramref name="reference"/> on <paramref name="track"/> starts, or null when
    /// <paramref name="reference"/> is last or not on the track.
    /// </summary>
    public static double? NextStart(SequenceTrackSave track, SequenceChainReferenceSave reference)
    {
        var ordered = Ordered(track);
        for (int i = 0; i < ordered.Count - 1; i++)
        {
            if (ReferenceEquals(ordered[i], reference)) return ordered[i + 1].Start;
        }
        return null;
    }

    /// <summary>
    /// The reference playing at <paramref name="time"/> on <paramref name="track"/>, or null when the sprite is hidden.
    /// </summary>
    /// <param name="track">The track to sample.</param>
    /// <param name="time">Seconds from the sequence start.</param>
    /// <param name="cycleLength">A reference's cycle length in seconds; 0 for a missing chain.</param>
    /// <param name="localTime">Seconds since the returned reference's start; 0 when none plays. The caller wraps it
    /// into a cycle, since the loop count overrides the chain's own Loop flag.</param>
    public static SequenceChainReferenceSave? ActiveAt(SequenceTrackSave track, double time,
        Func<SequenceChainReferenceSave, double> cycleLength, out double localTime)
    {
        localTime = 0;
        var ordered = Ordered(track);
        // The latest chain that has started is the only candidate: any earlier one is cut at its start.
        for (int i = ordered.Count - 1; i >= 0; i--)
        {
            var reference = ordered[i];
            if (reference.Start > time) continue;

            double? next = i + 1 < ordered.Count ? ordered[i + 1].Start : null;
            double played = PlayedLength(reference.Start, FullLength(reference.Loops, cycleLength(reference)), next);
            if (time >= reference.Start + played) return null;

            localTime = time - reference.Start;
            return reference;
        }
        return null;
    }

    /// <summary>
    /// Seconds until the sequence ends: the end of the last chain to finish playing, across all tracks. 0 when it has no
    /// chains.
    /// </summary>
    /// <param name="sequence">The sequence to measure.</param>
    /// <param name="cycleLength">A reference's cycle length in seconds; 0 for a missing chain.</param>
    public static double Length(SequenceSave sequence, Func<SequenceChainReferenceSave, double> cycleLength)
    {
        double end = 0;
        foreach (var track in sequence.Tracks)
        {
            foreach (var reference in track.Chains)
                end = Math.Max(end, reference.Start + PlayedLength(track, reference, cycleLength(reference)));
        }
        return end;
    }

    /// <summary>
    /// The sequence time to show for an elapsed <paramref name="time"/>: wrapped when <paramref name="loop"/>, otherwise
    /// held at <paramref name="length"/>. Negative time shows 0.
    /// </summary>
    public static double WrapTime(double time, double length, bool loop)
    {
        if (length <= 0) return 0;
        if (time < length) return Math.Max(0, time);
        return loop ? time % length : length;
    }

    /// <summary>
    /// The loop count for dragging a chain's right edge to <paramref name="edgeTime"/>. The count is free (fractions
    /// allowed) and never below <see cref="MinimumLoops"/>. Within <paramref name="magnetSeconds"/> of a whole cycle or of
    /// <paramref name="nextStart"/>, the edge is pulled to the nearer of the two. The edge may still go past
    /// <paramref name="nextStart"/>: that overlap is allowed and cut at playback, so the magnet only guards against
    /// making one by accident. Returns 1 when <paramref name="cycleLength"/> is not positive, since a missing chain has
    /// nothing to measure.
    /// </summary>
    public static float LoopsForEdgeDrag(double start, double edgeTime, double cycleLength, double? nextStart, double magnetSeconds)
    {
        if (cycleLength <= 0) return 1;

        double edge = edgeTime;
        double best = magnetSeconds;
        double whole = Math.Round((edgeTime - start) / cycleLength);
        if (whole >= 1 && Math.Abs(start + whole * cycleLength - edgeTime) is var toWhole && toWhole <= best)
        {
            edge = start + whole * cycleLength;
            best = toWhole;
        }
        if (nextStart is { } next && next > start && Math.Abs(next - edgeTime) < best)
            edge = next;
        return (float)Math.Max(MinimumLoops, (edge - start) / cycleLength);
    }
}
