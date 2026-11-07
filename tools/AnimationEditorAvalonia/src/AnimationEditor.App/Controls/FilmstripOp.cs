using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>One cell of a block's filmstrip, in layout units. <see cref="SpriteWidth"/> is 0 for a cell with nothing to copy from the atlas.</summary>
internal readonly record struct FilmstripCellDraw(
    double CellLeft, double CellRight, bool DrawsBoundary, bool IsMerged,
    AtlasEntry Sprite, bool HasSprite, double SpriteLeft, double SpriteTop);

/// <summary>
/// Draws a block's whole filmstrip in one operation on the render thread: every cell's sprite is a one-to-one copy from
/// the atlas page (no clip, no resampling, so a strip of hundreds of cells stays a few GPU batches), followed by the
/// merged-group bars and the cell boundary lines. Cell geometry is snapped to device pixels with
/// <see cref="FilmstripPlacement"/>.
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

    public FilmstripOp(FilmstripCellDraw[] cells, SKImage[] pages, Rect film, Color boundary)
    {
        _cells = cells;
        _pages = pages;
        _filmTop = film.Top;
        _filmBottom = film.Bottom;
        _boundary = new SKColor(boundary.R, boundary.G, boundary.B, boundary.A);
        Bounds = film;
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
            if (lease.GrContext is { } gr && gr.GetResourceCacheLimit() < GpuResourceCacheBytes)
                gr.SetResourceCacheLimit(GpuResourceCacheBytes);
            Draw(lease.SkCanvas);
        }
    }

    private void Draw(SKCanvas canvas)
    {
        // Work in device pixels: the clip stays as Avalonia set it, only the matrix goes.
        var matrix = canvas.TotalMatrix;
        double scale = matrix.ScaleX, originX = matrix.TransX, originY = matrix.TransY;
        canvas.Save();
        canvas.ResetMatrix();
        using var amber = new SKPaint { Color = Amber };
        using var line = new SKPaint { Color = _boundary };
        foreach (var cell in _cells)
        {
            var placement = FilmstripPlacement.Place(originX, originY, scale, cell.CellLeft, cell.CellRight, _filmTop, _filmBottom,
                cell.SpriteLeft, cell.SpriteTop, cell.HasSprite ? cell.Sprite.Width : 0, cell.HasSprite ? cell.Sprite.Height : 0);
            if (placement.HasSprite && cell.Sprite.Page < _pages.Length)
            {
                var source = new SKRect(cell.Sprite.X + placement.SourceX, cell.Sprite.Y + placement.SourceY,
                    cell.Sprite.X + placement.SourceX + placement.SpriteRight - placement.SpriteLeft,
                    cell.Sprite.Y + placement.SourceY + placement.SpriteBottom - placement.SpriteTop);
                canvas.DrawImage(_pages[cell.Sprite.Page], source,
                    new SKRect(placement.SpriteLeft, placement.SpriteTop, placement.SpriteRight, placement.SpriteBottom), Nearest);
            }
            if (cell.IsMerged)
            {
                int barTop = Math.Max(placement.CellTop, placement.CellBottom - Math.Max(1, (int)Math.Round(2 * scale)));
                canvas.DrawRect(new SKRect(placement.CellLeft, barTop, placement.CellRight, placement.CellBottom), amber);
            }
            if (cell.DrawsBoundary)
            {
                int width = Math.Max(1, (int)Math.Round(scale));
                canvas.DrawRect(new SKRect(placement.CellLeft, placement.CellTop, placement.CellLeft + width, placement.CellBottom), line);
            }
        }
        canvas.Restore();
    }
}
