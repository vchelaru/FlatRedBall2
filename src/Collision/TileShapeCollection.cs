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

    /// <summary>
    /// Controls how polygon tiles resolve overlap. <see cref="SlopeCollisionMode.Standard"/>
    /// uses SAT (correct for top-down). <see cref="SlopeCollisionMode.PlatformerFloor"/>
    /// uses vertical-only heightmap separation (correct for platformer slopes).
    /// </summary>
    public SlopeCollisionMode SlopeMode { get; set; } = SlopeCollisionMode.Standard;

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
    /// Removes all tiles (rectangles and polygons) from this collection.
    /// After clearing, <see cref="X"/>, <see cref="Y"/>, and <see cref="GridSize"/> can be changed again.
    /// </summary>
    public void Clear()
    {
        foreach (var tile in _tiles.Values)
            _onTileRemoved?.Invoke(tile);
        foreach (var poly in _polyTiles.Values)
            _onTileRemoved?.Invoke(poly);
        _tiles.Clear();
        _polyTiles.Clear();
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

    // Returns the total separation vector needed to move 'shape' out of all overlapping tiles.
    // Called by CollisionDispatcher when this collection is the static geometry side.
    internal Vector2 GetSeparationFor(ICollidable shape)
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
                    if (SlopeMode == SlopeCollisionMode.PlatformerFloor)
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
                    if (SlopeMode == SlopeCollisionMode.PlatformerFloor)
                        sep = GetHeightmapSeparation(poly, minX, maxX, minY, maxY);
                    else
                        sep = CollisionDispatcher.GetSeparationVector(shape, poly);
                }
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

    // Heightmap-based vertical separation for platformer slopes.
    // Computes the polygon's surface height at the shape's center X and pushes up if needed.
    private static Vector2 GetHeightmapSeparation(Polygon poly, float shapeMinX, float shapeMaxX, float shapeMinY, float shapeMaxY)
    {
        float centerX = (shapeMinX + shapeMaxX) / 2f;
        float bottomY = shapeMinY;

        // Find the polygon's surface Y at centerX by checking each edge.
        // The surface is the highest Y where an edge spans centerX.
        float surfaceY = float.MinValue;
        var pts = poly.Points;
        for (int i = 0; i < pts.Count; i++)
        {
            // World-space vertices (polygon tiles have no rotation)
            float ax = poly.X + pts[i].X;
            float ay = poly.Y + pts[i].Y;
            int next = (i + 1) % pts.Count;
            float bx = poly.X + pts[next].X;
            float by = poly.Y + pts[next].Y;

            // Does this edge span centerX?
            if ((ax <= centerX && bx >= centerX) || (bx <= centerX && ax >= centerX))
            {
                float range = bx - ax;
                if (MathF.Abs(range) < 1e-6f)
                {
                    // Vertical edge — take the higher Y
                    surfaceY = MathF.Max(surfaceY, MathF.Max(ay, by));
                }
                else
                {
                    float t = (centerX - ax) / range;
                    float y = ay + t * (by - ay);
                    if (y > surfaceY) surfaceY = y;
                }
            }
        }

        if (surfaceY == float.MinValue) return Vector2.Zero; // centerX outside polygon edges
        if (bottomY >= surfaceY) return Vector2.Zero; // shape is above the surface

        return new Vector2(0f, surfaceY - bottomY);
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
    }

    private void ThrowIfTilesExist()
    {
        if (_tiles.Count > 0 || _polyTiles.Count > 0)
            throw new InvalidOperationException(
                "Cannot change GridSize after tiles have been added. Call Clear() first, change the value, then re-add tiles.");
    }
}
