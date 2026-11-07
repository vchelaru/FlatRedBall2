namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// The timing rules of a <see cref="SequenceSave"/>, shared by the editor and any runtime player so they agree.
/// <list type="bullet">
/// <item>A chain reference plays for <c>Loops</c> cycles (its full length), or until the next chain on its track starts,
/// whichever is first. The cut part never plays and is not resumed.</item>
/// <item>The next chain is the one with the next later start; on a tie, the later one in the track's list. So of two
/// chains starting together, the earlier one in the list plays for zero seconds.</item>
/// <item>Outside every chain the track's sprite is hidden.</item>
/// </list>
/// Cycle lengths come from the caller (a chain's frame lengths, doubled for ping-pong), so this class needs no chain data.
/// </summary>
public static class SequenceTimeline
{
    /// <summary>The shortest loop count an edge drag produces, so a chain never shrinks to nothing.</summary>
    public const float MinimumLoops = 0.05f;

    /// <summary>Authored length: <paramref name="loops"/> cycles of <paramref name="cycleLength"/> seconds.</summary>
    public static double FullLength(float loops, double cycleLength) => Math.Max(0, loops) * cycleLength;

    /// <summary>Seconds the chain actually plays: its full length, cut where <paramref name="nextStart"/> begins.</summary>
    public static double PlayedLength(double start, double fullLength, double? nextStart) =>
        nextStart is { } next ? Math.Clamp(next - start, 0, fullLength) : fullLength;

    /// <summary>The track's references in playback order: by start, ties kept in list order.</summary>
    public static IReadOnlyList<SequenceChainReferenceSave> Ordered(SequenceTrackSave track) =>
        track.Chains.Select((reference, index) => (reference, index))
            .OrderBy(pair => pair.reference.Start).ThenBy(pair => pair.index)
            .Select(pair => pair.reference).ToList();

    /// <summary>When the chain after <paramref name="reference"/> on <paramref name="track"/> starts, or null when it is last.</summary>
    public static double? NextStart(SequenceTrackSave track, SequenceChainReferenceSave reference)
    {
        var ordered = Ordered(track);
        int index = IndexOfReference(ordered, reference);
        return index >= 0 && index + 1 < ordered.Count ? ordered[index + 1].Start : null;
    }

    /// <summary>Seconds <paramref name="reference"/> plays on <paramref name="track"/>, after the cut rule.</summary>
    public static double PlayedLength(SequenceTrackSave track, SequenceChainReferenceSave reference, double cycleLength) =>
        PlayedLength(reference.Start, FullLength(reference.Loops, cycleLength), NextStart(track, reference));

    /// <summary>
    /// The reference playing at <paramref name="time"/> on <paramref name="track"/>, and the seconds into it, or null when
    /// the sprite is hidden. <paramref name="cycleLength"/> returns a reference's cycle length (0 for a missing chain,
    /// which then never plays).
    /// </summary>
    public static SequenceChainReferenceSave? ActiveAt(SequenceTrackSave track, double time,
        Func<SequenceChainReferenceSave, double> cycleLength, out double localTime)
    {
        localTime = 0;
        var ordered = Ordered(track);
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

    /// <summary>Where the sequence ends: the end of the last chain to finish playing. 0 when it has no chains.</summary>
    public static double Length(SequenceSave sequence, Func<SequenceChainReferenceSave, double> cycleLength)
    {
        double end = 0;
        foreach (var track in sequence.Tracks)
            foreach (var reference in track.Chains)
                end = Math.Max(end, reference.Start + PlayedLength(track, reference, cycleLength(reference)));
        return end;
    }

    /// <summary>The sequence time shown at <paramref name="time"/>: wrapped when looping, otherwise held at the end.</summary>
    public static double WrapTime(double time, double length, bool loop)
    {
        if (length <= 0) return 0;
        if (time < length) return Math.Max(0, time);
        return loop ? time % length : length;
    }

    /// <summary>
    /// The loop count for dragging a chain's right edge to <paramref name="edgeTime"/>: free, pulled to a whole cycle
    /// within <paramref name="magnetSeconds"/>, and stopped at <paramref name="nextStart"/> since length past it never plays.
    /// </summary>
    public static float LoopsForEdgeDrag(double start, double edgeTime, double cycleLength, double? nextStart, double magnetSeconds)
    {
        if (cycleLength <= 0) return 1;
        double loops = (edgeTime - start) / cycleLength;
        double whole = Math.Round(loops);
        if (whole >= 1 && Math.Abs(loops - whole) * cycleLength <= magnetSeconds)
            loops = whole;
        if (nextStart is { } next)
            loops = Math.Min(loops, (next - start) / cycleLength);
        return (float)Math.Max(MinimumLoops, loops);
    }

    private static int IndexOfReference(IReadOnlyList<SequenceChainReferenceSave> ordered, SequenceChainReferenceSave reference)
    {
        for (int i = 0; i < ordered.Count; i++)
            if (ReferenceEquals(ordered[i], reference)) return i;
        return -1;
    }
}
