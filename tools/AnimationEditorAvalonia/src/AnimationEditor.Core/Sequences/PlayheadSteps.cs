using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Where the keyboard moves the sequence playhead. A "frame" of a sequence is the span between two times at which the
/// composited picture can change: the start and end of every visible track's chain, and every frame boundary inside a chain
/// (repeated for each cycle it plays), the same of every clip and of the sequence it plays, plus time 0 and the sequence end.
/// Stepping goes to the next or previous such time.
/// </summary>
public static class PlayheadSteps
{
    // Two times closer than this are the same boundary (float frame lengths summed in different orders).
    private const double Tolerance = 1e-6;

    // A chain asking for thousands of cycles of tiny frames must not stall a key press.
    private const int MaxBoundariesPerChain = 20000;

    /// <summary>
    /// Every time the picture can change in <paramref name="sequence"/>, ascending and without duplicates, from 0 to its
    /// length. Hidden tracks, missing chains and clips that do not play add nothing. A chain that repeats (its Loop flag) adds
    /// its frame boundaries in every cycle it plays; one that does not adds only the first cycle's, since the rest is a held
    /// frame. A clip adds the boundaries of the sequence it plays the same way, through every level.
    /// </summary>
    /// <param name="file">The file's sequences, which clips resolve against.</param>
    /// <param name="sequence">The sequence stepped through.</param>
    /// <param name="chainOf">The chain a reference points at, or null when it cannot be found.</param>
    /// <param name="unitOf">The unit of the frame lengths in the file a reference points at.</param>
    public static IReadOnlyList<double> Boundaries(IReadOnlyList<SequenceSave> file, SequenceSave sequence,
        Func<SequenceChainReferenceSave, AnimationChainSave?> chainOf, Func<SequenceChainReferenceSave, FrameLengthUnit> unitOf) =>
        Within(file, sequence, chainOf, unitOf, new List<SequenceSave> { sequence });

    private static IReadOnlyList<double> Within(IReadOnlyList<SequenceSave> file, SequenceSave sequence,
        Func<SequenceChainReferenceSave, AnimationChainSave?> chainOf, Func<SequenceChainReferenceSave, FrameLengthUnit> unitOf, List<SequenceSave> stack)
    {
        double? CycleLength(SequenceChainReferenceSave reference) =>
            ChainClock.CycleLength(chainOf(reference), unitOf(reference));
        double LengthOf(SequenceSave s) => SequenceTimeline.Length(file, s, CycleLength);

        double length = LengthOf(sequence);
        var times = new List<double> { 0, length };
        foreach (var block in TimelineLayout.Blocks(sequence, CycleLength))
        {
            if (block.IsStandIn) continue;
            if (!sequence.Tracks[block.Track].Visible || chainOf(block.Reference) is not { } chain) continue;
            // The frame a chain that does not loop holds first shows at the cycle's end when it has no length of its own.
            double cycleEnd = block.Start + block.CycleLength;
            if (!chain.Loop && cycleEnd < block.PlayedEnd - Tolerance
                && unitOf(block.Reference) is var unit && PlaybackController.FrameSeconds(chain.Frames[ChainClock.Locate(chain, block.CycleLength, unit).FrameIndex], unit) <= 0)
                times.Add(cycleEnd);
            AddCycles(times, block.Start, block.Played, block.CycleLength, chain.Loop,
                TimelineSnapTargets.FrameBoundaries(chain, unitOf(block.Reference)));
        }
        foreach (var track in sequence.Tracks)
        {
            if (!track.Visible) continue;
            foreach (var clip in SequenceTimeline.OrderedClips(track))
            {
                // The stack check is a backstop for duplicate names, which Plays (by name) cannot tell apart.
                if (!SequenceReferences.Plays(file, sequence.Name, clip.Sequence)
                    || SequenceReferences.Find(file, clip.Sequence) is not { } target || stack.Contains(target)) continue;
                double targetLength = LengthOf(target);
                if (targetLength <= 0) continue;
                stack.Add(target);
                var inner = Within(file, target, chainOf, unitOf, stack);
                stack.RemoveAt(stack.Count - 1);
                double played = SequenceTimeline.PlayedLength(clip.Start, SequenceTimeline.FullLength(clip.Length, targetLength),
                    SequenceTimeline.NextStart(track, clip));
                AddCycles(times, clip.Start, played, targetLength, target.Loop, inner.Where(time => time > 0 && time < targetLength - Tolerance).ToList());
            }
        }

        times.Sort();
        var result = new List<double>(times.Count);
        foreach (double time in times.Where(time => time >= 0 && time <= length + Tolerance))
            if (result.Count == 0 || time - result[^1] > Tolerance) result.Add(Math.Min(time, length));
        return result;
    }

    // The start and end of a block or clip, and the boundaries inside it (offsets from a cycle's start) in every cycle it plays.
    private static void AddCycles(List<double> times, double start, double played, double cycleLength, bool loop, IReadOnlyList<double> inCycle)
    {
        double end = start + played;
        times.Add(start);
        times.Add(end);
        if (cycleLength <= 0) return; // a held frame
        int cycles = loop ? (int)Math.Ceiling(played / cycleLength) : 1;
        int added = 0;
        for (int cycle = 0; cycle < cycles && added < MaxBoundariesPerChain; cycle++)
        {
            double cycleStart = start + cycle * cycleLength;
            if (cycle > 0 && cycleStart < end - Tolerance) times.Add(cycleStart);
            added++;
            foreach (double offset in inCycle)
            {
                double time = cycleStart + offset;
                if (time < end - Tolerance) times.Add(time);
                added++;
            }
        }
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
