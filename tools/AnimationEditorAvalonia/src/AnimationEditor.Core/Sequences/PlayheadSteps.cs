using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Where the keyboard moves the sequence playhead. A "frame" of a sequence is the span between two times at which the
/// composited picture can change: the start and end of every visible track's chain, and every frame boundary inside a chain
/// (repeated for each cycle it plays), plus time 0 and the sequence end. Stepping goes to the next or previous such time.
/// </summary>
public static class PlayheadSteps
{
    // Two times closer than this are the same boundary (float frame lengths summed in different orders).
    private const double Tolerance = 1e-6;

    // A chain asking for thousands of cycles of tiny frames must not stall a key press.
    private const int MaxBoundariesPerChain = 20000;

    /// <summary>
    /// Every time the picture can change in <paramref name="sequence"/>, ascending and without duplicates, from 0 to
    /// <paramref name="length"/>. Hidden tracks and missing chains add nothing. A chain that repeats (its Loop flag) adds its
    /// frame boundaries in every cycle it plays; one that does not adds only the first cycle's, since the rest is a held frame.
    /// </summary>
    /// <param name="blocks">The timeline's blocks for <paramref name="sequence"/> (<see cref="TimelineLayout.Blocks"/>).</param>
    /// <param name="chainOf">The chain a reference points at, or null when it cannot be found.</param>
    public static IReadOnlyList<double> Boundaries(SequenceSave sequence, IReadOnlyList<TimelineBlock> blocks,
        Func<SequenceChainReferenceSave, AnimationChainSave?> chainOf, double length)
    {
        length = Math.Max(0, length);
        var times = new List<double> { 0, length };
        foreach (var block in blocks)
        {
            if (block.IsMissing || block.CycleLength <= 0 || block.Track < 0 || block.Track >= sequence.Tracks.Count) continue;
            if (!sequence.Tracks[block.Track].Visible || chainOf(block.Reference) is not { } chain) continue;

            times.Add(block.Start);
            times.Add(block.PlayedEnd);
            var inCycle = TimelineSnapTargets.FrameBoundaries(chain);
            int cycles = chain.Loop ? (int)Math.Ceiling(block.Played / block.CycleLength) : 1;
            int added = 0;
            for (int cycle = 0; cycle < cycles && added < MaxBoundariesPerChain; cycle++)
            {
                double cycleStart = block.Start + cycle * block.CycleLength;
                if (cycle > 0 && cycleStart < block.PlayedEnd - Tolerance) times.Add(cycleStart);
                foreach (double offset in inCycle)
                {
                    double time = cycleStart + offset;
                    if (time < block.PlayedEnd - Tolerance) times.Add(time);
                    added++;
                }
            }
        }

        times.Sort();
        var result = new List<double>(times.Count);
        foreach (double time in times.Where(time => time >= 0 && time <= length + Tolerance))
            if (result.Count == 0 || time - result[^1] > Tolerance) result.Add(Math.Min(time, length));
        return result;
    }

    /// <summary>The first boundary after <paramref name="playhead"/>, or the last one when it is already there.</summary>
    public static double Next(IReadOnlyList<double> boundaries, double playhead)
    {
        foreach (double time in boundaries)
            if (time > playhead + Tolerance) return time;
        return boundaries.Count > 0 ? boundaries[^1] : playhead;
    }

    /// <summary>The last boundary before <paramref name="playhead"/>, or the first one (0) when it is already there.</summary>
    public static double Previous(IReadOnlyList<double> boundaries, double playhead)
    {
        for (int i = boundaries.Count - 1; i >= 0; i--)
            if (boundaries[i] < playhead - Tolerance) return boundaries[i];
        return boundaries.Count > 0 ? boundaries[0] : playhead;
    }
}
