namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// The timing rules of a <see cref="SequenceSave"/>, shared by the editor and any runtime player so both agree.
/// <list type="bullet">
/// <item>A chain reference lasts <see cref="SequenceChainReferenceSave.Loops"/> cycles, or until the next chain on
/// its track starts, whichever is first. The cut part never plays and is not resumed. The cut is not stored: move the
/// next chain away and the earlier one plays in full again.</item>
/// <item>What fills that length follows the chain's own Loop flag: a looping chain repeats, a non-looping one plays one
/// cycle and holds its last frame (the final frame of <see cref="PlaybackTimeline"/>'s cycle) to the end.</item>
/// <item>Frame events fire when playback enters their frame, in every cycle of a reference's block, not only the first
/// (a walk repeated 3 times fires its footstep 3 times). A block of a chain that does not loop has one cycle; its hold
/// raises nothing. Never from the part of a block the next chain cuts off, and never from a hidden track (turning it on
/// does not replay frames already passed). A ping-pong's turnaround and loop point raise nothing: only a changed frame
/// does. A large time step raises every skipped frame's events in order. A sequence with Loop off raises none during its
/// hold.</item>
/// <item>The next chain is the one with the next later start. On a tie, list order decides: of two chains starting
/// together, the earlier one in <see cref="SequenceTrackSave.Chains"/> plays for zero seconds.</item>
/// <item>A missing chain (cycle length 0) never plays, but it still cuts the chain before it.</item>
/// <item>Outside every chain, the track's sprite is hidden. That includes the time before the first chain starts,
/// on purpose: it is how a sprite appears partway through a sequence. If it showed before its chain, it would always
/// be visible.</item>
/// <item>A sequence with <see cref="SequenceSave.Loop"/> off does not end by going blank: it stops at its length and holds
/// its final pose (<see cref="PoseTime"/>). The hold is the composite of the last instant, so a track whose chains finished
/// earlier stays hidden. With Loop on the sequence repeats from 0.</item>
/// </list>
/// Cycle lengths come from the caller (a chain's frame lengths summed, doubled for ping-pong), so this class needs no
/// chain data.
/// </summary>
public static class SequenceTimeline
{
    /// <summary>The smallest loop count <see cref="LoopsForEdge"/> returns, so a chain never shrinks to nothing.</summary>
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
    /// <param name="localTime">Seconds since the returned reference's start; 0 when none plays. The caller maps it onto the
    /// chain: wrapped when the chain loops, held at the end of the cycle when it does not.</param>
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

    /// <summary>How far before the end of a non-looping sequence its held final pose is sampled, in seconds.</summary>
    public const double HoldEpsilon = 1e-4;

    /// <summary>
    /// The time to sample a sequence's pose at for a playhead at <paramref name="time"/>. A sequence with
    /// <paramref name="loop"/> off stops at its end and holds its final pose: at or past <paramref name="length"/> the pose
    /// is the one at the last instant before the end (<see cref="HoldEpsilon"/> earlier), so a track whose chains already
    /// finished stays hidden and the rest keep their last frame. Otherwise, and for an empty sequence, the time is unchanged.
    /// </summary>
    public static double PoseTime(double time, double length, bool loop) =>
        !loop && length > 0 && time >= length ? length - Math.Min(HoldEpsilon, length) : time;

    /// <summary>The earliest a chain's right edge can sit: <see cref="MinimumLoops"/> cycles after <paramref name="start"/>.</summary>
    public static double MinimumEdge(double start, double cycleLength) => start + MinimumLoops * cycleLength;

    /// <summary>
    /// The loop count that puts a chain's right edge at <paramref name="edgeTime"/>. The count is free (fractions
    /// allowed) and never below <see cref="MinimumLoops"/>. Snapping the edge is <see cref="TimelineSnap"/>'s job; this
    /// only converts. Returns 1 when <paramref name="cycleLength"/> is not positive, since a missing chain has nothing
    /// to measure.
    /// </summary>
    public static float LoopsForEdge(double start, double edgeTime, double cycleLength)
    {
        if (cycleLength <= 0) return 1;
        return (float)Math.Max(MinimumLoops, (edgeTime - start) / cycleLength);
    }
}
