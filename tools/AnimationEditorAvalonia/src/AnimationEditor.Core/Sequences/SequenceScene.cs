using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// One track's sprite at an instant of a sequence: the frame its active chain shows, the texture that frame resolves
/// to through its own source, and the track's flip. A track has no offset: the sprite sits at its frame's.
/// </summary>
/// <param name="Track">The track's index in the sequence.</param>
/// <param name="Chain">The active chain, from the reference's source.</param>
/// <param name="FrameIndex">The frame shown.</param>
/// <param name="ElapsedInFrame">Forward seconds into that frame, for keyframe sampling.</param>
/// <param name="TexturePath">The frame's texture, resolved against its source's folder; null when it has none.</param>
/// <param name="FlipHorizontal">Whether the track mirrors its sprite.</param>
/// <param name="Sequence">The sequence's index in the file.</param>
public sealed record SequenceSprite(
    int Track, AnimationChainSave Chain, int FrameIndex, double ElapsedInFrame, string? TexturePath,
    bool FlipHorizontal, int Sequence = 0)
{
    public AnimationFrameSave Frame => Chain.Frames[FrameIndex];

    /// <summary>The frame's sampled offset, with X mirrored by the track flip, and its color.</summary>
    public KeyframeSample Sample()
    {
        var sample = KeyframeSampler.Sample(Chain, FrameIndex, ElapsedInFrame);
        return FlipHorizontal ? sample with { OffsetX = -sample.OffsetX } : sample;
    }

    /// <summary>
    /// The 2x2 draw matrix when the track mirrors the frame (the frame's own flip with X negated), or null when the
    /// frame's own flip flags apply unchanged.
    /// </summary>
    public (float a, float b, float c, float d)? FlipMatrix
    {
        get
        {
            if (!FlipHorizontal) return null;
            var m = FlipScaleCalculator.ComputeMatrix(Frame.FlipHorizontal, Frame.FlipVertical, Frame.FlipDiagonal);
            return (-m.a, -m.b, m.c, m.d);
        }
    }
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
    public static IReadOnlyList<SequenceSprite> At(SequenceDocument document, int sequenceIndex, double time)
    {
        if (sequenceIndex < 0 || sequenceIndex >= document.Save.Sequences.Count) return [];
        var sequence = document.Save.Sequences[sequenceIndex];
        var tracks = sequence.Tracks;
        time = SequenceTimeline.PoseTime(time, document.SequenceLength(sequenceIndex), sequence.Loop);
        var sprites = new List<SequenceSprite>();
        for (int t = tracks.Count - 1; t >= 0; t--)
        {
            var track = tracks[t];
            if (!track.Visible) continue;
            var reference = SequenceTimeline.ActiveAt(track, time, r => document.CycleLength(r) ?? 0, out double local);
            if (reference is null || document.FindChain(reference) is not { Frames.Count: > 0 } chain) continue;

            // The chain's own Loop flag decides what happens past one cycle: a looping chain wraps, a non-looping one holds
            // its last frame until the block ends (ChainClock does both).
            var (frame, elapsed) = ChainClock.Locate(chain, local);
            string? texture = document.ResolveTexturePath(reference.Source, chain.Frames[frame]);
            sprites.Add(new SequenceSprite(t, chain, frame, elapsed, texture, track.FlipHorizontal, sequenceIndex));
        }
        return sprites;
    }
}
