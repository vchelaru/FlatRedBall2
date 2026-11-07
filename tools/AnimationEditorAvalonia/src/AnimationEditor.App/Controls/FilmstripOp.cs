using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace AnimationEditor.App.Controls;

/// <summary>One cell of a block's filmstrip, in layout units. <see cref="SpriteWidth"/> is 0 for a cell with nothing to copy from the atlas.</summary>
internal readonly record struct FilmstripCellDraw(
    double CellLeft, double CellRight, bool DrawsBoundary, bool IsMerged,
    AtlasEntry Sprite, bool HasSprite, double SpriteLeft, double SpriteTop);

/// <summary>
/// Draws a block's whole filmstrip in one operation on the render thread: every cell's sprite is a one-to-one copy from
/// the atlas page (no clip, no resampling), followed by the merged-group bars and the cell boundary lines. Geometry is
/// snapped to device pixels with <see cref="FilmstripPlacement"/>. The draws are grouped by kind, not interleaved per
/// cell, and the sprites of one page go out as a single <c>DrawAtlas</c>, so the GPU sees one batch per kind instead of
/// hundreds of alternating draws.
/// </summary>
internal sealed class FilmstripOp : ICustomDrawOperation
{
    // Skia keeps the atlas pages as textures across frames only when its cache is big enough to hold them (the same
    // budget the texture viewport asks for).
    private const long GpuResourceCacheBytes = 512L * 1024 * 1024;
    private static readonly SKSamplingOptions Nearest = new(SKFilterMode.Nearest);
    private static readonly SKColor Amber = new(0xFF, 0xB4, 0x54);

    private readonly FilmstripCellDraw[] _cells;
    private readonly SKImage[] _pages;
    private readonly double _filmTop, _filmBottom;
    private readonly SKColor _boundary;
    private readonly FilmstripProbe? _probe;
    private readonly bool _perCell;
    private readonly bool _waitForGpu;
    private readonly BlockPaint? _block;
    private double _opacity = 1;   // what an enclosing PushOpacity asks of every draw, read from the lease each time

    public FilmstripOp(FilmstripCellDraw[] cells, SKImage[] pages, Rect film, Color boundary,
        FilmstripProbe? probe = null, bool perCell = false, bool waitForGpu = false, BlockPaint? block = null)
    {
        _cells = cells;
        _pages = pages;
        _filmTop = film.Top;
        _filmBottom = film.Bottom;
        _boundary = new SKColor(boundary.R, boundary.G, boundary.B, boundary.A);
        _probe = probe;
        _perCell = perCell;
        _waitForGpu = waitForGpu;
        _block = block;
        Bounds = block is null ? film : block.Body.Inflate(2);
    }

    public Rect Bounds { get; }
    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation? other) => false;
    public void Dispose() { }

    public void Render(ImmediateDrawingContext context)
    {
        var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>()?.Lease();
        if (lease is null) return;
        using (lease)
        {
            var gr = lease.GrContext;
            _opacity = lease.CurrentOpacity;
            if (gr is not null && gr.GetResourceCacheLimit() < GpuResourceCacheBytes)
                gr.SetResourceCacheLimit(GpuResourceCacheBytes);
            long start = Stopwatch.GetTimestamp();
            if (_block is not null) DrawBlockBackdrop(lease.SkCanvas, _block);
            if (_perCell) DrawPerCell(lease.SkCanvas);
            else DrawBatched(lease.SkCanvas);
            if (_block is not null) DrawBlockFront(lease.SkCanvas, _block);
            if (_waitForGpu) gr?.Flush(submit: true, synchronous: true);
            long elapsed = Stopwatch.GetTimestamp() - start;
            if (_probe is not null)
            {
                int resources = 0;
                long bytes = 0;
                if (gr is not null) gr.GetResourceCacheUsage(out resources, out bytes);
                _probe.Report(elapsed, resources, bytes, gr?.GetResourceCacheLimit() ?? 0);
            }
        }
    }

    private static SKRect ToSk(Avalonia.Rect r) => new((float)r.Left, (float)r.Top, (float)r.Right, (float)r.Bottom);

    // In layout units under Avalonia's own transform and clip, with the same anti-aliasing Avalonia's drawing would use.
    // Under a dimmed track every sprite is multiplied by white at the track's opacity.
    private void DrawAtlas(SKCanvas canvas, SKImage page, SKRect[] sources, SKRotationScaleMatrix[] moves)
    {
        if (_opacity >= 1)
        {
            canvas.DrawAtlas(page, sources, moves, Nearest);
            return;
        }
        var tint = new SKColor(255, 255, 255, (byte)(255 * _opacity));
        var colors = new SKColor[sources.Length];
        Array.Fill(colors, tint);
        canvas.DrawAtlas(page, sources, moves, colors, SKBlendMode.Modulate, Nearest, new SKRect(-1e6f, -1e6f, 1e6f, 1e6f), null);
    }

    // Null at full opacity, so the common draw carries no paint at all.
    private SKPaint? SpritePaint() => _opacity >= 1 ? null : new SKPaint { Color = new SKColor(255, 255, 255, (byte)(255 * _opacity)) };

    private SKColor Dim(SKColor color) => _opacity >= 1 ? color : color.WithAlpha((byte)(color.Alpha * _opacity));

    private void DrawBlockBackdrop(SKCanvas canvas, BlockPaint block)
    {
        using (var fill = new SKPaint { IsAntialias = true, Color = Dim(block.Fill) })
            canvas.DrawRoundRect(ToSk(block.Body), (float)block.Radius, (float)block.Radius, fill);
        if (block.Film is { } film)
            using (var background = new SKPaint { IsAntialias = true, Color = Dim(block.FilmBackground) })
                canvas.DrawRect(ToSk(film), background);
    }

    private void DrawBlockFront(SKCanvas canvas, BlockPaint block)
    {
        using (var outline = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = block.OutlineThickness, Color = Dim(block.Outline) })
            canvas.DrawRoundRect(ToSk(block.Body), (float)block.Radius, (float)block.Radius, outline);
        if (block.MarkXs.Length == 0) return;
        // A dashed one-pixel line, two on and two off from the top, drawn as the little rectangles it is.
        using var mark = new SKPaint { IsAntialias = true, Color = Dim(block.Mark) };
        foreach (double x in block.MarkXs)
            for (double y = block.MarkTop; y < block.MarkBottom; y += 4)
                canvas.DrawRect(new SKRect((float)(x - 0.5), (float)y, (float)(x + 0.5), (float)Math.Min(y + 2, block.MarkBottom)), mark);
    }

    private readonly record struct Pixels(CellPlacement Place, FilmstripCellDraw Cell);

    private List<Pixels> Place(SKCanvas canvas, out double scale)
    {
        var matrix = canvas.TotalMatrix;
        scale = matrix.ScaleX;
        double originX = matrix.TransX, originY = matrix.TransY;
        var placed = new List<Pixels>(_cells.Length);
        foreach (var cell in _cells)
        {
            var placement = FilmstripPlacement.Place(originX, originY, scale, cell.CellLeft, cell.CellRight, _filmTop, _filmBottom,
                cell.SpriteLeft, cell.SpriteTop, cell.HasSprite ? cell.Sprite.Width : 0, cell.HasSprite ? cell.Sprite.Height : 0);
            placed.Add(new Pixels(placement, cell));
        }
        return placed;
    }

    private static SKRect SourceOf(in Pixels p) =>
        new(p.Cell.Sprite.X + p.Place.SourceX, p.Cell.Sprite.Y + p.Place.SourceY,
            p.Cell.Sprite.X + p.Place.SourceX + p.Place.SpriteRight - p.Place.SpriteLeft,
            p.Cell.Sprite.Y + p.Place.SourceY + p.Place.SpriteBottom - p.Place.SpriteTop);

    // The sprites of each page as one atlas draw, then every bar, then every boundary line.
    private void DrawBatched(SKCanvas canvas)
    {
        canvas.Save();
        var placed = Place(canvas, out double scale);
        canvas.ResetMatrix();

        for (int page = 0; page < _pages.Length; page++)
        {
            var sources = new List<SKRect>();
            var moves = new List<SKRotationScaleMatrix>();
            foreach (var p in placed)
            {
                if (!p.Place.HasSprite || p.Cell.Sprite.Page != page) continue;
                sources.Add(SourceOf(p));
                moves.Add(new SKRotationScaleMatrix(1, 0, p.Place.SpriteLeft, p.Place.SpriteTop));
            }
            if (sources.Count > 0)
                DrawAtlas(canvas, _pages[page], sources.ToArray(), moves.ToArray());
        }

        using (var amber = new SKPaint { Color = Dim(Amber) })
            foreach (var p in placed)
            {
                if (!p.Cell.IsMerged) continue;
                int barTop = Math.Max(p.Place.CellTop, p.Place.CellBottom - Math.Max(1, (int)Math.Round(2 * scale)));
                canvas.DrawRect(new SKRect(p.Place.CellLeft, barTop, p.Place.CellRight, p.Place.CellBottom), amber);
            }

        int width = Math.Max(1, (int)Math.Round(scale));
        using (var line = new SKPaint { Color = Dim(_boundary) })
            foreach (var p in placed)
                if (p.Cell.DrawsBoundary)
                    canvas.DrawRect(new SKRect(p.Place.CellLeft, p.Place.CellTop, p.Place.CellLeft + width, p.Place.CellBottom), line);
        canvas.Restore();
    }

    // The earlier shape, kept to compare against on the GPU: one image draw, then a bar and a line, per cell.
    private void DrawPerCell(SKCanvas canvas)
    {
        canvas.Save();
        var placed = Place(canvas, out double scale);
        canvas.ResetMatrix();
        using var amber = new SKPaint { Color = Dim(Amber) };
        using var line = new SKPaint { Color = Dim(_boundary) };
        foreach (var p in placed)
        {
            if (p.Place.HasSprite && p.Cell.Sprite.Page < _pages.Length)
                canvas.DrawImage(_pages[p.Cell.Sprite.Page], SourceOf(p),
                    new SKRect(p.Place.SpriteLeft, p.Place.SpriteTop, p.Place.SpriteRight, p.Place.SpriteBottom), Nearest, SpritePaint());
            if (p.Cell.IsMerged)
            {
                int barTop = Math.Max(p.Place.CellTop, p.Place.CellBottom - Math.Max(1, (int)Math.Round(2 * scale)));
                canvas.DrawRect(new SKRect(p.Place.CellLeft, barTop, p.Place.CellRight, p.Place.CellBottom), amber);
            }
            if (p.Cell.DrawsBoundary)
            {
                int width = Math.Max(1, (int)Math.Round(scale));
                canvas.DrawRect(new SKRect(p.Place.CellLeft, p.Place.CellTop, p.Place.CellLeft + width, p.Place.CellBottom), line);
            }
        }
        canvas.Restore();
    }
}
