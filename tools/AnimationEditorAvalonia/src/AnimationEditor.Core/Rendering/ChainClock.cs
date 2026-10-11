using System;
using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Stateless counterpart of <see cref="PlaybackController"/>: which frame a chain shows at an
/// absolute time, using the same frame lengths, looping and hold-last-frame rules.
/// </summary>
public static class ChainClock
{
    // Frame lengths are floats, so a boundary like 0.3s sums to a hair past 0.3 in double.
    // Nudging the sample forward keeps a time that sits on a boundary in the later frame.
    private const double BoundaryTolerance = 1e-6;

    /// <summary>Sum of the chain's frame lengths in seconds, with unset lengths counted as 0.1s.</summary>
    public static double TotalLength(AnimationChainSave chain)
    {
        double total = 0;
        foreach (var frame in chain.Frames)
            total += PlaybackController.FrameLength(frame);
        return total;
    }

    /// <summary>
    /// The frame showing <paramref name="time"/> seconds after the chain starts, and how far into
    /// that frame the time is. A looping chain wraps; a non-looping chain holds its last frame.
    /// The chain must have at least one frame.
    /// </summary>
    public static (int FrameIndex, double Elapsed) Locate(AnimationChainSave chain, double time)
    {
        double total = TotalLength(chain);
        int last = chain.Frames.Count - 1;

        double local = time + BoundaryTolerance;
        if (local >= total)
        {
            if (!chain.Loop)
                return (last, PlaybackController.FrameLength(chain.Frames[last]));
            local %= total;
        }

        double start = 0;
        for (int i = 0; i < chain.Frames.Count; i++)
        {
            double length = PlaybackController.FrameLength(chain.Frames[i]);
            if (local < start + length)
                return (i, Math.Max(0, local - start - BoundaryTolerance));
            start += length;
        }
        return (last, PlaybackController.FrameLength(chain.Frames[last]));
    }
}
