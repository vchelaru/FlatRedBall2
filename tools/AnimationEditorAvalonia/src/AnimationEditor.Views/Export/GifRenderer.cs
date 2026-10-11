using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Export;
using AnimationEditor.Core.Export.Gif;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Views.Export;

/// <summary>Renders the composed frames and hands them to <see cref="GifEncoder"/>.</summary>
public static class GifRenderer
{
    /// <param name="resolveTexturePath">Maps a frame to its texture file; <c>null</c> skips that layer.</param>
    /// <param name="offsetMultiplier">The editor's unit multiplier from stored offsets to pixels.</param>
    /// <exception cref="InvalidOperationException">Nothing drawable, or the GIF would be too large.</exception>
    public static byte[] Render(
        IReadOnlyList<ExportSourceChain> sources,
        Func<AnimationFrameSave, string?> resolveTexturePath,
        float offsetMultiplier,
        AnimationExportOptions options)
    {
        var timeline = ExportTimelinePlanner.Plan(
            sources, options.FrameRate, GifTiming.TicksPerSecond, GifTiming.MinDelayCentiseconds);
        var rendered = ExportFrameRenderer.Render(timeline, resolveTexturePath, offsetMultiplier, options, GifEncoder.MaxDimension);

        return GifEncoder.Encode(
            rendered.Width, rendered.Height,
            rendered.Frames.Select(f => new GifRgbaFrame(f.Rgba, f.DelayTicks)).ToList(),
            options.Transparent);
    }
}
