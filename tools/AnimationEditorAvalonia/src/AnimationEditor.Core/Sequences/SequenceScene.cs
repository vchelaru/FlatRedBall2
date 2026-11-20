using AnimationEditor.Core.Utilities;
using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// One track's sprite at an instant of a sequence: the frame its active chain shows, the texture that frame resolves
/// to through its own source. A track has no offset and no flip: the sprite sits at its frame's offset and draws with
/// the frame's own flip flags.
/// </summary>
/// <param name="Track">The track's index in the sequence.</param>
/// <param name="Chain">The active chain, from the reference's source.</param>
/// <param name="FrameIndex">The frame shown.</param>
/// <param name="ElapsedInFrame">Forward seconds into that frame, for keyframe sampling.</param>
/// <param name="TexturePath">The frame's texture, resolved against its source's folder; null when it has none.</param>
/// <param name="Sequence">The sequence's index in the file.</param>
public sealed record SequenceSprite(
    int Track, AnimationChainSave Chain, FrameLengthUnit Unit, int FrameIndex, double ElapsedInFrame, string? TexturePath, int Sequence = 0)
{
    public AnimationFrameSave Frame => Chain.Frames[FrameIndex];

    /// <summary>The frame's sampled offset and color.</summary>
    public KeyframeSample Sample() => KeyframeSampler.Sample(Chain, Unit, FrameIndex, ElapsedInFrame);
}

/// <summary>One selected sequence and the time its own playhead is at.</summary>
/// <param name="Sequence">The sequence's index in the file.</param>
/// <param name="Time">The playhead inside the current pass of the sequence.</param>
/// <param name="Pass">How many times a looping sequence has wrapped (<see cref="SequenceClock.Pass"/>); only continuous tracks look at it.</param>
public readonly record struct SequenceTime(int Sequence, double Time, int Pass = 0);

/// <summary>What the sequence preview draws at an instant.</summary>
public static class SequenceScene
{
    /// <summary>
    /// The sprites of several sequences playing side by side, each sampled at its own time (see <see cref="At(SequenceDocument, int, double)"/>).
    /// Sequences are drawn in the order given, first at the back; within one, back to front in reverse track order (the first track is in front). All share the entity origin.
    /// </summary>
    public static IReadOnlyList<SequenceSprite> At(SequenceDocument document, IEnumerable<SequenceTime> sequences) =>
        sequences.SelectMany(s => At(document, s.Sequence, s.Time, s.Pass)).ToList();

    /// <summary>
    /// The sprite of every track with a chain playing at <paramref name="time"/> in sequence
    /// <paramref name="sequenceIndex"/>, back to front: the last track (the bottom lane) first, the first track (the top lane) last, so the top lane is in front. A track with no chain playing, or
    /// whose chain is missing or empty, is left out (its sprite is hidden). A track is hidden before its first chain and
    /// after its last one on purpose: this is how a sprite appears or disappears partway through a sequence. A track whose
    /// <see cref="SequenceTrackSave.Visible"/> is false is left out too, shapes included; the sequence's length and
    /// playback do not change. A sequence with <see cref="SequenceSave.Loop"/> off holds its final pose: at or past its
    /// length the sprites are those of its last instant (<see cref="SequenceTimeline.PoseTime"/>), so a track whose chains
    /// finished earlier stays hidden. A looping sequence is sampled at exactly <paramref name="time"/>, in pass
    /// <paramref name="pass"/> (the number of wraps so far): a <see cref="SequenceTrackSave.Continuous"/> track carries its
    /// chain's phase across them (<see cref="SequenceTimeline.ActiveAtPass"/>).
    /// </summary>
    public static IReadOnlyList<SequenceSprite> At(SequenceDocument document, int sequenceIndex, double time, int pass = 0) =>
        sequenceIndex < 0 || sequenceIndex >= document.Save.Sequences.Count
            ? []
            : At(document.Save.Sequences[sequenceIndex], sequenceIndex, time, document, document.Save.Sequences, pass);

    /// <summary>
    /// <see cref="At(SequenceDocument, int, double)"/> for one sequence and any source of chains: the open document, or a
    /// <c>.seqj</c> read without opening it (the Open Project Folder thumbnails). A track that plays another sequence
    /// (<see cref="SequenceTrackSave.Reference"/>) shows that sequence's sprites in its own place in the stack, sampled by
    /// <see cref="SequenceTimeline.ReferenceTime"/> and honouring each hidden track on the way; they report the reference
    /// track's index as <see cref="SequenceSprite.Track"/>, so a click selects it. Pass <paramref name="file"/>, the file's
    /// sequences, to resolve references; without it they show nothing. <paramref name="pass"/> counts the wraps of a looping
    /// sequence, and applies to this sequence's own chain tracks only: a played sequence runs at pass 0.
    /// </summary>
    public static IReadOnlyList<SequenceSprite> At(SequenceSave sequence, int sequenceIndex, double time, ISequenceChainLookup lookup,
        IReadOnlyList<SequenceSave>? file = null, int pass = 0)
    {
        var sprites = new List<SequenceSprite>();
        Collect(sequence, sequenceIndex, time, lookup, file, sprites, null, new List<SequenceSave> { sequence }, pass);
        return sprites;
    }

    private static double LengthOf(SequenceSave sequence, ISequenceChainLookup lookup, IReadOnlyList<SequenceSave> file) =>
        SequenceTimeline.Length(file, sequence, reference => lookup.FindChain(reference) is { } found ? ChainClock.TotalLength(found, lookup.UnitOf(reference)) : 0);

    private static void Collect(SequenceSave sequence, int sequenceIndex, double time, ISequenceChainLookup lookup,
        IReadOnlyList<SequenceSave>? file, List<SequenceSprite> sprites, int? outerTrack, List<SequenceSave> stack, int pass = 0)
    {
        var tracks = sequence.Tracks;
        double CycleLength(SequenceChainReferenceSave reference) =>
            lookup.FindChain(reference) is { } found ? ChainClock.TotalLength(found, lookup.UnitOf(reference)) : 0;
        double length = SequenceTimeline.Length(file ?? Array.Empty<SequenceSave>(), sequence, CycleLength);
        time = SequenceTimeline.PoseTime(time, length, sequence.Loop);
        // Only a looping sequence wraps, so a pass means nothing to one that stops at its end.
        if (!sequence.Loop) pass = 0;
        for (int t = tracks.Count - 1; t >= 0; t--)
        {
            var track = tracks[t];
            if (!track.Visible) continue;
            int reported = outerTrack ?? t;
            if (track.Reference is { } playing)
            {
                // A cycle read from disk plays nothing; the stack check is the backstop for duplicate names.
                if (file is null || !SequenceReferences.Plays(file, sequence.Name, playing.Sequence)
                    || SequenceReferences.Find(file, playing.Sequence) is not { } target || stack.Contains(target)) continue;
                if (SequenceTimeline.ReferenceTime(playing, target, LengthOf(target, lookup, file), time) is not { } inner) continue;
                stack.Add(target);
                Collect(target, sequenceIndex, inner, lookup, file, sprites, reported, stack);
                stack.RemoveAt(stack.Count - 1);
                continue;
            }
            var reference = SequenceTimeline.ActiveAtPass(track, time, pass, length, CycleLength, out double local);
            if (reference is null || lookup.FindChain(reference) is not { Frames.Count: > 0 } chain) continue;

            // The chain's own Loop flag decides what happens past one cycle: a looping chain wraps, a non-looping one holds
            // its last frame until the block ends (ChainClock does both).
            var unit = lookup.UnitOf(reference);
            var (frame, elapsed) = ChainClock.Locate(chain, local, unit);
            string? texture = lookup.ResolveTexturePath(reference.Source, chain.Frames[frame]);
            sprites.Add(new SequenceSprite(reported, chain, unit, frame, elapsed, texture, sequenceIndex));
        }
    }
}

/// <summary>Where a sequence finds the chains its tracks reference, and the textures their frames use.</summary>
public interface ISequenceChainLookup
{
    /// <summary>The chain <paramref name="reference"/> points at, or null when its source or chain is missing.</summary>
    AnimationChainSave? FindChain(SequenceChainReferenceSave reference);

    /// <summary>The texture of <paramref name="frame"/> (a frame of a chain of <paramref name="source"/>), or null when it has none.</summary>
    string? ResolveTexturePath(string source, AnimationFrameSave frame);

    /// <summary>The unit of the frame lengths in the file <paramref name="reference"/> points at (seconds when it is missing).</summary>
    FrameLengthUnit UnitOf(SequenceChainReferenceSave reference);
}
