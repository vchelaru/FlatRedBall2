using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;

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
    public static double ComputeEffectivePixelsPerSecond(AnimationChainSave? chain, FrameLengthUnit unit)
    {
        if (chain is null || chain.Frames.Count == 0)
            return PixelsPerSecond;

        double minDuration = double.MaxValue;
        foreach (var frame in chain.Frames)
        {
            if (frame.FrameLength > 0)
                minDuration = Math.Min(minDuration, unit.ToSeconds(frame.FrameLength));
        }

        if (minDuration == double.MaxValue)
            return PixelsPerSecond; // all frames are zero-length

        return Math.Max(PixelsPerSecond, MinCellWidth / minDuration);
    }

    /// <summary>
    /// Returns one shared pixels-per-second rate for every chain in <paramref name="chains"/>, based
    /// on the shortest non-zero frame across all of them combined. Use this (instead of calling
    /// <see cref="ComputeEffectivePixelsPerSecond(AnimationChainSave?)"/> per chain) whenever multiple
    /// chains are rendered together — e.g. the multi-track group-preview timeline — so a frame of
    /// equal duration renders at equal width in every row instead of each chain scaling independently
    /// to its own shortest frame.
    /// </summary>
    public static double ComputeSharedEffectivePixelsPerSecond(IEnumerable<AnimationChainSave?> chains, FrameLengthUnit unit)
    {
        double minDuration = double.MaxValue;
        foreach (var chain in chains)
        {
            if (chain is null)
                continue;

            foreach (var frame in chain.Frames)
            {
                if (frame.FrameLength > 0)
                    minDuration = Math.Min(minDuration, unit.ToSeconds(frame.FrameLength));
            }
        }

        if (minDuration == double.MaxValue)
            return PixelsPerSecond;

        return Math.Max(PixelsPerSecond, MinCellWidth / minDuration);
    }

    /// <summary>
    /// Total play duration of <paramref name="chain"/> in seconds — the sum of every frame's
    /// <see cref="AnimationFrameSave.FrameLength"/>, which is in the file's unit (matching
    /// the editor's per-frame display), converted to seconds.
    /// Returns 0 for a null or empty chain.
    /// </summary>
    public static float TotalSeconds(AnimationChainSave? chain, FrameLengthUnit unit) =>
        (float)unit.ToSeconds(TotalLength(chain));

    /// <summary>Sum of every frame length in the chain, in the file's own unit.</summary>
    private static float TotalLength(AnimationChainSave? chain)
    {
        if (chain is null)
            return 0f;

        float total = 0f;
        foreach (var frame in chain.Frames)
            total += frame.FrameLength;
        return total;
    }

    /// <summary>Sum of <see cref="TotalLength(AnimationChainSave?)"/> across every chain in the list.</summary>
    public static float TotalSeconds(AnimationChainListSave? acls) =>
        (float)FrameLengthUnit.Of(acls).ToSeconds(TotalLength(acls));

    private static float TotalLength(AnimationChainListSave? acls)
    {
        if (acls is null)
            return 0f;

        float total = 0f;
        foreach (var chain in acls.AnimationChains)
            total += TotalLength(chain);
        return total;
    }

    /// <summary>Total play time of a chain in the file's own unit, formatted for display: <c>"1.50s"</c> or <c>"1500ms"</c>.</summary>
    public static string FormatTotal(AnimationChainSave? chain, FrameLengthUnit unit) => unit.Format(TotalLength(chain));

    /// <summary>Total play time of every chain in the file, in its own unit, formatted for display.</summary>
    public static string FormatTotal(AnimationChainListSave? acls) => FrameLengthUnit.Of(acls).Format(TotalLength(acls));

    /// <summary>Formats a duration in seconds for display, e.g. <c>1.5</c> → <c>"1.50s"</c>.</summary>
    public static string FormatSeconds(float seconds) => $"{seconds:0.00}s";

    public static List<TimelineFrameVm> BuildFrameItems(AnimationChainSave? chain, FrameLengthUnit unit) =>
        BuildFrameItems(chain, ComputeEffectivePixelsPerSecond(chain, unit), unit);

    /// <summary>
    /// Builds frame cells for <paramref name="chain"/> at an explicit <paramref name="pixelsPerSecond"/>
    /// rate instead of one derived from this chain alone — use with
    /// <see cref="ComputeEffectivePixelsPerSecond(IEnumerable{AnimationChainSave?})"/> so multiple
    /// chains rendered together (e.g. group-preview timeline rows) share one time scale.
    /// </summary>
    public static List<TimelineFrameVm> BuildFrameItems(AnimationChainSave? chain, double pixelsPerSecond, FrameLengthUnit unit)
    {
        if (chain is null)
            return [];

        var result = new List<TimelineFrameVm>(chain.Frames.Count);
        for (int i = 0; i < chain.Frames.Count; i++)
        {
            var length = Math.Max(0.0, unit.ToSeconds(chain.Frames[i].FrameLength));
            var width = length > 0 ? length * pixelsPerSecond : MinCellWidth;
            result.Add(new TimelineFrameVm(i, width) { EventNames = chain.Frames[i].Events.Select(e => e.Name).ToArray() });
        }

        return result;
    }
}
