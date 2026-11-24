using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.App.Controls;
using AnimationEditor.App.Theming;
using AnimationEditor.Core.Export;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;

namespace AnimationEditor.Views.Export;

/// <summary>A canvas rectangle in offset space: <paramref name="X"/>, <paramref name="Y"/> is the lower-left corner, Y up like frame offsets.</summary>
public readonly record struct StillBounds(float X, float Y, float Width, float Height);

/// <summary>How <see cref="StillImageRenderer"/> draws. A null <paramref name="Bounds"/> fits the canvas to every sprite of every image.</summary>
public sealed record StillRenderOptions(int Scale, bool Transparent, uint BackgroundArgb, bool Grid, StillBounds? Bounds, bool Shapes = false);

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
    private const long MaxSheetPixels = 32_000_000;
    private const int SheetGap = 8;
    private const float LabelSize = 12f;
    private const int LabelStrip = 20;
    private static readonly SKColor SheetBackground = new(32, 32, 32);

    /// <exception cref="InvalidOperationException">Nothing drawable, or the result would be too large.</exception>
    public static StillImages Render(
        IReadOnlyList<ExportTimelineFrame> frames, Func<AnimationFrameSave, string?> resolveTexturePath, StillRenderOptions options)
    {
        var rendered = RenderFrames(frames, resolveTexturePath, options);
        var pngs = new List<byte[]>(rendered.Frames.Count);
        foreach (var frame in rendered.Frames) pngs.Add(Encode(frame.Rgba, rendered.Width, rendered.Height));
        return new StillImages(rendered.Width, rendered.Height, pngs);
    }

    /// <summary>
    /// All <paramref name="frames"/> in one contact-sheet PNG, in rows, with each <paramref name="labels"/> entry printed under its tile.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing drawable, or the sheet would be too large (lower the scale).</exception>
    public static StillImages RenderSheet(
        IReadOnlyList<ExportTimelineFrame> frames, IReadOnlyList<string> labels,
        Func<AnimationFrameSave, string?> resolveTexturePath, StillRenderOptions options)
    {
        var rendered = RenderFrames(frames, resolveTexturePath, options);
        var (columns, rows, width, height) = SheetLayout(rendered.Frames.Count, rendered.Width, rendered.Height);
        if (width > MaxDimension || height > MaxDimension || (long)width * height > MaxSheetPixels)
            throw new InvalidOperationException(
                $"The contact sheet would be {width} x {height} pixels (limit {MaxDimension} per side, {MaxSheetPixels / 1_000_000} million in all). Lower --scale.");

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SheetBackground);
        using var font = new SKFont { Size = LabelSize };
        using var text = new SKPaint { Color = SKColors.White, IsAntialias = true };
        for (int i = 0; i < rendered.Frames.Count; i++)
        {
            float x = SheetGap + (i % columns) * (rendered.Width + SheetGap);
            float y = SheetGap + (i / columns) * (rendered.Height + LabelStrip + SheetGap);
            using var tile = new SKBitmap(new SKImageInfo(rendered.Width, rendered.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            System.Runtime.InteropServices.Marshal.Copy(rendered.Frames[i].Rgba, 0, tile.GetPixels(), rendered.Frames[i].Rgba.Length);
            using var tileImage = SKImage.FromBitmap(tile);
            canvas.DrawImage(tileImage, x, y, new SKSamplingOptions(SKFilterMode.Nearest));
            canvas.DrawText(i < labels.Count ? labels[i] : "", x, y + rendered.Height + LabelSize + 3f, SKTextAlign.Left, font, text);
        }

        using var readback = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (!surface.ReadPixels(readback.Info, readback.GetPixels(), readback.RowBytes, 0, 0))
            throw new InvalidOperationException("Could not read back the contact sheet.");
        return new StillImages(width, height, [Encode(readback.Bytes, width, height)]);
    }

    /// <summary>Tiles per row (about square, wider tiles first), rows, and the sheet size in pixels.</summary>
    internal static (int Columns, int Rows, int Width, int Height) SheetLayout(int count, int tileWidth, int tileHeight)
    {
        int columns = (int)Math.Ceiling(Math.Sqrt(count));
        int rows = (count + columns - 1) / columns;
        return (columns, rows, SheetGap + columns * (tileWidth + SheetGap), SheetGap + rows * (tileHeight + LabelStrip + SheetGap));
    }

    private static RenderedAnimation RenderFrames(
        IReadOnlyList<ExportTimelineFrame> frames, Func<AnimationFrameSave, string?> resolveTexturePath, StillRenderOptions options)
    {
        var export = new AnimationExportOptions(1, options.Transparent, options.BackgroundArgb, options.Scale);
        var canvas = options.Bounds is { } b ? new CanvasRect(b.X, b.Y, b.Width, b.Height) : (CanvasRect?)null;
        float lightness = options.Transparent ? 0f : Lightness(options.BackgroundArgb);
        var rendered = ExportFrameRenderer.Render(
            frames, resolveTexturePath, 1f, export, MaxDimension, canvas,
            options.Grid || options.Shapes
                ? (c, index, ox, oy) =>
                {
                    if (options.Grid) DrawGrid(c, ox, oy, options.Scale, c.DeviceClipBounds, lightness);
                    if (options.Shapes) DrawShapes(c, frames[index], ox, oy, options.Scale);
                }
                : null,
            options.Shapes && canvas is null ? ShapeExtent(frames) : null);
        return rendered;
    }

    // Every shape of every frame, in offset space, with room for its name; null when there are none.
    private static CanvasRect? ShapeExtent(IReadOnlyList<ExportTimelineFrame> frames)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var s in frames.SelectMany(f => f.Layers).SelectMany(l => l.Shapes ?? []))
        {
            if (s.Kind == ExportShapeKind.Polygon && s.Points is { Length: >= 2 } p)
                for (int i = 0; i + 1 < p.Length; i += 2) Include(s.X + p[i], s.Y + p[i + 1], 0, 0);
            else if (s.Kind == ExportShapeKind.Rect) Include(s.X, s.Y, s.Param1, s.Param2);
            else Include(s.X, s.Y, s.Param1, s.Param1);
        }
        if (minX > maxX) return null;
        const float margin = 4f, LabelRoom = 40f;
        return new CanvasRect(minX - margin, minY - margin, maxX - minX + 2 * margin + LabelRoom, maxY - minY + 2 * margin + LabelSize);

        void Include(float x, float y, float halfW, float halfH)
        {
            minX = Math.Min(minX, x - halfW); maxX = Math.Max(maxX, x + halfW);
            minY = Math.Min(minY, y - halfH); maxY = Math.Max(maxY, y + halfH);
        }
    }

    private static byte[] Encode(byte[] rgba, int width, int height)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(rgba, 0, bitmap.GetPixels(), rgba.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    // The preview's own shape drawing (colors, polygons, circles), then each shape's name at its top-left corner.
    private static void DrawShapes(SKCanvas canvas, ExportTimelineFrame frame, float originX, float originY, int scale)
    {
        var shapes = frame.Layers.SelectMany(l => l.Shapes ?? []).ToList();
        if (shapes.Count == 0) return;

        var infos = shapes.Select(s => new PreviewControl.PreviewShapeInfo(
            s.Kind switch { ExportShapeKind.Rect => PreviewControl.PreviewShapeKind.Rect, ExportShapeKind.Circle => PreviewControl.PreviewShapeKind.Circle, _ => PreviewControl.PreviewShapeKind.Polygon },
            s.X, s.Y, s.Param1, s.Param2, IsSelected: false, Points: s.Points, IsClosed: s.IsClosed)).ToArray();
        PreviewControl.DrawShapes(canvas, infos, originX, originY, scale);

        using var font = new SKFont { Size = LabelSize };
        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 200), IsAntialias = true };
        using var fill = new SKPaint { Color = EditorColors.Shape.WithAlpha(255), IsAntialias = true };
        foreach (var s in shapes)
        {
            if (string.IsNullOrEmpty(s.Name)) continue;
            float sx = originX + s.X * scale, sy = originY - s.Y * scale;
            float left = sx, top = sy;
            switch (s.Kind)
            {
                case ExportShapeKind.Rect: left = sx - s.Param1 * scale; top = sy - s.Param2 * scale; break;
                case ExportShapeKind.Circle: left = sx - s.Param1 * scale; top = sy - s.Param1 * scale; break;
                case ExportShapeKind.Polygon when s.Points is { Length: >= 2 } p:
                    left = float.MaxValue; top = float.MaxValue;
                    for (int i = 0; i + 1 < p.Length; i += 2)
                    {
                        left = Math.Min(left, sx + p[i] * scale);
                        top = Math.Min(top, sy - p[i + 1] * scale);
                    }
                    break;
            }
            canvas.DrawText(s.Name, left + 1f, top - 2f, SKTextAlign.Left, font, shadow);
            canvas.DrawText(s.Name, left, top - 3f, SKTextAlign.Left, font, fill);
        }
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
