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

    /// <summary>
    /// Total play duration of <paramref name="chain"/> in seconds — the sum of every frame's
    /// <see cref="AnimationFrameSave.FrameLength"/>. FrameLength is treated as seconds (matching
    /// the editor's per-frame display) regardless of the file's <c>TimeMeasurementUnit</c>.
    /// Returns 0 for a null or empty chain.
    /// </summary>
    public static float TotalSeconds(AnimationChainSave? chain)
    {
        if (chain is null)
            return 0f;

        float total = 0f;
        foreach (var frame in chain.Frames)
            total += frame.FrameLength;
        return total;
    }

    /// <summary>Sum of <see cref="TotalSeconds(AnimationChainSave?)"/> across every chain in the list.</summary>
    public static float TotalSeconds(AnimationChainListSave? acls)
    {
        if (acls is null)
            return 0f;

        float total = 0f;
        foreach (var chain in acls.AnimationChains)
            total += TotalSeconds(chain);
        return total;
    }

    /// <summary>Formats a duration in seconds for display, e.g. <c>1.5</c> → <c>"1.50s"</c>.</summary>
    public static string FormatSeconds(float seconds) => $"{seconds:0.00}s";

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
