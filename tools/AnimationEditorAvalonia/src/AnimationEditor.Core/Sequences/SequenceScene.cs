using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// One track's sprite at an instant of a sequence: the frame its active chain shows, the texture that frame resolves
/// to through its own source, and the track's offset and flip.
/// </summary>
/// <param name="Track">The track's index in the sequence.</param>
/// <param name="Chain">The active chain, from the reference's source.</param>
/// <param name="FrameIndex">The frame shown.</param>
/// <param name="ElapsedInFrame">Forward seconds into that frame, for keyframe sampling.</param>
/// <param name="TexturePath">The frame's texture, resolved against its source's folder; null when it has none.</param>
/// <param name="X">The track's offset from the entity origin.</param>
/// <param name="Y">The track's offset from the entity origin, Y+ up.</param>
/// <param name="FlipHorizontal">Whether the track mirrors its sprite.</param>
public sealed record SequenceSprite(
    int Track, AnimationChainSave Chain, int FrameIndex, double ElapsedInFrame, string? TexturePath,
    float X, float Y, bool FlipHorizontal)
{
    public AnimationFrameSave Frame => Chain.Frames[FrameIndex];

    /// <summary>The frame's sampled offset, mirrored by the track flip and moved by the track offset, and its color.</summary>
    public KeyframeSample Sample()
    {
        var sample = KeyframeSampler.Sample(Chain, FrameIndex, ElapsedInFrame);
        float sign = FlipHorizontal ? -1 : 1;
        return sample with { OffsetX = X + sign * sample.OffsetX, OffsetY = Y + sample.OffsetY };
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

/// <summary>What the sequence preview draws at an instant.</summary>
public static class SequenceScene
{
    /// <summary>
    /// The sprite of every track with a chain playing at <paramref name="time"/> in sequence
    /// <paramref name="sequenceIndex"/>, back to front: by Z, equal Z in track order. A track with no chain playing, or
    /// whose chain is missing or empty, is left out (its sprite is hidden).
    /// </summary>
    public static IReadOnlyList<SequenceSprite> At(SequenceDocument document, int sequenceIndex, double time)
    {
        if (sequenceIndex < 0 || sequenceIndex >= document.Save.Sequences.Count) return [];
        var tracks = document.Save.Sequences[sequenceIndex].Tracks;
        var sprites = new List<(int Z, SequenceSprite Sprite)>();
        for (int t = 0; t < tracks.Count; t++)
        {
            var track = tracks[t];
            var reference = SequenceTimeline.ActiveAt(track, time, r => document.CycleLength(r) ?? 0, out double local);
            if (reference is null || document.FindChain(reference) is not { Frames.Count: > 0 } chain) continue;

            // The loop count overrides the chain's own Loop flag, so wrap into a cycle here rather than in ChainClock.
            double cycle = ChainClock.TotalLength(chain);
            var (frame, elapsed) = ChainClock.Locate(chain, local % cycle);
            string? texture = document.ResolveTexturePath(reference.Source, chain.Frames[frame]);
            sprites.Add((track.Z, new SequenceSprite(t, chain, frame, elapsed, texture, track.X, track.Y, track.FlipHorizontal)));
        }
        // OrderBy is stable, so equal Z keeps track order.
        return sprites.OrderBy(s => s.Z).Select(s => s.Sprite).ToList();
    }
}
