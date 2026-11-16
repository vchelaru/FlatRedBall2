namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>One step from a sequence into a sequence its reference track plays.</summary>
/// <param name="Reference">The reference clip.</param>
/// <param name="Target">The sequence it plays.</param>
public sealed record ReferenceHop(SequenceReferenceSave Reference, SequenceSave Target);

/// <summary>
/// A chain track of a sequence with the reference tracks flattened away (<see cref="SequenceReferences.Inline"/>).
/// </summary>
/// <param name="Name">The track's name. Tracks inherited through a reference are prefixed with the reference track's name
/// and <c>/</c> (<c>Earth/Body</c>, <c>Earth/Dust/Body</c> when nested), so they cannot collide with the holding sequence's own tracks.</param>
/// <param name="Track">The chain track as written in its own sequence.</param>
/// <param name="Visible">Whether the track and every reference track on its way in are visible.</param>
/// <param name="Hops">The reference clips between the holding sequence and <paramref name="Track"/>, outermost first; empty for the sequence's own track.</param>
public sealed record InlinedTrack(string Name, SequenceTrackSave Track, bool Visible, IReadOnlyList<ReferenceHop> Hops);

/// <summary>
/// The rules for tracks that play another sequence (<see cref="SequenceTrackSave.Reference"/>), shared by the editor and
/// any runtime player: cycle detection, flattening into chain tracks, and the time mapping into the target. A reference
/// is resolved by sequence name within the one file; the first sequence with the name wins.
/// </summary>
public static class SequenceReferences
{
    /// <summary>
    /// A cycle among the sequences' references as the names along it, ending where it started (<c>A, B, A</c>), or null
    /// when there is none. A reference to a missing sequence is not a cycle.
    /// </summary>
    public static IReadOnlyList<string>? FindCycle(IReadOnlyList<SequenceSave> sequences)
    {
        var done = new HashSet<SequenceSave>();
        foreach (var sequence in sequences)
        {
            if (Visit(sequences, sequence, new List<SequenceSave>(), done) is { } cycle) return cycle;
        }
        return null;
    }

    private static List<string>? Visit(IReadOnlyList<SequenceSave> sequences, SequenceSave sequence, List<SequenceSave> stack, HashSet<SequenceSave> done)
    {
        int at = stack.IndexOf(sequence);
        if (at >= 0) return stack.Skip(at).Append(sequence).Select(s => s.Name).ToList();
        if (!done.Add(sequence)) return null;
        stack.Add(sequence);
        foreach (var track in sequence.Tracks)
        {
            if (track.Reference is { } reference && Find(sequences, reference.Sequence) is { } target
                && Visit(sequences, target, stack, done) is { } cycle)
                return cycle;
        }
        stack.RemoveAt(stack.Count - 1);
        return null;
    }

    /// <summary>
    /// Whether giving <paramref name="host"/> a reference to <paramref name="target"/> would close a cycle: the target is the
    /// host or, through its own references, plays it.
    /// </summary>
    public static bool WouldCycle(IReadOnlyList<SequenceSave> sequences, string host, string target)
    {
        if (host == target) return true;
        var seen = new HashSet<string>();
        bool Reaches(string from)
        {
            if (from == host) return true;
            if (!seen.Add(from) || Find(sequences, from) is not { } sequence) return false;
            return sequence.Tracks.Any(track => track.Reference is { } reference && Reaches(reference.Sequence));
        }
        return Reaches(target);
    }

    /// <summary>The first sequence called <paramref name="name"/>, or null.</summary>
    public static SequenceSave? Find(IReadOnlyList<SequenceSave> sequences, string name) =>
        sequences.FirstOrDefault(sequence => sequence.Name == name);

    /// <summary>
    /// Every chain track <paramref name="sequence"/> plays, in track order, with each reference track replaced by the tracks of
    /// its target (recursively, see <see cref="InlinedTrack.Name"/>). A reference to a missing sequence, or one that would
    /// loop back into itself, adds nothing.
    /// </summary>
    public static IReadOnlyList<InlinedTrack> Inline(IReadOnlyList<SequenceSave> sequences, SequenceSave sequence)
    {
        var result = new List<InlinedTrack>();
        Collect(sequences, sequence, "", true, new List<ReferenceHop>(), new List<SequenceSave> { sequence }, result);
        return result;
    }

    private static void Collect(IReadOnlyList<SequenceSave> sequences, SequenceSave sequence, string prefix, bool visible,
        List<ReferenceHop> hops, List<SequenceSave> stack, List<InlinedTrack> result)
    {
        foreach (var track in sequence.Tracks)
        {
            if (track.Reference is not { } reference)
            {
                result.Add(new InlinedTrack(prefix + track.Name, track, visible && track.Visible, hops.ToArray()));
                continue;
            }
            if (Find(sequences, reference.Sequence) is not { } target || stack.Contains(target)) continue;
            hops.Add(new ReferenceHop(reference, target));
            stack.Add(target);
            Collect(sequences, target, prefix + track.Name + "/", visible && track.Visible, hops, stack, result);
            stack.RemoveAt(stack.Count - 1);
            hops.RemoveAt(hops.Count - 1);
        }
    }

    /// <summary>
    /// The time to sample <paramref name="track"/>'s own sequence at when the outermost sequence is at
    /// <paramref name="time"/>: the time mapped through each hop (<see cref="SequenceTimeline.ReferenceTime"/>), or null
    /// when any clip on the way is not playing. A track of the outermost sequence returns <paramref name="time"/> unchanged.
    /// </summary>
    /// <param name="track">A track from <see cref="Inline"/>.</param>
    /// <param name="time">Seconds on the outermost sequence's clock.</param>
    /// <param name="length">A sequence's length in seconds (<see cref="SequenceTimeline.Length"/> with the caller's chain lengths).</param>
    public static double? InlinedTime(InlinedTrack track, double time, Func<SequenceSave, double> length)
    {
        double? current = time;
        foreach (var hop in track.Hops)
        {
            current = SequenceTimeline.ReferenceTime(hop.Reference, hop.Target, length(hop.Target), current.Value);
            if (current is null) return null;
        }
        return current;
    }
}
