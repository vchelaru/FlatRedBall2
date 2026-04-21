using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Rendering;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace FlatRedBall2.Collision;

/// <summary>
/// Controls how polygon tiles in a <see cref="TileShapeCollection"/> resolve overlap.
/// </summary>
public enum SlopeCollisionMode
{
    /// <summary>Standard SAT collision for polygon tiles. Correct for top-down games.</summary>
    Standard,
    /// <summary>Vertical-only heightmap separation for polygon tiles. Correct for platformer slopes.</summary>
    PlatformerFloor,
}

/// <summary>
/// A grid-based static collision structure for tile maps. Each filled cell holds one
/// <see cref="AxisAlignedRectangle"/>; spatial partitioning limits collision checks to
/// the cells overlapping the querying shape rather than all tiles.
/// </summary>
/// <remarks>
/// <see cref="X"/> and <see cref="Y"/> can be changed at any time — existing tiles shift
/// automatically. <see cref="GridSize"/> must be set before adding tiles; call
/// <see cref="Clear"/> first if you need to change it after tiles have been added.
/// </remarks>
public class TileShapeCollection : ICollidable
{
    private readonly Dictionary<(int col, int row), AxisAlignedRectangle> _tiles = new();
    private readonly Dictionary<(int col, int row), Polygon> _polyTiles = new();
    // Sub-cell rectangles authored as <object> rects in Tiled. Multiple may coexist in a single
    // cell (e.g., separate spike rects). They participate in RepositionDirections adjacency
    // updates: a face is suppressed when an aligned, overlapping opposite face from another
    // sub-cell rect or full-cell tile meets it — forming a continuous surface (e.g., two
    // half-height curbs side-by-side).
    private readonly Dictionary<(int col, int row), List<AxisAlignedRectangle>> _subCellRects = new();

    /// <summary>
    /// World X of the left edge of cell (0, 0). Can be changed at any time —
    /// existing tiles shift automatically to match the new origin.
    /// </summary>
    public float X
    {
        get => _x;
        set
        {
            float delta = value - _x;
            _x = value;
            if (delta != 0f)
                ShiftAllTiles(delta, 0f);
        }
    }

    /// <summary>
    /// World Y of the bottom edge of cell (0, 0). Can be changed at any time —
    /// existing tiles shift automatically to match the new origin.
    /// </summary>
    public float Y
    {
        get => _y;
        set
        {
            float delta = value - _y;
            _y = value;
            if (delta != 0f)
                ShiftAllTiles(0f, delta);
        }
    }

    /// <summary>
    /// Width and height of each tile in world units. Defaults to 16, which is the standard tile size
    /// for FlatRedBall2 games. Must be set before adding tiles.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if tiles have already been added. Call <see cref="Clear"/> first, change the value, then re-add tiles.</exception>
    public float GridSize
    {
        get => _gridSize;
        set
        {
            ThrowIfTilesExist();
            _gridSize = value;
        }
    }

    private float _x;
    private float _y;
    private float _gridSize = 16f;

    private Layer? _layer;
    private bool _isVisible;
    private XnaColor _color = new XnaColor(255, 255, 255, 128);
    private bool _isFilled = false;
    private float _outlineThickness = 2f;

    // Invoked when a tile shape is created or destroyed so Screen.Add can keep its render list in sync.
    internal Action<IRenderable>? _onTileAdded;
    internal Action<IRenderable>? _onTileRemoved;

    /// <summary>
    /// Returns all tile shapes (rectangles and polygons) currently in this collection. Used by
    /// <see cref="Screen.Add(TileShapeCollection)"/> to register tiles for rendering.
    /// </summary>
    internal IEnumerable<IRenderable> AllTiles
    {
        get
        {
            foreach (var r in _tiles.Values) yield return r;
            foreach (var p in _polyTiles.Values) yield return p;
            foreach (var list in _subCellRects.Values)
                foreach (var r in list) yield return r;
        }
    }

    /// <summary>
    /// Rendering layer for all tiles. Setting this propagates to all existing tiles.
    /// Tiles added after this is set inherit the current layer automatically.
    /// </summary>
    public Layer? Layer
    {
        get => _layer;
        set
        {
            _layer = value;
            foreach (var tile in _tiles.Values)
                tile.Layer = value;
            foreach (var poly in _polyTiles.Values)
                poly.Layer = value;
            foreach (var r in EnumerateSubCellRects())
                r.Layer = value;
        }
    }

    /// <summary>
    /// Shows or hides all tiles. Defaults to false.
    /// </summary>
    /// <remarks>
    /// Call <c>Screen.Add(tiles)</c> first so tiles are registered for rendering; then set
    /// <c>IsVisible = true</c> to make them appear. Tiles added after <c>Screen.Add</c> inherit
    /// the current <see cref="IsVisible"/> value automatically.
    /// </remarks>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            _isVisible = value;
            foreach (var tile in _tiles.Values)
                tile.IsVisible = value;
            foreach (var poly in _polyTiles.Values)
                poly.IsVisible = value;
            foreach (var r in EnumerateSubCellRects())
                r.IsVisible = value;
        }
    }

    /// <summary>
    /// Color applied to all tiles. Defaults to semi-transparent white.
    /// Tiles added after this is set inherit the current value automatically.
    /// </summary>
    public XnaColor Color
    {
        get => _color;
        set
        {
            _color = value;
            foreach (var tile in _tiles.Values)
                tile.Color = value;
            foreach (var poly in _polyTiles.Values)
                poly.Color = value;
            foreach (var r in EnumerateSubCellRects())
                r.Color = value;
        }
    }

    /// <summary>
    /// Whether tiles are drawn filled or as outlines. Defaults to false (outline only).
    /// Tiles added after this is set inherit the current value automatically.
    /// </summary>
    public bool IsFilled
    {
        get => _isFilled;
        set
        {
            _isFilled = value;
            foreach (var tile in _tiles.Values)
                tile.IsFilled = value;
            foreach (var poly in _polyTiles.Values)
                poly.IsFilled = value;
            foreach (var r in EnumerateSubCellRects())
                r.IsFilled = value;
        }
    }

    /// <summary>
    /// Outline thickness in pixels when <see cref="IsFilled"/> is false. Defaults to 2.
    /// Tiles added after this is set inherit the current value automatically.
    /// </summary>
    public float OutlineThickness
    {
        get => _outlineThickness;
        set
        {
            _outlineThickness = value;
            foreach (var tile in _tiles.Values)
                tile.OutlineThickness = value;
            foreach (var poly in _polyTiles.Values)
                poly.OutlineThickness = value;
            foreach (var r in EnumerateSubCellRects())
                r.OutlineThickness = value;
        }
    }

    private IEnumerable<AxisAlignedRectangle> EnumerateSubCellRects()
    {
        foreach (var list in _subCellRects.Values)
            foreach (var r in list) yield return r;
    }

    /// <summary>
    /// Removes all tiles (rectangles and polygons) from this collection.
    /// After clearing, <see cref="X"/>, <see cref="Y"/>, and <see cref="GridSize"/> can be changed again.
    /// </summary>
    public void Clear()
    {
        foreach (var tile in _tiles.Values)
            _onTileRemoved?.Invoke(tile);
        foreach (var poly in _polyTiles.Values)
            _onTileRemoved?.Invoke(poly);
        foreach (var r in EnumerateSubCellRects())
            _onTileRemoved?.Invoke(r);
        _tiles.Clear();
        _polyTiles.Clear();
        _subCellRects.Clear();
    }

    /// <summary>
    /// Adds a solid tile at the given grid cell. Does nothing if a tile already exists there.
    /// <see cref="RepositionDirections"/> on adjacent tiles are updated automatically.
    /// </summary>
    public void AddTileAtCell(int col, int row)
    {
        if (_tiles.ContainsKey((col, row))) return;

        var tile = new AxisAlignedRectangle
        {
            Width = GridSize,
            Height = GridSize,
            X = X + col * GridSize + GridSize / 2f,
            Y = Y + row * GridSize + GridSize / 2f,
            Layer = _layer,
            IsVisible = _isVisible,
            Color = _color,
            IsFilled = _isFilled,
            OutlineThickness = _outlineThickness,
        };

        _tiles[(col, row)] = tile;
        UpdateDirectionsAround(col, row);
        _onTileAdded?.Invoke(tile);
    }

    /// <summary>
    /// Adds a solid tile at the grid cell that contains <paramref name="x"/>, <paramref name="y"/>.
    /// Does nothing if the cell already contains a tile.
    /// </summary>
    public void AddTileAtWorld(float x, float y)
    {
        var (col, row) = WorldToCell(x, y);
        AddTileAtCell(col, row);
    }

    /// <summary>
    /// Removes the tile at the given grid cell. Does nothing if no tile exists there.
    /// <see cref="RepositionDirections"/> on neighboring tiles are updated automatically.
    /// </summary>
    public void RemoveTileAtCell(int col, int row)
    {
        if (!_tiles.TryGetValue((col, row), out var tile)) return;
        _tiles.Remove((col, row));
        UpdateNeighborDirections(col, row);
        _onTileRemoved?.Invoke(tile);
    }

    /// <summary>
    /// Removes the tile at the grid cell containing <paramref name="x"/>, <paramref name="y"/>.
    /// Does nothing if no tile exists at that position.
    /// </summary>
    public void RemoveTileAtWorld(float x, float y)
    {
        var (col, row) = WorldToCell(x, y);
        RemoveTileAtCell(col, row);
    }

    /// <summary>
    /// Returns the <see cref="AxisAlignedRectangle"/> at the given grid cell, or <c>null</c>
    /// if the cell is empty.
    /// </summary>
    public AxisAlignedRectangle? GetTileAtCell(int col, int row) =>
        _tiles.TryGetValue((col, row), out var tile) ? tile : null;

    /// <summary>
    /// Returns the <see cref="AxisAlignedRectangle"/> at the grid cell containing
    /// <paramref name="x"/>, <paramref name="y"/>, or <c>null</c> if the cell is empty.
    /// </summary>
    public AxisAlignedRectangle? GetTileAtWorld(float x, float y)
    {
        var (col, row) = WorldToCell(x, y);
        return GetTileAtCell(col, row);
    }

    /// <summary>
    /// Adds a polygon tile at the given grid cell using <paramref name="prototype"/> as the shape template.
    /// The prototype's local points are copied; the tile is placed at the cell's world center.
    /// Does nothing if a rectangle tile already exists at that cell. Throws
    /// <see cref="InvalidOperationException"/> if a polygon tile already exists there —
    /// multi-polygon-per-cell is not supported; merge the polygons in Tiled instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The prototype's local points define the shape relative to the cell center (0, 0). For a
    /// 16-unit grid, a right-triangle slope covering the bottom-right half of a cell would use
    /// points like <c>(-8,-8)</c>, <c>(8,-8)</c>, <c>(8,8)</c>.
    /// </para>
    /// <para>
    /// Unlike rectangle tiles, polygon tiles do not participate in automatic
    /// <see cref="RepositionDirections"/> management — their collision response is determined
    /// entirely by the polygon geometry via SAT.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">A polygon tile already exists at (<paramref name="col"/>, <paramref name="row"/>).</exception>
    public void AddPolygonTileAtCell(int col, int row, Polygon prototype)
    {
        if (_tiles.ContainsKey((col, row))) return;
        if (_polyTiles.ContainsKey((col, row)))
            throw new InvalidOperationException(
                $"A polygon tile already exists at cell ({col}, {row}). Multi-polygon-per-cell is not supported — merge the polygons in Tiled.");

        var poly = Polygon.FromPoints(prototype.Points);
        poly.X = X + col * GridSize + GridSize / 2f;
        poly.Y = Y + row * GridSize + GridSize / 2f;
        poly.Layer = _layer;
        poly.IsVisible = _isVisible;
        poly.Color = _color;
        poly.IsFilled = _isFilled;
        poly.OutlineThickness = _outlineThickness;

        _polyTiles[(col, row)] = poly;
        UpdateDirectionsAround(col, row);
        _onTileAdded?.Invoke(poly);
    }

    /// <summary>
    /// Removes the polygon tile at the given grid cell. Does nothing if no polygon tile exists there.
    /// </summary>
    public void RemovePolygonTileAtCell(int col, int row)
    {
        if (!_polyTiles.TryGetValue((col, row), out var poly)) return;
        _polyTiles.Remove((col, row));
        UpdateNeighborDirections(col, row);
        _onTileRemoved?.Invoke(poly);
    }

    /// <summary>
    /// Returns the <see cref="Polygon"/> tile at the given grid cell, or <c>null</c> if no polygon
    /// tile exists there.
    /// </summary>
    public Polygon? GetPolygonTileAtCell(int col, int row) =>
        _polyTiles.TryGetValue((col, row), out var poly) ? poly : null;

    /// <summary>
    /// Adds a sub-cell <see cref="AxisAlignedRectangle"/> whose center sits at the cell's world
    /// center plus <paramref name="localCenterX"/> / <paramref name="localCenterY"/>. Used by
    /// <see cref="Tiled.TileMapCollisionGenerator"/> to emit Tiled <c>&lt;object&gt;</c> rectangle
    /// collision shapes; multiple sub-cell rects may coexist in the same cell.
    /// </summary>
    /// <remarks>
    /// Sub-cell rects participate in <see cref="RepositionDirections"/> adjacency updates: a face
    /// is suppressed when it shares an aligned, overlapping opposite face with another sub-cell
    /// rect (same or neighbor cell) or with a full-cell neighbor tile. This prevents snagging at
    /// seams when adjacent sub-cell rects form a continuous surface (e.g., a row of half-height
    /// curbs). When a sub-cell rect fully covers an adjacent full-cell tile's face, the matching
    /// face on the full-cell tile is suppressed as well so the seam presents a single continuous
    /// surface; partial coverage leaves the full-cell face live. Sub-cell-rect vs. polygon
    /// adjacency is not yet honored.
    /// </remarks>
    public void AddRectangleTileAtCell(int col, int row, float localCenterX, float localCenterY, float width, float height)
    {
        var rect = new AxisAlignedRectangle
        {
            Width = width,
            Height = height,
            X = X + col * GridSize + GridSize / 2f + localCenterX,
            Y = Y + row * GridSize + GridSize / 2f + localCenterY,
            Layer = _layer,
            IsVisible = _isVisible,
            Color = _color,
            IsFilled = _isFilled,
            OutlineThickness = _outlineThickness,
        };

        if (!_subCellRects.TryGetValue((col, row), out var list))
        {
            list = new List<AxisAlignedRectangle>();
            _subCellRects[(col, row)] = list;
        }
        list.Add(rect);
        UpdateDirectionsAround(col, row);
        _onTileAdded?.Invoke(rect);
    }

    /// <summary>
    /// Returns the sub-cell rectangles at the given grid cell, or an empty list if none exist.
    /// Unlike <see cref="GetTileAtCell"/>, multiple rectangles may be returned.
    /// </summary>
    public IReadOnlyList<AxisAlignedRectangle> GetRectangleTilesAtCell(int col, int row) =>
        _subCellRects.TryGetValue((col, row), out var list)
            ? list
            : Array.Empty<AxisAlignedRectangle>();

    private (int col, int row) WorldToCell(float x, float y) =>
        ((int)MathF.Floor((x - X) / GridSize), (int)MathF.Floor((y - Y) / GridSize));

    private void UpdateDirectionsAround(int col, int row)
    {
        UpdateCellDirections(col, row);
        UpdateCellDirections(col - 1, row);
        UpdateCellDirections(col + 1, row);
        UpdateCellDirections(col, row - 1);
        UpdateCellDirections(col, row + 1);
        UpdatePolygonEdges(col, row);
        UpdatePolygonEdges(col - 1, row);
        UpdatePolygonEdges(col + 1, row);
        UpdatePolygonEdges(col, row - 1);
        UpdatePolygonEdges(col, row + 1);
        UpdateSubCellRectDirections(col, row);
        UpdateSubCellRectDirections(col - 1, row);
        UpdateSubCellRectDirections(col + 1, row);
        UpdateSubCellRectDirections(col, row - 1);
        UpdateSubCellRectDirections(col, row + 1);
    }

    private void UpdateNeighborDirections(int col, int row)
    {
        UpdateCellDirections(col - 1, row);
        UpdateCellDirections(col + 1, row);
        UpdateCellDirections(col, row - 1);
        UpdateCellDirections(col, row + 1);
        UpdatePolygonEdges(col - 1, row);
        UpdatePolygonEdges(col + 1, row);
        UpdatePolygonEdges(col, row - 1);
        UpdatePolygonEdges(col, row + 1);
        UpdateSubCellRectDirections(col - 1, row);
        UpdateSubCellRectDirections(col + 1, row);
        UpdateSubCellRectDirections(col, row - 1);
        UpdateSubCellRectDirections(col, row + 1);
    }

    private void UpdateCellDirections(int col, int row)
    {
        if (!_tiles.TryGetValue((col, row), out var tile)) return;

        var dirs = RepositionDirections.All;
        bool hasLeft  = _tiles.ContainsKey((col - 1, row)) || _polyTiles.ContainsKey((col - 1, row));
        bool hasRight = _tiles.ContainsKey((col + 1, row)) || _polyTiles.ContainsKey((col + 1, row));
        bool hasDown  = _tiles.ContainsKey((col, row - 1)) || _polyTiles.ContainsKey((col, row - 1));
        bool hasUp    = _tiles.ContainsKey((col, row + 1)) || _polyTiles.ContainsKey((col, row + 1));
        if (hasLeft)  dirs &= ~RepositionDirections.Left;
        if (hasRight) dirs &= ~RepositionDirections.Right;
        if (hasDown)  dirs &= ~RepositionDirections.Down;
        if (hasUp)    dirs &= ~RepositionDirections.Up;

        tile.RepositionDirections = dirs;
    }

    // Computes SuppressedEdges for the polygon tile at (col, row).
    // An edge is suppressed if both its vertices lie along a cell boundary
    // that borders an occupied cell (rect or polygon).
    private void UpdatePolygonEdges(int col, int row)
    {
        if (!_polyTiles.TryGetValue((col, row), out var poly)) return;

        float cellLeft   = X + col * GridSize;
        float cellRight  = X + (col + 1) * GridSize;
        float cellBottom = Y + row * GridSize;
        float cellTop    = Y + (row + 1) * GridSize;

        bool hasLeft  = _tiles.ContainsKey((col - 1, row)) || _polyTiles.ContainsKey((col - 1, row));
        bool hasRight = _tiles.ContainsKey((col + 1, row)) || _polyTiles.ContainsKey((col + 1, row));
        bool hasDown  = _tiles.ContainsKey((col, row - 1)) || _polyTiles.ContainsKey((col, row - 1));
        bool hasUp    = _tiles.ContainsKey((col, row + 1)) || _polyTiles.ContainsKey((col, row + 1));

        int suppressed = 0;
        const float eps = 1e-3f;

        for (int i = 0; i < poly.Points.Count; i++)
        {
            // World-space vertex positions (polygon has no rotation in tile grids)
            float ax = poly.X + poly.Points[i].X;
            float ay = poly.Y + poly.Points[i].Y;
            int next = (i + 1) % poly.Points.Count;
            float bx = poly.X + poly.Points[next].X;
            float by = poly.Y + poly.Points[next].Y;

            // Check if both vertices lie along a cell boundary
            if (hasLeft  && MathF.Abs(ax - cellLeft)   < eps && MathF.Abs(bx - cellLeft)   < eps) suppressed |= 1 << i;
            if (hasRight && MathF.Abs(ax - cellRight)  < eps && MathF.Abs(bx - cellRight)  < eps) suppressed |= 1 << i;
            if (hasDown  && MathF.Abs(ay - cellBottom) < eps && MathF.Abs(by - cellBottom) < eps) suppressed |= 1 << i;
            if (hasUp    && MathF.Abs(ay - cellTop)    < eps && MathF.Abs(by - cellTop)    < eps) suppressed |= 1 << i;
        }

        poly.SuppressedEdges = suppressed;
    }

    // Recomputes RepositionDirections for every sub-cell rect in (col, row) by finding, for each
    // of its four faces, an aligned-and-overlapping opposite face from another sub-cell rect or
    // full-cell tile (same cell, or immediate neighbors). Any such match suppresses the whole
    // face — partial-overlap face fragmentation is not modeled (RepositionDirections is a 4-bit
    // bitfield, not a range). When a sub-cell rect fully covers an adjacent full-cell tile's face
    // (endpoints coincide within eps), the matching bit on the full-cell tile is also cleared so
    // the seam presents a single continuous surface. Partial coverage intentionally leaves the
    // full-cell face live (e.g. a short spike next to a tall wall — movers should still repo off
    // the exposed upper portion of the wall). Sub-cell-rect vs. polygon adjacency is handled on
    // the rect side only: if an axis-aligned polygon edge lies on the shared cell boundary and
    // fully covers the rect's face, the rect's bit is cleared. The polygon side of the seam is
    // not modified here — see TODOS.md "Polygon Snagging in Top-Down".
    private void UpdateSubCellRectDirections(int col, int row)
    {
        if (!_subCellRects.TryGetValue((col, row), out var rects)) return;

        foreach (var rect in rects)
        {
            var dirs = RepositionDirections.All;
            float left   = rect.X - rect.Width  / 2f;
            float right  = rect.X + rect.Width  / 2f;
            float bottom = rect.Y - rect.Height / 2f;
            float top    = rect.Y + rect.Height / 2f;

            if (HasAlignedFace(col, row, rect, RepositionDirections.Left,  left,   bottom, top)
                || IsFaceFullyCoveredByAdjacentPolygonEdge(col, row, RepositionDirections.Left,  left,   bottom, top))
                dirs &= ~RepositionDirections.Left;
            if (HasAlignedFace(col, row, rect, RepositionDirections.Right, right,  bottom, top)
                || IsFaceFullyCoveredByAdjacentPolygonEdge(col, row, RepositionDirections.Right, right,  bottom, top))
                dirs &= ~RepositionDirections.Right;
            if (HasAlignedFace(col, row, rect, RepositionDirections.Down,  bottom, left,   right)
                || IsFaceFullyCoveredByAdjacentPolygonEdge(col, row, RepositionDirections.Down,  bottom, left,   right))
                dirs &= ~RepositionDirections.Down;
            if (HasAlignedFace(col, row, rect, RepositionDirections.Up,    top,    left,   right)
                || IsFaceFullyCoveredByAdjacentPolygonEdge(col, row, RepositionDirections.Up,    top,    left,   right))
                dirs &= ~RepositionDirections.Up;

            rect.RepositionDirections = dirs;

            // Full-coverage seam: if this sub-cell rect's face spans the entire adjacent full-cell
            // tile's opposite face, clear the full-cell's matching bit too.
            SuppressFullCellFaceIfFullyCovered(col, row, RepositionDirections.Left,  left,   bottom, top);
            SuppressFullCellFaceIfFullyCovered(col, row, RepositionDirections.Right, right,  bottom, top);
            SuppressFullCellFaceIfFullyCovered(col, row, RepositionDirections.Down,  bottom, left,   right);
            SuppressFullCellFaceIfFullyCovered(col, row, RepositionDirections.Up,    top,    left,   right);
        }
    }

    // If the full-cell neighbor in 'dir' has its opposite face at 'facePos' and that face's
    // parallel-axis extent is fully contained within [rangeMin, rangeMax] (within eps), clear
    // the corresponding RepositionDirections bit on the neighbor. Partial coverage is a no-op.
    private void SuppressFullCellFaceIfFullyCovered(int col, int row,
        RepositionDirections dir, float facePos, float rangeMin, float rangeMax)
    {
        const float eps = 1e-3f;

        int nCol = col, nRow = row;
        switch (dir)
        {
            case RepositionDirections.Left:  nCol -= 1; break;
            case RepositionDirections.Right: nCol += 1; break;
            case RepositionDirections.Down:  nRow -= 1; break;
            case RepositionDirections.Up:    nRow += 1; break;
        }

        if (!_tiles.TryGetValue((nCol, nRow), out var fullTile)) return;

        float oLeft   = fullTile.X - fullTile.Width  / 2f;
        float oRight  = fullTile.X + fullTile.Width  / 2f;
        float oBottom = fullTile.Y - fullTile.Height / 2f;
        float oTop    = fullTile.Y + fullTile.Height / 2f;

        // For each dir: check alignment of the full-cell's opposite face, then verify the
        // full-cell face's parallel extent is fully covered by [rangeMin, rangeMax].
        bool aligned, fullyCovered;
        RepositionDirections clearBit;
        switch (dir)
        {
            case RepositionDirections.Left:
                aligned = MathF.Abs(oRight - facePos) < eps;
                fullyCovered = rangeMin <= oBottom + eps && rangeMax >= oTop - eps;
                clearBit = RepositionDirections.Right;
                break;
            case RepositionDirections.Right:
                aligned = MathF.Abs(oLeft - facePos) < eps;
                fullyCovered = rangeMin <= oBottom + eps && rangeMax >= oTop - eps;
                clearBit = RepositionDirections.Left;
                break;
            case RepositionDirections.Down:
                aligned = MathF.Abs(oTop - facePos) < eps;
                fullyCovered = rangeMin <= oLeft + eps && rangeMax >= oRight - eps;
                clearBit = RepositionDirections.Up;
                break;
            case RepositionDirections.Up:
                aligned = MathF.Abs(oBottom - facePos) < eps;
                fullyCovered = rangeMin <= oLeft + eps && rangeMax >= oRight - eps;
                clearBit = RepositionDirections.Down;
                break;
            default:
                return;
        }

        if (aligned && fullyCovered)
            fullTile.RepositionDirections &= ~clearBit;
    }

    // True if the neighbor cell in 'dir' holds a polygon whose axis-aligned edge lies on the
    // shared cell boundary AND that edge's parallel-axis extent fully covers [rangeMin, rangeMax].
    // Only axis-aligned polygon edges along the boundary can match; slanted edges never will, so
    // slope polygons do not suppress the rect face along their slanted sides.
    private bool IsFaceFullyCoveredByAdjacentPolygonEdge(int col, int row,
        RepositionDirections dir, float facePos, float rangeMin, float rangeMax)
    {
        const float eps = 1e-3f;

        int nCol = col, nRow = row;
        switch (dir)
        {
            case RepositionDirections.Left:  nCol -= 1; break;
            case RepositionDirections.Right: nCol += 1; break;
            case RepositionDirections.Down:  nRow -= 1; break;
            case RepositionDirections.Up:    nRow += 1; break;
        }

        if (!_polyTiles.TryGetValue((nCol, nRow), out var poly)) return false;

        bool isVerticalEdge = dir == RepositionDirections.Left || dir == RepositionDirections.Right;

        for (int i = 0; i < poly.Points.Count; i++)
        {
            int next = (i + 1) % poly.Points.Count;
            float ax = poly.X + poly.Points[i].X;
            float ay = poly.Y + poly.Points[i].Y;
            float bx = poly.X + poly.Points[next].X;
            float by = poly.Y + poly.Points[next].Y;

            if (isVerticalEdge)
            {
                // Need both endpoints on the shared vertical boundary (x = facePos)
                if (MathF.Abs(ax - facePos) >= eps || MathF.Abs(bx - facePos) >= eps) continue;
                float edgeMin = MathF.Min(ay, by);
                float edgeMax = MathF.Max(ay, by);
                if (edgeMin <= rangeMin + eps && edgeMax >= rangeMax - eps) return true;
            }
            else
            {
                // Horizontal boundary (y = facePos)
                if (MathF.Abs(ay - facePos) >= eps || MathF.Abs(by - facePos) >= eps) continue;
                float edgeMin = MathF.Min(ax, bx);
                float edgeMax = MathF.Max(ax, bx);
                if (edgeMin <= rangeMin + eps && edgeMax >= rangeMax - eps) return true;
            }
        }

        return false;
    }

    // Returns true if some rect (sub-cell or full-cell, same or neighbor cell) has an opposite
    // face lying on 'facePos' (the perpendicular-axis coordinate of 'rect's face in 'dir') with
    // non-zero overlap along [rangeMin, rangeMax] (the parallel-axis extent of 'rect's face).
    private bool HasAlignedFace(int col, int row, AxisAlignedRectangle rect,
        RepositionDirections dir, float facePos, float rangeMin, float rangeMax)
    {
        const float eps = 1e-3f;

        // Same-cell sub-cell rects.
        if (_subCellRects.TryGetValue((col, row), out var sameList))
        {
            foreach (var other in sameList)
            {
                if (ReferenceEquals(other, rect)) continue;
                if (OppositeFaceMatches(other, dir, facePos, rangeMin, rangeMax, eps)) return true;
            }
        }

        // Neighbor cell (only the one in this direction matters).
        int nCol = col, nRow = row;
        switch (dir)
        {
            case RepositionDirections.Left:  nCol -= 1; break;
            case RepositionDirections.Right: nCol += 1; break;
            case RepositionDirections.Down:  nRow -= 1; break;
            case RepositionDirections.Up:    nRow += 1; break;
        }

        if (_tiles.TryGetValue((nCol, nRow), out var fullTile)
            && OppositeFaceMatches(fullTile, dir, facePos, rangeMin, rangeMax, eps))
            return true;

        if (_subCellRects.TryGetValue((nCol, nRow), out var neighborList))
        {
            foreach (var other in neighborList)
                if (OppositeFaceMatches(other, dir, facePos, rangeMin, rangeMax, eps)) return true;
        }

        return false;
    }

    private static bool OppositeFaceMatches(AxisAlignedRectangle other,
        RepositionDirections dir, float facePos, float rangeMin, float rangeMax, float eps)
    {
        float oLeft   = other.X - other.Width  / 2f;
        float oRight  = other.X + other.Width  / 2f;
        float oBottom = other.Y - other.Height / 2f;
        float oTop    = other.Y + other.Height / 2f;

        // The opposite face: dir=Left checks other's right face, Right checks other's left,
        // Down checks other's top, Up checks other's bottom.
        switch (dir)
        {
            case RepositionDirections.Left:
                return MathF.Abs(oRight - facePos) < eps
                    && MathF.Min(oTop, rangeMax) - MathF.Max(oBottom, rangeMin) > eps;
            case RepositionDirections.Right:
                return MathF.Abs(oLeft - facePos) < eps
                    && MathF.Min(oTop, rangeMax) - MathF.Max(oBottom, rangeMin) > eps;
            case RepositionDirections.Down:
                return MathF.Abs(oTop - facePos) < eps
                    && MathF.Min(oRight, rangeMax) - MathF.Max(oLeft, rangeMin) > eps;
            case RepositionDirections.Up:
                return MathF.Abs(oBottom - facePos) < eps
                    && MathF.Min(oRight, rangeMax) - MathF.Max(oLeft, rangeMin) > eps;
        }
        return false;
    }

    // Returns the total separation vector needed to move 'shape' out of all overlapping tiles.
    // Called by CollisionDispatcher when this collection is the static geometry side.
    // slopeMode is supplied by the caller (CollisionRelationship) because the correct
    // resolution depends on the relationship, not on the tile geometry — the same collection
    // may be used by a player with PlatformerFloor semantics and a ball with Standard SAT.
    internal Vector2 GetSeparationFor(ICollidable shape, SlopeCollisionMode slopeMode = SlopeCollisionMode.Standard)
    {
        var (minX, maxX, minY, maxY) = CollisionDispatcher.GetBounds(shape);
        float centerX = (minX + maxX) / 2f;

        int colMin = (int)MathF.Floor((minX - X) / GridSize);
        int colMax = (int)MathF.Floor((maxX - X) / GridSize);
        int rowMin = (int)MathF.Floor((minY - Y) / GridSize);
        int rowMax = (int)MathF.Floor((maxY - Y) / GridSize);

        Vector2 total = Vector2.Zero;
        for (int col = colMin; col <= colMax; col++)
        {
            for (int row = rowMin; row <= rowMax; row++)
            {
                Vector2 sep;
                if (_tiles.TryGetValue((col, row), out var tile))
                {
                    sep = CollisionDispatcher.GetSeparationVector(shape, tile);
                    if (slopeMode == SlopeCollisionMode.PlatformerFloor)
                    {
                        float rectLeft  = tile.AbsoluteX - tile.Width / 2f;
                        float rectRight = tile.AbsoluteX + tile.Width / 2f;
                        bool centerInside = centerX >= rectLeft && centerX <= rectRight;

                        float rectTop = tile.AbsoluteY + tile.Height / 2f;
                        bool hasEntityParent = shape is IAttachable att && att.Parent is Entity;
                        Entity? ent = hasEntityParent ? (Entity)((IAttachable)shape).Parent! : null;

                        // Prefer landing: if standard collision pushes horizontally
                        // but the shape was above the rect last frame, override to land
                        // on top. Reconstructs last-frame bottom from velocity:
                        //   lastBottom ≈ currentBottom - velocityY / 60
                        // Only fires when the tile's Up direction is active — otherwise
                        // the top face is covered by another tile and isn't a landing
                        // surface.
                        if (sep.X != 0f && sep.Y == 0f && ent != null && ent.VelocityY < 0f
                            && tile.RepositionDirections.HasFlag(RepositionDirections.Up))
                        {
                            float lastBottom = minY - ent.VelocityY / 60f;
                            if (lastBottom > rectTop)
                            {
                                float pushUp = rectTop - minY;
                                if (pushUp > 0f)
                                    sep = new Vector2(0f, pushUp);
                            }
                        }
                        // Suppress vertical push at slope-to-rect seams (center X outside
                        // rect AND a polygon tile is adjacent on that side).
                        else if (sep.Y != 0f && !centerInside)
                        {
                            int adjCol = centerX < rectLeft ? col - 1 : col + 1;
                            if (_polyTiles.ContainsKey((adjCol, row)))
                                sep = new Vector2(sep.X, 0f);
                        }
                    }
                }
                else if (_polyTiles.TryGetValue((col, row), out var poly))
                {
                    // Heightmap separation assumes the polygon is a floor (walkable surface on
                    // top, open space below). For ceiling-like polygons (e.g., a V-flipped
                    // slope whose mass sits in the upper half of the cell), the heightmap
                    // path would push the player UP into the solid mass. Fall back to SAT
                    // for those — rect ceilings already work correctly via SAT.
                    if (slopeMode == SlopeCollisionMode.PlatformerFloor && IsFloorLikePolygon(poly))
                        sep = GetHeightmapSeparation(poly, minX, maxX, minY, maxY);
                    else
                        sep = CollisionDispatcher.GetSeparationVector(shape, poly);
                }
                else
                    sep = Vector2.Zero;

                if (sep != Vector2.Zero)
                {
                    // Take the largest push on each axis independently to avoid double-counting
                    // when the shape overlaps multiple tiles on the same side.
                    if (MathF.Abs(sep.X) > MathF.Abs(total.X))
                        total = new Vector2(sep.X, total.Y);
                    if (MathF.Abs(sep.Y) > MathF.Abs(total.Y))
                        total = new Vector2(total.X, sep.Y);
                }

                // Sub-cell rects are additive: a cell may contain multiple, and they may coexist
                // with a full-cell polygon (rare) authored in Tiled.
                if (_subCellRects.TryGetValue((col, row), out var subRects))
                {
                    foreach (var sub in subRects)
                    {
                        var subSep = CollisionDispatcher.GetSeparationVector(shape, sub);
                        if (subSep == Vector2.Zero) continue;
                        if (MathF.Abs(subSep.X) > MathF.Abs(total.X))
                            total = new Vector2(subSep.X, total.Y);
                        if (MathF.Abs(subSep.Y) > MathF.Abs(total.Y))
                            total = new Vector2(total.X, subSep.Y);
                    }
                }
            }
        }

        return total;
    }

    // Classifies a polygon tile as "floor-like" (walkable surface on top, mass below)
    // vs "ceiling-like" (mass above, open space below). Uses the area-weighted centroid
    // in polygon-local space: centroid Y < 0 means mass is in the lower half of the cell,
    // which is the floor case. Ceiling-like polygons (e.g., V-flipped slopes) fall back
    // to SAT so the player gets pushed away from the ceiling instead of up into it.
    private static bool IsFloorLikePolygon(Polygon poly)
    {
        var pts = poly.Points;
        if (pts.Count < 3) return true;

        // Shoelace centroid in local coordinates (poly.X/Y cancel out, so ignore them).
        float area2 = 0f;
        float cy = 0f;
        for (int i = 0; i < pts.Count; i++)
        {
            var p0 = pts[i];
            var p1 = pts[(i + 1) % pts.Count];
            float cross = p0.X * p1.Y - p1.X * p0.Y;
            area2 += cross;
            cy += (p0.Y + p1.Y) * cross;
        }

        if (MathF.Abs(area2) < 1e-6f) return true; // degenerate — default to floor
        float centroidY = cy / (3f * area2);
        return centroidY < 0f;
    }

    // Heightmap-based vertical separation for platformer slopes.
    // Computes the polygon's surface height at the shape's center X and pushes up if needed.
    private static Vector2 GetHeightmapSeparation(Polygon poly, float shapeMinX, float shapeMaxX, float shapeMinY, float shapeMaxY)
    {
        float centerX = (shapeMinX + shapeMaxX) / 2f;
        float bottomY = shapeMinY;

        float? surfaceY = GetPolygonSurfaceYAt(poly, centerX);
        if (surfaceY == null) return Vector2.Zero; // centerX outside polygon edges
        if (bottomY >= surfaceY.Value) return Vector2.Zero; // shape is above the surface

        return new Vector2(0f, surfaceY.Value - bottomY);
    }

    // Finds the polygon's top-surface Y at the given world X by scanning edges.
    // The surface is the highest Y of any edge spanning centerX. Returns null when centerX
    // is outside the polygon's X range.
    private static float? GetPolygonSurfaceYAt(Polygon poly, float centerX)
    {
        float surfaceY = float.MinValue;
        var pts = poly.Points;
        for (int i = 0; i < pts.Count; i++)
        {
            float ax = poly.X + pts[i].X;
            float ay = poly.Y + pts[i].Y;
            int next = (i + 1) % pts.Count;
            float bx = poly.X + pts[next].X;
            float by = poly.Y + pts[next].Y;

            if ((ax <= centerX && bx >= centerX) || (bx <= centerX && ax >= centerX))
            {
                float range = bx - ax;
                if (MathF.Abs(range) < 1e-6f)
                    surfaceY = MathF.Max(surfaceY, MathF.Max(ay, by));
                else
                {
                    float t = (centerX - ax) / range;
                    float y = ay + t * (by - ay);
                    if (y > surfaceY) surfaceY = y;
                }
            }
        }
        return surfaceY == float.MinValue ? null : surfaceY;
    }

    /// <summary>
    /// Returns the top-surface world-Y of a floor-like polygon tile occupying the cell at
    /// <paramref name="worldX"/>, or <c>null</c> when no polygon tile is present. Used by the
    /// one-way-collision gate to make the LastPosition check slope-aware: on an uphill cloud
    /// slope, the surface Y at the player's last-frame X is lower than at the current X, and
    /// that difference must be folded into the gate. Only polygon tiles are queried — rect
    /// tiles are excluded so the delta adjustment doesn't fire at slope-to-flat transitions
    /// (where it would incorrectly loosen the gate and allow large upward separations).
    /// </summary>
    internal float? GetHeightmapSurfaceYAt(float worldX)
    {
        int col = (int)MathF.Floor((worldX - X) / GridSize);
        foreach (var kv in _polyTiles)
        {
            if (kv.Key.col != col) continue;
            if (!IsFloorLikePolygon(kv.Value)) continue;
            var y = GetPolygonSurfaceYAt(kv.Value, worldX);
            if (y.HasValue) return y;
        }
        return null;
    }

    /// <summary>
    /// Casts a line segment from <paramref name="start"/> to <paramref name="end"/> and returns
    /// the closest intersection with any tile, using DDA grid traversal.
    /// </summary>
    /// <param name="start">Start of the segment in world space.</param>
    /// <param name="end">End of the segment in world space.</param>
    /// <param name="hitPoint">World-space position of the first intersection, or <see cref="Vector2.Zero"/> if no hit.</param>
    /// <param name="hitNormal">Outward surface normal at the hit face — one of (±1,0) or (0,±1) — or <see cref="Vector2.Zero"/> if no hit.</param>
    /// <returns><c>true</c> if the segment intersects any tile; <c>false</c> otherwise.</returns>
    /// <remarks>
    /// Only tiles along the ray path are tested. If <paramref name="start"/> is inside a full-cell tile, returns <c>false</c>. If <paramref name="start"/> is inside a polygon tile's fill, returns an immediate hit at <paramref name="start"/> with normal pointing back along the ray — useful for ground-snap probes that start just inside a slope.
    /// </remarks>
    public bool Raycast(Vector2 start, Vector2 end, out Vector2 hitPoint, out Vector2 hitNormal)
        => Raycast(start, end, out hitPoint, out hitNormal, out _);

    /// <summary>
    /// Returns the grid cell (col, row) that contains <paramref name="worldPoint"/>. No bounds
    /// check — caller may get back coordinates outside the currently-populated range.
    /// </summary>
    public (int col, int row) GetCellAt(Vector2 worldPoint)
    {
        int col = (int)MathF.Floor((worldPoint.X - X) / GridSize);
        int row = (int)MathF.Floor((worldPoint.Y - Y) / GridSize);
        return (col, row);
    }

    /// <summary>
    /// Returns the world-space center of cell (<paramref name="col"/>, <paramref name="row"/>).
    /// Inverse of <see cref="GetCellAt"/>. No bounds check — callers may pass coordinates
    /// outside the currently-populated range (useful for procedurally spawning at grid
    /// positions before any tile has been added there).
    /// </summary>
    public Vector2 GetCellWorldPosition(int col, int row) => new(
        X + col * GridSize + GridSize / 2f,
        Y + row * GridSize + GridSize / 2f);

    /// <inheritdoc cref="Raycast(Vector2, Vector2, out Vector2, out Vector2)"/>
    /// <param name="hitShape">
    /// The shape that produced the hit — an <see cref="AxisAlignedRectangle"/> (full-cell tile or
    /// sub-cell rect) or a <see cref="Polygon"/>. <c>null</c> if no hit.
    /// </param>
    public bool Raycast(Vector2 start, Vector2 end, out Vector2 hitPoint, out Vector2 hitNormal, out ICollidable? hitShape)
    {
        hitPoint = Vector2.Zero;
        hitNormal = Vector2.Zero;
        hitShape = null;

        Vector2 dir = end - start;
        if (dir.X == 0f && dir.Y == 0f) return false;

        // Grid-local start position
        float lx = start.X - X;
        float ly = start.Y - Y;

        int col = (int)MathF.Floor(lx / GridSize);
        int row = (int)MathF.Floor(ly / GridSize);

        if (_tiles.ContainsKey((col, row))) return false;

        int stepX = dir.X > 0 ? 1 : dir.X < 0 ? -1 : 0;
        int stepY = dir.Y > 0 ? 1 : dir.Y < 0 ? -1 : 0;

        // How much t (0..1 along the segment) increases when crossing one full cell on each axis
        float tDeltaX = stepX != 0 ? MathF.Abs(GridSize / dir.X) : float.MaxValue;
        float tDeltaY = stepY != 0 ? MathF.Abs(GridSize / dir.Y) : float.MaxValue;

        // t at which we first cross the next boundary on each axis
        float tMaxX = stepX > 0 ? ((col + 1) * GridSize - lx) / dir.X
                    : stepX < 0 ? (col * GridSize - lx) / dir.X
                    : float.MaxValue;
        float tMaxY = stepY > 0 ? ((row + 1) * GridSize - ly) / dir.Y
                    : stepY < 0 ? (row * GridSize - ly) / dir.Y
                    : float.MaxValue;

        // Check polygon and sub-cell rects in the starting cell up front. The DDA loop below
        // only tests shapes when crossing INTO a cell, so start-cell shapes would otherwise
        // be skipped. This matters for ground snap when feet sit slightly embedded in a
        // slope's cell — a probe starting just below the surface must still hit the slope,
        // not fall through to whatever is in the cell beneath.
        {
            float startBestT = float.MaxValue;
            Vector2 startBestPoint = Vector2.Zero;
            Vector2 startBestNormal = Vector2.Zero;
            ICollidable? startBestShape = null;

            if (_polyTiles.TryGetValue((col, row), out var startPoly))
            {
                if (IsPointInsidePolygon(startPoly, start))
                {
                    // Ray origin is inside the polygon fill — return immediate hit at start
                    // with a normal opposing the ray direction so callers back out.
                    hitPoint = start;
                    var rev = -dir;
                    float len = rev.Length();
                    hitNormal = len > 0f ? rev / len : Vector2.Zero;
                    hitShape = startPoly;
                    return true;
                }
                if (startPoly.Raycast(start, end, out var polyPoint, out var polyNormal))
                {
                    float polyT = dir.LengthSquared() > 0f
                        ? Vector2.Dot(polyPoint - start, dir) / dir.LengthSquared()
                        : 0f;
                    if (polyT >= 0f && polyT < startBestT)
                    {
                        startBestT = polyT;
                        startBestPoint = polyPoint;
                        startBestNormal = polyNormal;
                        startBestShape = startPoly;
                    }
                }
            }

            if (TryRaycastSubCellRects(col, row, start, dir, out var rectPoint, out var rectNormal, out var startRect))
            {
                float rectT = dir.LengthSquared() > 0f
                    ? Vector2.Dot(rectPoint - start, dir) / dir.LengthSquared()
                    : 0f;
                if (rectT < startBestT)
                {
                    startBestT = rectT;
                    startBestPoint = rectPoint;
                    startBestNormal = rectNormal;
                    startBestShape = startRect;
                }
            }

            if (startBestShape != null)
            {
                hitPoint = startBestPoint;
                hitNormal = startBestNormal;
                hitShape = startBestShape;
                return true;
            }
        }

        while (true)
        {
            float tBoundary;
            Vector2 boundaryNormal;

            if (tMaxX <= tMaxY)
            {
                tBoundary = tMaxX;
                col += stepX;
                boundaryNormal = new Vector2(-stepX, 0);
                tMaxX += tDeltaX;
            }
            else
            {
                tBoundary = tMaxY;
                row += stepY;
                boundaryNormal = new Vector2(0, -stepY);
                tMaxY += tDeltaY;
            }

            if (tBoundary > 1f) break;

            if (_tiles.TryGetValue((col, row), out var fullTile))
            {
                hitPoint = start + dir * tBoundary;
                hitNormal = boundaryNormal;
                hitShape = fullTile;
                return true;
            }

            // For polygon and sub-cell shapes, the ray may hit anywhere inside the cell — pick the
            // earliest t among the two. `tBoundary` is the t when the ray entered this cell.
            float bestT = float.MaxValue;
            Vector2 bestNormal = Vector2.Zero;
            Vector2 bestPoint = Vector2.Zero;
            ICollidable? bestShape = null;
            bool foundInCell = false;

            if (_polyTiles.TryGetValue((col, row), out var polyTile))
            {
                if (polyTile.Raycast(start, end, out var polyPoint, out var polyNormal))
                {
                    float polyT = dir.LengthSquared() > 0f
                        ? Vector2.Dot(polyPoint - start, dir) / dir.LengthSquared()
                        : 0f;
                    if (polyT < bestT)
                    {
                        bestT = polyT;
                        bestNormal = polyNormal;
                        bestPoint = polyPoint;
                        bestShape = polyTile;
                        foundInCell = true;
                    }
                }
            }

            if (TryRaycastSubCellRects(col, row, start, dir, out var rectPoint, out var rectNormal, out var subRect))
            {
                float rectT = dir.LengthSquared() > 0f
                    ? Vector2.Dot(rectPoint - start, dir) / dir.LengthSquared()
                    : 0f;
                if (rectT < bestT)
                {
                    bestT = rectT;
                    bestNormal = rectNormal;
                    bestPoint = rectPoint;
                    bestShape = subRect;
                    foundInCell = true;
                }
            }

            if (foundInCell)
            {
                hitPoint = bestPoint;
                hitNormal = bestNormal;
                hitShape = bestShape;
                return true;
            }
        }

        return false;
    }

    // Even-odd rule point-in-polygon test in world space. Assumes the polygon has no rotation
    // (true for polygon tiles emitted by the tileset importer).
    private static bool IsPointInsidePolygon(Polygon poly, Vector2 point)
    {
        var pts = poly.Points;
        if (pts.Count < 3) return false;
        bool inside = false;
        float px = point.X - poly.X;
        float py = point.Y - poly.Y;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            float ix = pts[i].X, iy = pts[i].Y;
            float jx = pts[j].X, jy = pts[j].Y;
            bool crosses = ((iy > py) != (jy > py))
                && (px < (jx - ix) * (py - iy) / (jy - iy) + ix);
            if (crosses) inside = !inside;
        }
        return inside;
    }

    // Slab-method ray-vs-AABB over all sub-cell rects in the given cell. Returns the earliest
    // hit (smallest t in [0,1] along start + dir). Normal points back toward the ray origin.
    private bool TryRaycastSubCellRects(int col, int row, Vector2 start, Vector2 dir,
        out Vector2 hitPoint, out Vector2 hitNormal, out AxisAlignedRectangle? hitRect)
    {
        hitPoint = Vector2.Zero;
        hitNormal = Vector2.Zero;
        hitRect = null;

        if (!_subCellRects.TryGetValue((col, row), out var list) || list.Count == 0)
            return false;

        float bestT = float.MaxValue;
        Vector2 bestNormal = Vector2.Zero;
        AxisAlignedRectangle? bestRect = null;

        foreach (var rect in list)
        {
            float minX = rect.X - rect.Width / 2f;
            float maxX = rect.X + rect.Width / 2f;
            float minY = rect.Y - rect.Height / 2f;
            float maxY = rect.Y + rect.Height / 2f;

            float tMin = 0f;
            float tMax = 1f;
            Vector2 enterNormal = Vector2.Zero;

            // X slab
            if (dir.X == 0f)
            {
                if (start.X < minX || start.X > maxX) continue;
            }
            else
            {
                float t1 = (minX - start.X) / dir.X;
                float t2 = (maxX - start.X) / dir.X;
                Vector2 n1 = new(-1f, 0f);
                Vector2 n2 = new( 1f, 0f);
                if (t1 > t2) { (t1, t2) = (t2, t1); (n1, n2) = (n2, n1); }
                if (t1 > tMin) { tMin = t1; enterNormal = n1; }
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) continue;
            }

            // Y slab
            if (dir.Y == 0f)
            {
                if (start.Y < minY || start.Y > maxY) continue;
            }
            else
            {
                float t1 = (minY - start.Y) / dir.Y;
                float t2 = (maxY - start.Y) / dir.Y;
                Vector2 n1 = new(0f, -1f);
                Vector2 n2 = new(0f,  1f);
                if (t1 > t2) { (t1, t2) = (t2, t1); (n1, n2) = (n2, n1); }
                if (t1 > tMin) { tMin = t1; enterNormal = n1; }
                if (t2 < tMax) tMax = t2;
                if (tMin > tMax) continue;
            }

            if (tMin < 0f || tMin > 1f) continue;
            if (tMin < bestT)
            {
                bestT = tMin;
                bestNormal = enterNormal;
                bestRect = rect;
            }
        }

        if (bestT == float.MaxValue) return false;

        hitPoint = start + dir * bestT;
        hitNormal = bestNormal;
        hitRect = bestRect;
        return true;
    }

    // ICollidable — TileShapeCollection is static geometry; only the querying shape moves.
    public float AbsoluteX => X;
    public float AbsoluteY => Y;
    public float BroadPhaseRadius => float.MaxValue;
    public bool CollidesWith(ICollidable other) => GetSeparationFor(other) != Vector2.Zero;
    public Vector2 GetSeparationVector(ICollidable other) => GetSeparationFor(other);
    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f) { }
    public void ApplySeparationOffset(Vector2 offset) { }
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
    public void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }

    private void ShiftAllTiles(float dx, float dy)
    {
        foreach (var tile in _tiles.Values)
        {
            tile.X += dx;
            tile.Y += dy;
        }
        foreach (var poly in _polyTiles.Values)
        {
            poly.X += dx;
            poly.Y += dy;
        }
        foreach (var r in EnumerateSubCellRects())
        {
            r.X += dx;
            r.Y += dy;
        }
    }

    private void ThrowIfTilesExist()
    {
        if (_tiles.Count > 0 || _polyTiles.Count > 0 || _subCellRects.Count > 0)
            throw new InvalidOperationException(
                "Cannot change GridSize after tiles have been added. Call Clear() first, change the value, then re-add tiles.");
    }
}
