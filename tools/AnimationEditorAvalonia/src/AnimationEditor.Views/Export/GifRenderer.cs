using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Export.Gif;
using AnimationEditor.App.Controls;
using AnimationEditor.Views.Services;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;

namespace AnimationEditor.Views.Export;

/// <summary>
/// Draws the composed frames of a GIF with the preview's own frame drawing (crop, flip, color,
/// opacity), minus shapes and guides, and hands them to <see cref="GifEncoder"/>. Runs entirely
/// on private images and surfaces, so it is safe off the UI thread.
/// </summary>
public static class GifRenderer
{
    private const long MaxCanvasPixels = 16_000_000;
    private const long MaxTotalBytes = 1L << 30;

    private readonly record struct Placement(
        GifLayer Layer, SKImage Image, ResolvedFrameColor Color, int Left, int Top, int Width, int Height);

    /// <param name="resolveTexturePath">Maps a frame to its texture file; <c>null</c> skips that layer.</param>
    /// <param name="offsetMultiplier">The editor's unit multiplier from stored offsets to pixels.</param>
    /// <exception cref="InvalidOperationException">Nothing drawable, or the GIF would be too large.</exception>
    public static byte[] Render(
        IReadOnlyList<GifSourceChain> sources,
        Func<AnimationFrameSave, string?> resolveTexturePath,
        float offsetMultiplier,
        GifExportOptions options)
    {
        if (options.Scale < 1 || options.Scale > GifExportOptions.MaxScale)
            throw new ArgumentOutOfRangeException(nameof(options), $"Scale must be 1 to {GifExportOptions.MaxScale}.");

        var timeline = GifTimelinePlanner.Plan(sources, options.FrameRate);
        if (timeline.Count == 0)
            throw new InvalidOperationException("The selected animations have no frames to export.");

        var bitmaps = new List<SKBitmap>();
        var images = new Dictionary<string, SKImage?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            SKImage? Load(string? path)
            {
                if (path is null) return null;
                if (images.TryGetValue(path, out var cached)) return cached;
                var bitmap = SkiaFileDecoder.DecodeFile(path);
                SKImage? image = null;
                if (bitmap is not null)
                {
                    bitmaps.Add(bitmap);
                    image = SKImage.FromBitmap(bitmap);
                }
                images[path] = image;
                return image;
            }

            var placements = timeline
                .Select(frame => frame.Layers
                    .Select(layer => Place(layer, Load(resolveTexturePath(layer.Frame)), offsetMultiplier, options.Scale))
                    .OfType<Placement>().ToList())
                .ToList();
            if (placements.All(p => p.Count == 0))
                throw new InvalidOperationException("None of the animations' textures could be loaded.");

            int minLeft = placements.SelectMany(p => p).Min(p => p.Left);
            int minTop = placements.SelectMany(p => p).Min(p => p.Top);
            int width = placements.SelectMany(p => p).Max(p => p.Left + p.Width) - minLeft;
            int height = placements.SelectMany(p => p).Max(p => p.Top + p.Height) - minTop;
            if ((long)width * height > MaxCanvasPixels)
                throw new InvalidOperationException($"The GIF would be {width} x {height} pixels. Lower the scale.");
            if ((long)width * height * 4 * timeline.Count > MaxTotalBytes)
                throw new InvalidOperationException($"The GIF would need too much memory ({timeline.Count} frames of {width} x {height}). Lower the scale or frame rate.");

            var background = options.Transparent ? SKColors.Transparent : new SKColor(options.BackgroundArgb).WithAlpha(255);
            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            var readback = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var pixels = new SKBitmap(readback);

            var frames = new List<GifRgbaFrame>(timeline.Count);
            for (int i = 0; i < timeline.Count; i++)
            {
                var canvas = surface.Canvas;
                canvas.Clear(background);
                foreach (var p in placements[i])
                {
                    PreviewControl.DrawFrameCore(
                        canvas, p.Layer.Frame, p.Color, p.Image,
                        cx: p.Left - minLeft + p.Width / 2f, cy: p.Top - minTop + p.Height / 2f,
                        zoom: options.Scale, alpha: 1f, showOutline: false);
                }
                if (!surface.ReadPixels(readback, pixels.GetPixels(), pixels.RowBytes, 0, 0))
                    throw new InvalidOperationException("Could not read back a rendered GIF frame.");
                frames.Add(new GifRgbaFrame(pixels.Bytes, timeline[i].DelayCentiseconds));
            }

            return GifEncoder.Encode(width, height, frames, options.Transparent);
        }
        finally
        {
            foreach (var image in images.Values) image?.Dispose();
            foreach (var bitmap in bitmaps) bitmap.Dispose();
        }
    }

    // Snaps the sprite's footprint to whole output pixels so a pixel-art sprite is resampled evenly
    // (a half-pixel offset would double some source pixels and not others).
    private static Placement? Place(GifLayer layer, SKImage? image, float offsetMultiplier, int scale)
    {
        if (image is null) return null;

        var frame = layer.Frame;
        var (_, _, sw, sh) = PreviewControl.ComputeSourceRect(frame, image.Width, image.Height);
        int width = (frame.FlipDiagonal ? sh : sw) * scale;
        int height = (frame.FlipDiagonal ? sw : sh) * scale;
        float centerX = layer.OffsetX * offsetMultiplier * scale;
        float centerY = -layer.OffsetY * offsetMultiplier * scale;
        int left = (int)Math.Floor(centerX - width / 2f + 0.5f);
        int top = (int)Math.Floor(centerY - height / 2f + 0.5f);

        int index = layer.Chain.Frames.IndexOf(frame);
        var color = EffectiveFrameColor.Resolve(layer.Chain.Frames, index);
        return new Placement(layer, image, color, left, top, width, height);
    }
}
