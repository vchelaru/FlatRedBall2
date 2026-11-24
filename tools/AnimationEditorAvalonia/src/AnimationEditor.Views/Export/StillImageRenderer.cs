using System;
using System.Collections.Generic;
using AnimationEditor.Core.Export;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;

namespace AnimationEditor.Views.Export;

/// <summary>A canvas rectangle in offset space: <paramref name="X"/>, <paramref name="Y"/> is the lower-left corner, Y up like frame offsets.</summary>
public readonly record struct StillBounds(float X, float Y, float Width, float Height);

/// <summary>How <see cref="StillImageRenderer"/> draws. A null <paramref name="Bounds"/> fits the canvas to every sprite of every image.</summary>
public sealed record StillRenderOptions(int Scale, bool Transparent, uint BackgroundArgb, bool Grid, StillBounds? Bounds);

/// <summary>PNG data of the rendered images; all share one <see cref="Width"/> by <see cref="Height"/>.</summary>
public sealed record StillImages(int Width, int Height, IReadOnlyList<byte[]> Pngs);

/// <summary>
/// Renders composed frames to PNG data, with the same frame drawing as the animated image export
/// (<see cref="ExportFrameRenderer"/>) and no window. The canvas is common to all images so they line up.
/// </summary>
public static class StillImageRenderer
{
    private const int MaxDimension = 16384;
    private const float GridStep = 16f;

    /// <exception cref="InvalidOperationException">Nothing drawable, or the result would be too large.</exception>
    public static StillImages Render(
        IReadOnlyList<ExportTimelineFrame> frames, Func<AnimationFrameSave, string?> resolveTexturePath, StillRenderOptions options)
    {
        var export = new AnimationExportOptions(1, options.Transparent, options.BackgroundArgb, options.Scale);
        var canvas = options.Bounds is { } b ? new CanvasRect(b.X, b.Y, b.Width, b.Height) : (CanvasRect?)null;
        float lightness = options.Transparent ? 0f : Lightness(options.BackgroundArgb);
        var rendered = ExportFrameRenderer.Render(
            frames, resolveTexturePath, 1f, export, MaxDimension, canvas,
            options.Grid ? (c, ox, oy) => DrawGrid(c, ox, oy, options.Scale, c.DeviceClipBounds, lightness) : null);

        var pngs = new List<byte[]>(rendered.Frames.Count);
        foreach (var frame in rendered.Frames)
        {
            using var bitmap = new SKBitmap(new SKImageInfo(rendered.Width, rendered.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            System.Runtime.InteropServices.Marshal.Copy(frame.Rgba, 0, bitmap.GetPixels(), frame.Rgba.Length);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            pngs.Add(data.ToArray());
        }
        return new StillImages(rendered.Width, rendered.Height, pngs);
    }

    private static float Lightness(uint argb) =>
        (((argb >> 16) & 0xFF) * 0.299f + ((argb >> 8) & 0xFF) * 0.587f + (argb & 0xFF) * 0.114f) / 255f;

    // Faint lines every GridStep offset units, and the origin axes on top.
    private static void DrawGrid(SKCanvas canvas, float originX, float originY, int scale, SKRectI bounds, float backgroundLightness)
    {
        byte shade = backgroundLightness > 0.5f ? (byte)0 : (byte)255;
        using var faint = new SKPaint { Color = new SKColor(shade, shade, shade, 64), StrokeWidth = 1f, IsStroke = true, IsAntialias = false };
        float step = GridStep * scale;
        for (float x = originX % step; x < bounds.Right; x += step) canvas.DrawLine(x, 0, x, bounds.Bottom, faint);
        for (float y = originY % step; y < bounds.Bottom; y += step) canvas.DrawLine(0, y, bounds.Right, y, faint);
        using var xAxis = new SKPaint { Color = new SKColor(230, 60, 60, 200), StrokeWidth = 1f, IsStroke = true, IsAntialias = false };
        using var yAxis = new SKPaint { Color = new SKColor(60, 200, 60, 200), StrokeWidth = 1f, IsStroke = true, IsAntialias = false };
        canvas.DrawLine(0, originY, bounds.Right, originY, xAxis);
        canvas.DrawLine(originX, 0, originX, bounds.Bottom, yAxis);
    }
}
