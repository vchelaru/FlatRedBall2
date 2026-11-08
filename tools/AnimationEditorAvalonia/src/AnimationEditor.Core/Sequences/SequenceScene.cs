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
    int Track, AnimationChainSave Chain, int FrameIndex, double ElapsedInFrame, string? TexturePath, int Sequence = 0)
{
    public AnimationFrameSave Frame => Chain.Frames[FrameIndex];

    /// <summary>The frame's sampled offset and color.</summary>
    public KeyframeSample Sample() => KeyframeSampler.Sample(Chain, FrameIndex, ElapsedInFrame);
}

/// <summary>One selected sequence and the time its own playhead is at.</summary>
public readonly record struct SequenceTime(int Sequence, double Time);

/// <summary>What the sequence preview draws at an instant.</summary>
public static class SequenceScene
{
    /// <summary>
    /// The sprites of several sequences playing side by side, each sampled at its own time (see <see cref="At(SequenceDocument, int, double)"/>).
    /// Sequences are drawn in the order given, first at the back; within one, back to front in reverse track order (the first track is in front). All share the entity origin.
    /// </summary>
    public static IReadOnlyList<SequenceSprite> At(SequenceDocument document, IEnumerable<SequenceTime> sequences) =>
        sequences.SelectMany(s => At(document, s.Sequence, s.Time)).ToList();

    /// <summary>
    /// The sprite of every track with a chain playing at <paramref name="time"/> in sequence
    /// <paramref name="sequenceIndex"/>, back to front: the last track (the bottom lane) first, the first track (the top lane) last, so the top lane is in front. A track with no chain playing, or
    /// whose chain is missing or empty, is left out (its sprite is hidden). A track is hidden before its first chain and
    /// after its last one on purpose: this is how a sprite appears or disappears partway through a sequence. A track whose
    /// <see cref="SequenceTrackSave.Visible"/> is false is left out too, shapes included; the sequence's length and
    /// playback do not change. A sequence with <see cref="SequenceSave.Loop"/> off holds its final pose: at or past its
    /// length the sprites are those of its last instant (<see cref="SequenceTimeline.PoseTime"/>), so a track whose chains
    /// finished earlier stays hidden. A looping sequence is sampled at exactly <paramref name="time"/>.
    /// </summary>
    public static IReadOnlyList<SequenceSprite> At(SequenceDocument document, int sequenceIndex, double time) =>
        sequenceIndex < 0 || sequenceIndex >= document.Save.Sequences.Count
            ? []
            : At(document.Save.Sequences[sequenceIndex], sequenceIndex, time, document);

    /// <summary>
    /// <see cref="At(SequenceDocument, int, double)"/> for one sequence and any source of chains: the open document, or a
    /// <c>.seqj</c> read without opening it (the Open Project Folder thumbnails).
    /// </summary>
    public static IReadOnlyList<SequenceSprite> At(SequenceSave sequence, int sequenceIndex, double time, ISequenceChainLookup lookup)
    {
        var tracks = sequence.Tracks;
        double CycleLength(SequenceChainReferenceSave reference) =>
            lookup.FindChain(reference) is { } found ? ChainClock.TotalLength(found) : 0;
        time = SequenceTimeline.PoseTime(time, SequenceTimeline.Length(sequence, CycleLength), sequence.Loop);
        var sprites = new List<SequenceSprite>();
        for (int t = tracks.Count - 1; t >= 0; t--)
        {
            var track = tracks[t];
            if (!track.Visible) continue;
            var reference = SequenceTimeline.ActiveAt(track, time, CycleLength, out double local);
            if (reference is null || lookup.FindChain(reference) is not { Frames.Count: > 0 } chain) continue;

            // The chain's own Loop flag decides what happens past one cycle: a looping chain wraps, a non-looping one holds
            // its last frame until the block ends (ChainClock does both).
            var (frame, elapsed) = ChainClock.Locate(chain, local);
            string? texture = lookup.ResolveTexturePath(reference.Source, chain.Frames[frame]);
            sprites.Add(new SequenceSprite(t, chain, frame, elapsed, texture, sequenceIndex));
        }
        return sprites;
    }
}

/// <summary>Where a sequence finds the chains its tracks reference, and the textures their frames use.</summary>
public interface ISequenceChainLookup
{
    /// <summary>The chain <paramref name="reference"/> points at, or null when its source or chain is missing.</summary>
    AnimationChainSave? FindChain(SequenceChainReferenceSave reference);

    /// <summary>The texture of <paramref name="frame"/> (a frame of a chain of <paramref name="source"/>), or null when it has none.</summary>
    string? ResolveTexturePath(string source, AnimationFrameSave frame);
}
