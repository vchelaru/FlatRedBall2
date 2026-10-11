using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Export;

/// <summary>An animation to export and whether its offsets ease between frames (the preview's Interpolate toggle).</summary>
public sealed record ExportSourceChain(AnimationChainSave Chain, bool Interpolate);

/// <summary>One animation's contribution to a composed frame: its current frame at its (possibly eased) offset.</summary>
public sealed record ExportLayer(AnimationChainSave Chain, AnimationFrameSave Frame, float OffsetX, float OffsetY);

/// <summary>A composed frame: the layers to draw back to front, shown for <see cref="DelayTicks"/> (the format's delay unit).</summary>
public sealed record ExportTimelineFrame(IReadOnlyList<ExportLayer> Layers, int DelayTicks);

/// <summary>
/// Turns the selected animations into the composed frames of an animated image. The result runs as
/// long as the longest animation; shorter looping animations cycle until then and non-looping ones
/// hold their last frame, matching the group preview.
/// </summary>
public static class ExportTimelinePlanner
{
    /// <summary>Returns an empty list when no animation has a frame.</summary>
    public static IReadOnlyList<ExportTimelineFrame> Plan(
        IReadOnlyList<ExportSourceChain> sources, int frameRate, int ticksPerSecond, int minTicks)
    {
        var playable = sources.Where(s => s.Chain.Frames.Count > 0).ToList();
        if (playable.Count == 0) return Array.Empty<ExportTimelineFrame>();

        double duration = playable.Max(s => ChainClock.TotalLength(s.Chain));
        int[] delays = ExportTiming.PlanDelays(duration, frameRate, ticksPerSecond, minTicks);

        var frames = new List<ExportTimelineFrame>();
        List<ExportLayer>? previous = null;
        for (int step = 0; step < delays.Length; step++)
        {
            double time = (double)step / frameRate;
            var layers = new List<ExportLayer>(playable.Count);
            foreach (var (chain, interpolate) in playable)
            {
                var (index, elapsed) = ChainClock.Locate(chain, time);
                var (x, y) = OffsetInterpolator.ComputeOffset(chain, index, elapsed, interpolate);
                layers.Add(new ExportLayer(chain, chain.Frames[index], x, y));
            }

            if (previous is not null && SameLayers(previous, layers))
            {
                var last = frames[^1];
                frames[^1] = last with { DelayTicks = last.DelayTicks + delays[step] };
            }
            else
            {
                frames.Add(new ExportTimelineFrame(layers, delays[step]));
            }
            previous = layers;
        }
        return frames;
    }

    private static bool SameLayers(List<ExportLayer> a, List<ExportLayer> b)
    {
        for (int i = 0; i < a.Count; i++)
        {
            if (!ReferenceEquals(a[i].Frame, b[i].Frame) || a[i].OffsetX != b[i].OffsetX || a[i].OffsetY != b[i].OffsetY)
                return false;
        }
        return true;
    }
}
