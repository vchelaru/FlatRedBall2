using System;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Rendering;

/// <summary>Where a chain's playback is at one instant: the frame shown, how far into it, and whether it plays backward.</summary>
public readonly record struct ChainClockPosition(int FrameIndex, double Elapsed, bool IsBackward);

/// <summary>
/// Stateless counterpart of <see cref="PlaybackController"/>: which frame a chain shows at an
/// absolute time, using the same frame lengths, looping, hold-at-end, and PingPong/Reverse visit order
/// (<see cref="PlaybackOrder"/>) rules.
/// </summary>
public static class ChainClock
{
    // Frame lengths are floats, so a boundary like 0.3s sums to a hair past 0.3 in double.
    // Nudging the sample forward keeps a time that sits on a boundary in the later frame.
    private const double BoundaryTolerance = 1e-6;

    /// <summary>
    /// Length of one pass of the chain in seconds (unset lengths count as 0.1s): the sum of its frames, or under
    /// <see cref="AnimationChainSave.PingPong"/> the whole round trip.
    /// </summary>
    public static double TotalLength(AnimationChainSave chain)
    {
        double total = 0;
        int visits = PlaybackOrder.VisitCount(chain.Frames.Count, chain.Loop, chain.PingPong);
        for (int v = 0; v < visits; v++)
            total += PlaybackController.FrameLength(chain.Frames[Visit(chain, v).FrameIndex]);
        return total;
    }

    /// <summary>
    /// The frame showing <paramref name="time"/> seconds after the chain starts, and how far into
    /// that frame the time is. A looping chain wraps; a non-looping chain holds its last visit.
    /// The chain must have at least one frame.
    /// </summary>
    public static (int FrameIndex, double Elapsed) Locate(AnimationChainSave chain, double time)
    {
        var at = LocateVisit(chain, time);
        return (at.FrameIndex, at.Elapsed);
    }

    /// <summary>Same as <see cref="Locate"/>, also reporting whether the frame is playing backward.</summary>
    public static ChainClockPosition LocateVisit(AnimationChainSave chain, double time)
    {
        double total = TotalLength(chain);
        int visits = PlaybackOrder.VisitCount(chain.Frames.Count, chain.Loop, chain.PingPong);
        var lastVisit = Visit(chain, visits - 1);

        double local = time + BoundaryTolerance;
        if (local >= total)
        {
            if (!chain.Loop)
                return new(lastVisit.FrameIndex, PlaybackController.FrameLength(chain.Frames[lastVisit.FrameIndex]), lastVisit.IsBackward);
            local %= total;
        }

        double start = 0;
        for (int v = 0; v < visits; v++)
        {
            var visit = Visit(chain, v);
            double length = PlaybackController.FrameLength(chain.Frames[visit.FrameIndex]);
            if (local < start + length)
                return new(visit.FrameIndex, Math.Max(0, local - start - BoundaryTolerance), visit.IsBackward);
            start += length;
        }
        return new(lastVisit.FrameIndex, PlaybackController.FrameLength(chain.Frames[lastVisit.FrameIndex]), lastVisit.IsBackward);
    }

    private static PlaybackVisit Visit(AnimationChainSave chain, int visitIndex) =>
        PlaybackOrder.VisitAt(visitIndex, chain.Frames.Count, chain.PingPong, chain.Reverse);
}
