using System;
using System.Collections.Generic;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>One step of playback: which frame shows, and whether it plays backward (its keyframe curves time-reversed).</summary>
public readonly record struct PlaybackVisit(int FrameIndex, bool IsBackward);

/// <summary>
/// The order a chain's frames play in under PingPong and Reverse (#1538), shared by every player so they agree.
/// Playback time runs through a sequence of <em>visits</em>, each showing one frame for that frame's length:
/// <list type="bullet">
/// <item>Default: 0, 1, ... n-1.</item>
/// <item>Reverse: n-1, ... 1, 0, all backward.</item>
/// <item>PingPong: 0 ... n-2 forward, then n-1 ... 1 backward (A B C B), so the end frames are not repeated at the
/// turnaround. A non-looping ping-pong adds a final backward visit of frame 0 (A B C B A) and stops there.</item>
/// <item>Reverse + PingPong: n-1 ... 1 backward, then 0 ... n-2 forward; non-looping adds a final visit of n-1.</item>
/// </list>
/// A looping chain repeats the sequence; one pass through it is one loop.
/// </summary>
public static class PlaybackOrder
{
    /// <summary>How many visits one pass of the sequence has. Zero for an empty chain.</summary>
    public static int VisitCount(int frameCount, bool loop, bool pingPong)
    {
        if (frameCount <= 0) return 0;
        if (!pingPong || frameCount == 1) return frameCount;
        return 2 * frameCount - 2 + (loop ? 0 : 1);
    }

    /// <summary>The frame shown by visit <paramref name="visitIndex"/> (0-based, within one pass) and its direction.</summary>
    public static PlaybackVisit VisitAt(int visitIndex, int frameCount, bool pingPong, bool reverse)
    {
        int last = frameCount - 1;
        if (!pingPong || frameCount <= 1)
            return reverse ? new PlaybackVisit(last - visitIndex, true) : new PlaybackVisit(visitIndex, false);

        // The first half covers visits 0..n-2; the turnaround frame starts the second half.
        if (visitIndex < last)
            return reverse ? new PlaybackVisit(last - visitIndex, true) : new PlaybackVisit(visitIndex, false);
        return reverse ? new PlaybackVisit(visitIndex - last, false) : new PlaybackVisit(2 * last - visitIndex, true);
    }

    /// <summary>
    /// Whether keyframe glides wrap from the last keyframe back to the first. Only a plain looping chain does: a
    /// ping-pong turns around instead, so its ends hold. Pass this as the evaluator's <c>loop</c> argument.
    /// </summary>
    public static bool GlidesAcrossLoop(bool loop, bool pingPong) => loop && !pingPong;

    /// <summary>Length of one pass of the sequence in seconds, from each frame's <see cref="IKeyframeFrame.DurationSeconds"/>.</summary>
    public static double PassLength<TFrame>(IReadOnlyList<TFrame> frames, bool loop, bool pingPong, bool reverse)
        where TFrame : IKeyframeFrame
    {
        double total = 0;
        int count = VisitCount(frames.Count, loop, pingPong);
        for (int v = 0; v < count; v++)
            total += frames[VisitAt(v, frames.Count, pingPong, reverse).FrameIndex].DurationSeconds;
        return total;
    }

    /// <summary>
    /// The visit showing at <paramref name="time"/> seconds into one pass (clamped to the pass), and when it started.
    /// A time exactly on a boundary stays in the earlier visit, matching the runtimes' frame lookup.
    /// </summary>
    public static int VisitAtTime<TFrame>(IReadOnlyList<TFrame> frames, bool loop, bool pingPong, bool reverse,
        double time, out double visitStart)
        where TFrame : IKeyframeFrame
    {
        int count = VisitCount(frames.Count, loop, pingPong);
        visitStart = 0;
        if (count == 0) return 0;
        double start = 0;
        for (int v = 0; v < count; v++)
        {
            double length = frames[VisitAt(v, frames.Count, pingPong, reverse).FrameIndex].DurationSeconds;
            if (time - (start + length) <= 0)
            {
                visitStart = start;
                return v;
            }
            start += length;
        }
        visitStart = Math.Max(0, start - frames[VisitAt(count - 1, frames.Count, pingPong, reverse).FrameIndex].DurationSeconds);
        return count - 1;
    }
}
