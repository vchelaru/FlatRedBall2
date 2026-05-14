using System;
using System.Collections.Generic;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.ViewModels;

public static class TimelineBuilder
{
    /// <summary>
    /// Minimum cell width in pixels, applied only to zero-length and negative-length frames.
    /// </summary>
    public const double MinCellWidth = 24.0;

    /// <summary>
    /// Baseline pixels-per-second. The effective rate is scaled up when the chain contains
    /// frames shorter than <c>MinCellWidth / PixelsPerSecond</c> seconds so every frame is
    /// at least <see cref="MinCellWidth"/> px wide while all widths stay proportional to each other.
    /// </summary>
    public const double PixelsPerSecond = 120.0;

    /// <summary>
    /// Returns the pixels-per-second rate that makes the shortest non-zero frame exactly
    /// <see cref="MinCellWidth"/> pixels wide, or <see cref="PixelsPerSecond"/> when all
    /// frames are already long enough.
    /// </summary>
    public static double ComputeEffectivePixelsPerSecond(AnimationChainSave? chain)
    {
        if (chain is null || chain.Frames.Count == 0)
            return PixelsPerSecond;

        double minDuration = double.MaxValue;
        foreach (var frame in chain.Frames)
        {
            if (frame.FrameLength > 0)
                minDuration = Math.Min(minDuration, frame.FrameLength);
        }

        if (minDuration == double.MaxValue)
            return PixelsPerSecond; // all frames are zero-length

        return Math.Max(PixelsPerSecond, MinCellWidth / minDuration);
    }

    public static List<TimelineFrameVm> BuildFrameItems(AnimationChainSave? chain)
    {
        if (chain is null)
            return [];

        double pps = ComputeEffectivePixelsPerSecond(chain);

        var result = new List<TimelineFrameVm>(chain.Frames.Count);
        for (int i = 0; i < chain.Frames.Count; i++)
        {
            var length = Math.Max(0f, chain.Frames[i].FrameLength);
            var width = length > 0 ? length * pps : MinCellWidth;
            result.Add(new TimelineFrameVm(i, width));
        }

        return result;
    }
}
