using System;
using System.Collections.Generic;
using System.Numerics;

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

    /// <summary>
    /// World X of the left edge of cell (0, 0). Set before adding tiles.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// World Y of the bottom edge of cell (0, 0). Set before adding tiles.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    /// Width and height of each tile in world units. Defaults to 16. Set before adding tiles.
    /// </summary>
    public float GridSize { get; set; } = 16f;

    private bool _visible;

    // Invoked when a tile rect is created or destroyed so Screen.Add can keep its render list in sync.
    internal Action<AxisAlignedRectangle>? _onTileAdded;
    internal Action<AxisAlignedRectangle>? _onTileRemoved;

    /// <summary>
    /// Returns all tile rectangles currently in this collection. Used by
    /// <see cref="Screen.Add(TileShapeCollection)"/> to register tiles for rendering.
    /// </summary>
    internal IEnumerable<AxisAlignedRectangle> AllTiles => _tiles.Values;

    /// <summary>
    /// Shows or hides all tile rectangles. Defaults to false.
    /// </summary>
    /// <remarks>
    /// Call <c>Screen.Add(tiles)</c> first so tiles are registered for rendering; then set
    /// <c>Visible = true</c> to make them appear. Tiles added after <c>Screen.Add</c> inherit
    /// the current <see cref="Visible"/> value automatically.
    /// </remarks>
    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            foreach (var tile in _tiles.Values)
                tile.Visible = value;
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
            Visible = _visible,
            IsFilled = false,
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
                if (!_tiles.TryGetValue((col, row), out var tile)) continue;

                var sep = CollisionDispatcher.GetSeparationVector(shape, tile);
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

    // ICollidable — TileShapeCollection is static geometry; only the querying shape moves.
    public bool CollidesWith(ICollidable other) => GetSeparationFor(other) != Vector2.Zero;
    public Vector2 GetSeparationVector(ICollidable other) => GetSeparationFor(other);
    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f) { }
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
}
