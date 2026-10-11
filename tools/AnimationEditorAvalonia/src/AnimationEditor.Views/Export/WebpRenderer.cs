using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Export;
using AnimationEditor.Core.Export.Webp;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;

namespace AnimationEditor.Views.Export;

/// <summary>
/// Renders the composed frames and encodes them as one animated WebP with Skia's encoder, which
/// stores only the changed region of each frame. Unlike GIF it keeps full alpha and 60fps timing.
/// </summary>
public static class WebpRenderer
{
    /// <param name="resolveTexturePath">Maps a frame to its texture file; <c>null</c> skips that layer.</param>
    /// <param name="offsetMultiplier">The editor's unit multiplier from stored offsets to pixels.</param>
    /// <exception cref="InvalidOperationException">Nothing drawable, the image would be too large, or encoding failed.</exception>
    public static byte[] Render(
        IReadOnlyList<ExportSourceChain> sources,
        Func<AnimationFrameSave, string?> resolveTexturePath,
        float offsetMultiplier,
        AnimationExportOptions options)
    {
        if (options.FrameRate < 1 || options.FrameRate > WebpTiming.MaxFrameRate)
            throw new ArgumentOutOfRangeException(nameof(options), $"WebP export supports 1 to {WebpTiming.MaxFrameRate} fps.");

        var timeline = ExportTimelinePlanner.Plan(
            sources, options.FrameRate, WebpTiming.TicksPerSecond, WebpTiming.MinDelayMilliseconds);
        var rendered = ExportFrameRenderer.Render(timeline, resolveTexturePath, offsetMultiplier, options, WebpTiming.MaxDimension);

        var info = new SKImageInfo(rendered.Width, rendered.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmaps = new List<SKBitmap>(rendered.Frames.Count);
        try
        {
            foreach (var frame in rendered.Frames)
            {
                var bitmap = new SKBitmap(info);
                bitmaps.Add(bitmap);
                System.Runtime.InteropServices.Marshal.Copy(frame.Rgba, 0, bitmap.GetPixels(), frame.Rgba.Length);
            }

            var frames = bitmaps
                .Select((bitmap, i) => new SKWebpEncoderFrame(bitmap, TimeSpan.FromMilliseconds(rendered.Frames[i].DelayTicks)))
                .ToArray();
            var encoderOptions = options.Lossless
                ? new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100)
                : new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, Math.Clamp(options.Quality, 0, 100));

            using var data = SKWebpEncoder.EncodeAnimated(frames, encoderOptions)
                ?? throw new InvalidOperationException("The WebP encoder could not encode these frames.");
            return data.ToArray();
        }
        finally
        {
            foreach (var bitmap in bitmaps) bitmap.Dispose();
        }
    }
}
