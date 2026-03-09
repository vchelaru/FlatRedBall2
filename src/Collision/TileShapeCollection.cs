using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Rendering;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace FlatRedBall2.Collision;

/// <summary>
/// A grid-based static collision structure for tile maps. Each filled cell holds one
/// <see cref="AxisAlignedRectangle"/>; spatial partitioning limits collision checks to
/// the cells overlapping the querying shape rather than all tiles.
/// </summary>
/// <remarks>
/// Set <see cref="X"/>, <see cref="Y"/>, and <see cref="GridSize"/> before adding tiles —
/// tile positions are computed from these values at insertion time and are not updated if
/// the properties change afterwards.
/// </remarks>
public class TileShapeCollection : ICollidable
{
    private readonly Dictionary<(int col, int row), AxisAlignedRectangle> _tiles = new();
    private readonly Dictionary<(int col, int row), Polygon> _polyTiles = new();

    /// <summary>
    /// World X of the left edge of cell (0, 0). Set before adding tiles.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// World Y of the bottom edge of cell (0, 0). Set before adding tiles.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Width and height of each tile in world units. Defaults to 16, which is the standard tile size
    /// for FlatRedBall2 games. Set before adding tiles.
    /// </summary>
    public float GridSize { get; set; } = 16f;

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
        }
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
    /// Does nothing if any tile (rectangle or polygon) already exists at that cell.
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
    public void AddPolygonTileAtCell(int col, int row, Polygon prototype)
    {
        if (_tiles.ContainsKey((col, row)) || _polyTiles.ContainsKey((col, row))) return;

        var poly = Polygon.FromPoints(prototype.Points);
        poly.X = X + col * GridSize + GridSize / 2f;
        poly.Y = Y + row * GridSize + GridSize / 2f;
        poly.IsVisible = _isVisible;
        poly.Color = _color;
        poly.IsFilled = _isFilled;
        poly.OutlineThickness = _outlineThickness;

        _polyTiles[(col, row)] = poly;
        _onTileAdded?.Invoke(poly);
    }

    /// <summary>
    /// Removes the polygon tile at the given grid cell. Does nothing if no polygon tile exists there.
    /// </summary>
    public void RemovePolygonTileAtCell(int col, int row)
    {
        if (!_polyTiles.TryGetValue((col, row), out var poly)) return;
        _polyTiles.Remove((col, row));
        _onTileRemoved?.Invoke(poly);
    }

    /// <summary>
    /// Returns the <see cref="Polygon"/> tile at the given grid cell, or <c>null</c> if no polygon
    /// tile exists there.
    /// </summary>
    public Polygon? GetPolygonTileAtCell(int col, int row) =>
        _polyTiles.TryGetValue((col, row), out var poly) ? poly : null;

    private (int col, int row) WorldToCell(float x, float y) =>
        ((int)MathF.Floor((x - X) / GridSize), (int)MathF.Floor((y - Y) / GridSize));

    private void UpdateDirectionsAround(int col, int row)
    {
        UpdateCellDirections(col, row);
        UpdateCellDirections(col - 1, row);
        UpdateCellDirections(col + 1, row);
        UpdateCellDirections(col, row - 1);
        UpdateCellDirections(col, row + 1);
    }

    private void UpdateNeighborDirections(int col, int row)
    {
        UpdateCellDirections(col - 1, row);
        UpdateCellDirections(col + 1, row);
        UpdateCellDirections(col, row - 1);
        UpdateCellDirections(col, row + 1);
    }

    private void UpdateCellDirections(int col, int row)
    {
        if (!_tiles.TryGetValue((col, row), out var tile)) return;

        var dirs = RepositionDirections.All;
        if (_tiles.ContainsKey((col - 1, row))) dirs &= ~RepositionDirections.Left;
        if (_tiles.ContainsKey((col + 1, row))) dirs &= ~RepositionDirections.Right;
        if (_tiles.ContainsKey((col, row - 1))) dirs &= ~RepositionDirections.Down;
        if (_tiles.ContainsKey((col, row + 1))) dirs &= ~RepositionDirections.Up;

        tile.RepositionDirections = dirs;
    }

    // Returns the total separation vector needed to move 'shape' out of all overlapping tiles.
    // Called by CollisionDispatcher when this collection is the static geometry side.
    internal Vector2 GetSeparationFor(ICollidable shape)
    {
        var (minX, maxX, minY, maxY) = CollisionDispatcher.GetBounds(shape);

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
                    sep = CollisionDispatcher.GetSeparationVector(shape, tile);
                else if (_polyTiles.TryGetValue((col, row), out var poly))
                    sep = CollisionDispatcher.GetSeparationVector(shape, poly);
                else
                    continue;

                if (sep == Vector2.Zero) continue;

                // Take the largest push on each axis independently to avoid double-counting
                // when the shape overlaps multiple tiles on the same side.
                if (MathF.Abs(sep.X) > MathF.Abs(total.X))
                    total = new Vector2(sep.X, total.Y);
                if (MathF.Abs(sep.Y) > MathF.Abs(total.Y))
                    total = new Vector2(total.X, sep.Y);
            }
        }

        return total;
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
    /// Only tiles along the ray path are tested. If <paramref name="start"/> is already inside a tile, returns <c>false</c>.
    /// </remarks>
    public bool Raycast(Vector2 start, Vector2 end, out Vector2 hitPoint, out Vector2 hitNormal)
    {
        hitPoint = Vector2.Zero;
        hitNormal = Vector2.Zero;

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

        while (true)
        {
            float t;
            Vector2 normal;

            if (tMaxX <= tMaxY)
            {
                t = tMaxX;
                col += stepX;
                normal = new Vector2(-stepX, 0);
                tMaxX += tDeltaX;
            }
            else
            {
                t = tMaxY;
                row += stepY;
                normal = new Vector2(0, -stepY);
                tMaxY += tDeltaY;
            }

            if (t > 1f) break;

            if (_tiles.ContainsKey((col, row)))
            {
                hitPoint = start + dir * t;
                hitNormal = normal;
                return true;
            }

            if (_polyTiles.TryGetValue((col, row), out var polyTile))
            {
                if (polyTile.Raycast(start, end, out hitPoint, out hitNormal))
                    return true;
            }
        }

        return false;
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
}
