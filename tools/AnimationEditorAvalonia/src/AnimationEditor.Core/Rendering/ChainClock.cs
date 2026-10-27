using System;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Stateless counterpart of <see cref="PlaybackController"/>: which frame a chain shows at an
/// absolute time, using the same frame lengths, looping, hold-at-end, and Reverse/PingPong mirroring
/// (<see cref="PlaybackTimeline"/>) rules.
/// </summary>
public static class ChainClock
{
    // Frame lengths are floats, so a boundary like 0.3s sums to a hair past 0.3 in double.
    // Nudging the sample forward keeps a time that sits on a boundary in the later frame.
    private const double BoundaryTolerance = 1e-6;

    /// <summary>
    /// Length of one cycle in seconds (unset lengths count as 0.1s): the sum of the chain's frames, or twice that under
    /// <see cref="AnimationChainSave.PingPong"/>.
    /// </summary>
    public static double TotalLength(AnimationChainSave chain)
    {
        double total = 0;
        foreach (var frame in chain.Frames)
            total += PlaybackController.FrameLength(frame);
        return PlaybackTimeline.CycleLength(total, chain.PingPong);
    }

    /// <summary>
    /// The frame showing <paramref name="time"/> seconds after the chain starts, and the forward seconds into
    /// that frame to sample it at. A looping chain wraps; a non-looping chain holds its end.
    /// The chain must have at least one frame.
    /// </summary>
    public static (int FrameIndex, double Elapsed) Locate(AnimationChainSave chain, double time)
    {
        double total = TotalLength(chain);
        double local = time + BoundaryTolerance;
        bool atEnd = local >= total && !chain.Loop;
        if (local >= total)
            local = chain.Loop ? local % total : total;

        var at = PlaybackTimeline.Locate(chain.Frames, PlaybackController.FrameLength, local, chain.PingPong, chain.Reverse);
        // Undo the nudge on the sampled position, in the direction the timeline plays.
        double elapsed = atEnd ? at.ElapsedInFrame
            : at.IsBackward ? at.ElapsedInFrame + BoundaryTolerance : at.ElapsedInFrame - BoundaryTolerance;
        return (at.FrameIndex, Math.Clamp(elapsed, 0, PlaybackController.FrameLength(chain.Frames[at.FrameIndex])));
    }
}
