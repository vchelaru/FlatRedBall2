using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Rendering;

/// <summary>A whole-cell region on a <see cref="TileGrid"/>: its top-left cell and its size in cells.</summary>
public readonly record struct TileFootprint(int Column, int Row, int Columns, int Rows);

/// <summary>
/// The geometry of a tile grid laid over a texture, in Tiled's terms: cells of
/// <see cref="CellWidth"/>x<see cref="CellHeight"/> pixels, the first cell inset by
/// <see cref="Margin"/> from the texture's top-left, and <see cref="Spacing"/> pixels of gap
/// between neighbouring cells. Column <c>c</c> starts at <c>Margin + c * (CellWidth + Spacing)</c>.
/// Pure math, no Avalonia or SkiaSharp.
/// </summary>
/// <remarks>
/// A frame that spans N cells is one contiguous rect that includes the N-1 gaps between them
/// (see <see cref="SpanWidth"/>) -- the editor never models a footprint as separate pieces. The
/// gap pixels are not part of any tile, so what Tiled draws for such a frame is only the cells;
/// <see cref="FrameCells"/> gives those so the wireframe can outline them inside the frame rect.
/// </remarks>
public readonly record struct TileGrid(int CellWidth, int CellHeight, int Margin = 0, int Spacing = 0)
{
    /// <summary>A plain square grid with no margin or spacing -- the achx/achj snap-to-grid setting.</summary>
    public static TileGrid Uniform(int cellSize) => new(cellSize, cellSize);

    public int StrideX => CellWidth + Spacing;
    public int StrideY => CellHeight + Spacing;

    /// <summary>False when every cell is <c>&lt;= 0</c> wide or tall, which disables snapping/drawing.</summary>
    public bool IsValid => CellWidth > 0 && CellHeight > 0;

    public int CellLeft(int column) => Margin + column * StrideX;
    public int CellTop(int row) => Margin + row * StrideY;

    /// <summary>Pixel width of a frame spanning <paramref name="cells"/> cells, gaps included.</summary>
    public int SpanWidth(int cells) => cells * CellWidth + (cells - 1) * Spacing;
    public int SpanHeight(int cells) => cells * CellHeight + (cells - 1) * Spacing;

    /// <summary>The column whose cell (or the gap right after it) contains <paramref name="x"/>; floor
    /// semantics, so a point left of the margin yields a negative column.</summary>
    public int ColumnContaining(float x) => (int)MathF.Floor((x - Margin) / StrideX);
    public int RowContaining(float y) => (int)MathF.Floor((y - Margin) / StrideY);

    /// <summary>Nearest cell start (left edge) to <paramref name="x"/> -- for a frame's left edge or a move.</summary>
    public float SnapStartX(float x) => CellLeft((int)MathF.Round((x - Margin) / StrideX));
    public float SnapStartY(float y) => CellTop((int)MathF.Round((y - Margin) / StrideY));

    /// <summary>Nearest cell end (right edge) to <paramref name="x"/> -- for a frame's right edge, so a
    /// resize lands on the far side of a cell rather than on the next cell's start.</summary>
    public float SnapEndX(float x) => CellLeft((int)MathF.Round((x - Margin - CellWidth) / StrideX)) + CellWidth;
    public float SnapEndY(float y) => CellTop((int)MathF.Round((y - Margin - CellHeight) / StrideY)) + CellHeight;

    /// <summary>
    /// The whole-cell footprint a pixel rect covers, or <see langword="null"/> when its origin
    /// isn't on a cell start or its size isn't a whole number of cells plus the gaps between them
    /// (both within <paramref name="epsilon"/>). The column/row may be negative; callers reject
    /// that themselves.
    /// </summary>
    public TileFootprint? TryLocate(float left, float top, float width, float height, float epsilon)
    {
        if (!IsValid) return null;

        var columns = (int)MathF.Round((width + Spacing) / StrideX);
        var rows = (int)MathF.Round((height + Spacing) / StrideY);
        if (columns < 1 || rows < 1
            || MathF.Abs(width - SpanWidth(columns)) > epsilon
            || MathF.Abs(height - SpanHeight(rows)) > epsilon)
            return null;

        var column = (int)MathF.Round((left - Margin) / StrideX);
        var row = (int)MathF.Round((top - Margin) / StrideY);
        if (MathF.Abs(left - CellLeft(column)) > epsilon || MathF.Abs(top - CellTop(row)) > epsilon)
            return null;

        return new TileFootprint(column, row, columns, rows);
    }

    /// <summary>
    /// Each cell's own pixel rect inside a frame rect, gaps excluded -- what Tiled actually
    /// draws for the frame. Empty when the rect isn't a whole-cell footprint (mid-edit, or an
    /// achx frame that was never grid-aligned).
    /// </summary>
    public IReadOnlyList<BoundsRect> FrameCells(float left, float top, float width, float height)
    {
        if (TryLocate(left, top, width, height, epsilon: 0.001f) is not { } footprint)
            return [];

        var cells = new List<BoundsRect>(footprint.Columns * footprint.Rows);
        for (var dy = 0; dy < footprint.Rows; dy++)
            for (var dx = 0; dx < footprint.Columns; dx++)
            {
                float cellLeft = CellLeft(footprint.Column + dx);
                float cellTop = CellTop(footprint.Row + dy);
                cells.Add(new BoundsRect(cellLeft, cellTop, cellLeft + CellWidth, cellTop + CellHeight));
            }
        return cells;
    }
}
