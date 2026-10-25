using System;
using System.Collections.Generic;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>A playback instant mapped onto the chain's forward timeline.</summary>
/// <param name="ForwardTime">Seconds into the forward timeline (0 = start of the first frame, T = end of the last).</param>
/// <param name="IsBackward">Whether that timeline is being played mirrored at this instant.</param>
public readonly record struct TimelinePoint(double ForwardTime, bool IsBackward);

/// <summary>Where playback is: the frame shown and the forward-timeline position to evaluate it at.</summary>
/// <param name="FrameIndex">The frame shown.</param>
/// <param name="ElapsedInFrame">Forward seconds into that frame (0 = its start, its length = its end), whichever direction plays.
/// Evaluate keyframes and place a timeline playhead with this; it falls as a frame plays backward.</param>
/// <param name="ForwardTime">See <see cref="TimelinePoint.ForwardTime"/>.</param>
/// <param name="IsBackward">See <see cref="TimelinePoint.IsBackward"/>.</param>
/// <param name="Visit">Which showing of the frame this is within one cycle (see <see cref="PlaybackTimeline.VisitCount"/>), for frame events.</param>
public readonly record struct PlaybackPosition(int FrameIndex, double ElapsedInFrame, double ForwardTime, bool IsBackward, int Visit);

/// <summary>
/// Reverse and PingPong (#1538) as mirrors of the forward timeline, shared by every player so they agree. The forward
/// timeline (frames in order, length T = the sum of the frame lengths) is the only thing that is evaluated:
/// <list type="bullet">
/// <item>Reverse plays it mirrored: cycle time t shows forward time T - t.</item>
/// <item>PingPong plays it forward, then mirrored, for a cycle of 2T. The end frames show for twice their length across
/// the turnaround (and across the loop point), and their keyframe glides play backward on the way back.</item>
/// <item>PingPong and Reverse start with the mirrored half.</item>
/// </list>
/// A player keeps its own cycle time, maps it with <see cref="Map"/> (or <see cref="Locate"/>), and evaluates frames and
/// keyframe curves with <see cref="KeyframeEvaluator"/> at the forward time.
/// </summary>
public static class PlaybackTimeline
{
    /// <summary>Length of one cycle: T, or 2T for a ping-pong. A looping player wraps here; a non-looping one stops here.</summary>
    public static double CycleLength(double forwardLength, bool pingPong) => pingPong ? 2 * forwardLength : forwardLength;

    /// <summary>
    /// The forward time shown <paramref name="cycleTime"/> seconds into a cycle (clamped to the cycle), and whether the
    /// timeline is mirrored there. A ping-pong's turnaround instant (cycle time T) already counts as mirrored.
    /// </summary>
    public static TimelinePoint Map(double cycleTime, double forwardLength, bool pingPong, bool reverse)
    {
        double t = forwardLength;
        double u = Math.Clamp(cycleTime, 0, CycleLength(t, pingPong));
        if (!pingPong) return reverse ? new(t - u, true) : new(u, false);
        bool firstHalf = u < t;
        if (!reverse) return firstHalf ? new(u, false) : new(2 * t - u, true);
        return firstHalf ? new(t - u, true) : new(u - t, false);
    }

    /// <summary>The first cycle time that shows <paramref name="forwardTime"/>: itself, or T minus it when the cycle starts mirrored.</summary>
    public static double ToCycleTime(double forwardTime, double forwardLength, bool reverse) =>
        reverse ? forwardLength - forwardTime : forwardTime;

    /// <summary>
    /// Whether keyframe glides wrap from the last keyframe back to the first. Only a looping chain that is not a ping-pong
    /// does; a ping-pong turns around instead. Pass this as <see cref="KeyframeEvaluator"/>'s <c>loop</c> argument.
    /// </summary>
    public static bool GlidesAcrossLoop(bool loop, bool pingPong) => loop && !pingPong;

    /// <summary>
    /// <see cref="Map"/> plus the frame shown. A time exactly on a frame boundary shows the frame playback is leaving
    /// unless <paramref name="boundaryEntersNextFrame"/> (the runtimes hold the old frame; the AnimationEditor shows the new one).
    /// </summary>
    public static PlaybackPosition Locate<TFrame>(IReadOnlyList<TFrame> frames, Func<TFrame, double> length,
        double cycleTime, bool pingPong, bool reverse, bool boundaryEntersNextFrame)
    {
        int n = frames.Count;
        if (n == 0) return default;
        double total = 0;
        for (int i = 0; i < n; i++) total += length(frames[i]);

        var point = Map(cycleTime, total, pingPong, reverse);
        double f = point.ForwardTime;
        // Going forward the frame being left is the earlier one, going backward the later one.
        bool strict = point.IsBackward != boundaryEntersNextFrame;
        int frame = n - 1;
        double start = 0;
        for (int i = 0; i < n; i++)
        {
            double end = start + length(frames[i]);
            if (strict ? f < end : f <= end)
            {
                frame = i;
                break;
            }
            start = end;
        }
        if (frame == n - 1) start = total - length(frames[n - 1]);
        double elapsed = Math.Clamp(f - start, 0, length(frames[frame]));

        bool firstHalf = !pingPong || (reverse ? point.IsBackward : !point.IsBackward);
        int visit = point.IsBackward ? n - 1 - frame : frame;
        if (!firstHalf) visit += n;
        return new PlaybackPosition(frame, elapsed, f, point.IsBackward, visit);
    }

    /// <summary>
    /// How many showings of a frame one cycle has: one per frame, or two under ping-pong (once each way; the end frames'
    /// two showings are back to back).
    /// </summary>
    public static int VisitCount(int frameCount, bool pingPong) => pingPong ? 2 * frameCount : frameCount;

    /// <summary>The frame of visit <paramref name="visit"/> within a cycle, in the order <see cref="Locate"/> numbers them.</summary>
    public static int FrameOfVisit(int visit, int frameCount, bool pingPong, bool reverse)
    {
        int n = frameCount;
        if (!pingPong) return reverse ? n - 1 - visit : visit;
        if (!reverse) return visit < n ? visit : 2 * n - 1 - visit;
        return visit < n ? n - 1 - visit : visit - n;
    }

    /// <summary>
    /// Whether reaching visit <paramref name="visit"/> enters a frame, so its frame events fire. Always for a chain that
    /// is not a ping-pong (a plain loop re-enters its first frame on each wrap, even a one-frame chain); for a ping-pong,
    /// only when the shown frame changes, so the turnaround and the loop point raise nothing.
    /// </summary>
    public static bool EntersNewFrame(int visit, int frameCount, bool pingPong, bool reverse)
    {
        if (!pingPong) return true;
        int count = VisitCount(frameCount, true);
        return FrameOfVisit(visit, frameCount, true, reverse) != FrameOfVisit((visit - 1 + count) % count, frameCount, true, reverse);
    }
}
