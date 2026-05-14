using System;
using System.Collections.Generic;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.ViewModels;

public static class TimelineBuilder
{
    /// <summary>
    /// Minimum cell width in pixels. Applied to every frame so the thumbnail (22px image + 1px border each side = 24px)
    /// always fits. Short frames below this threshold are rendered at MinCellWidth; the playhead still
    /// traverses them in their actual FrameLength duration (faster than PixelsPerSecond for very short frames),
    /// which is acceptable since constant speed is only guaranteed for frames whose natural width exceeds the minimum.
    /// </summary>
    public const double MinCellWidth = 24.0;
    public const double PixelsPerSecond = 120.0;

    public static List<TimelineFrameVm> BuildFrameItems(AnimationChainSave? chain)
    {
        if (chain is null)
            return [];

        var result = new List<TimelineFrameVm>(chain.Frames.Count);
        for (int i = 0; i < chain.Frames.Count; i++)
        {
            var length = Math.Max(0f, chain.Frames[i].FrameLength);
            var width = Math.Max(length * PixelsPerSecond, MinCellWidth);
            result.Add(new TimelineFrameVm(i, width));
        }

        return result;
    }
}
