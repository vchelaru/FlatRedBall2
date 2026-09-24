using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace FlatRedBall2.Collision;

/// <summary>
/// Sparse static collision geometry made of occupied cells in a <see cref="HexGrid"/>.
/// </summary>
public sealed class HexShapes : ICollidable, IRenderable
{
    private readonly Dictionary<HexCoordinate, Polygon> _hexes = new();
    private readonly List<HexCoordinate> _orderedCoordinates = new();
    // Reused per query so collision against hexes allocates nothing per frame.
    private readonly List<HexCoordinate> _candidates = new();
    private AARect? _scratchRectangle;
    private Circle? _scratchCircle;
    private Polygon? _scratchPolygon;

    /// <summary>Creates an empty collection using <paramref name="grid"/>'s immutable layout.</summary>
    public HexShapes(HexGrid grid) => Grid = grid ?? throw new ArgumentNullException(nameof(grid));

    /// <summary>The immutable layout used to position every occupied cell.</summary>
    public HexGrid Grid { get; }
    /// <summary>The number of occupied cells.</summary>
    public int Count => _hexes.Count;
    /// <inheritdoc/>
    public float AbsoluteX => Grid.Origin.X;
    /// <inheritdoc/>
    public float AbsoluteY => Grid.Origin.Y;
    /// <inheritdoc/>
    public float BroadPhaseRadius => float.MaxValue;
    /// <inheritdoc/>
    public float Z { get; set; }
    /// <inheritdoc/>
    public Layer? Layer { get; set; }
    /// <inheritdoc/>
    public IRenderBatch Batch { get; } = ShapesBatch.Instance;
    /// <inheritdoc/>
    public string? Name { get; set; }
    /// <summary>Whether occupied cells are drawn. Collision does not depend on this value.</summary>
    public bool IsVisible { get; set; }
    /// <summary>Color used when drawing occupied cells.</summary>
    public XnaColor Color { get; set; } = XnaColor.White;
    /// <summary>Whether occupied cells are filled when drawn.</summary>
    public bool IsFilled { get; set; }
    /// <summary>Outline thickness used when <see cref="IsFilled"/> is false.</summary>
    public float OutlineThickness { get; set; } = 1f;

    /// <summary>Adds a solid hex at <paramref name="coordinate"/>. Duplicate calls are ignored.</summary>
    public void AddHexAtCell(HexCoordinate coordinate)
    {
        if (_hexes.ContainsKey(coordinate)) return;

        var polygon = Polygon.FromPoints(Grid.GetCellCorners(coordinate));
        _hexes.Add(coordinate, polygon);
        _orderedCoordinates.Insert(GetCoordinateInsertIndex(coordinate), coordinate);
    }

    /// <summary>Adds a solid hex at the cell containing <paramref name="worldPosition"/>.</summary>
    public void AddHexAtWorld(Vector2 worldPosition) => AddHexAtCell(Grid.GetCellAt(worldPosition));

    /// <summary>Removes all occupied cells.</summary>
    public void Clear()
    {
        _hexes.Clear();
        _orderedCoordinates.Clear();
    }

    /// <summary>Returns whether <paramref name="coordinate"/> is occupied.</summary>
    public bool ContainsCell(HexCoordinate coordinate) => _hexes.ContainsKey(coordinate);

    /// <inheritdoc/>
    public bool Contains(Vector2 worldPoint)
    {
        foreach (var polygon in _hexes.Values)
            if (polygon.Contains(worldPoint))
                return true;
        return false;
    }

    /// <inheritdoc/>
    public bool CollidesWith(ICollidable other)
    {
        if (other is HexShapes || other is Line) return false;

        if (other is Entity)
        {
            foreach (var shape in Entity.GetLeafShapes(other))
                if (CollidesWithLeaf(shape))
                    return true;
            return false;
        }

        return CollidesWithLeaf(other);
    }

    private bool CollidesWithLeaf(ICollidable shape)
    {
        GetCandidateCoordinates(shape, _candidates);
        foreach (var coordinate in _candidates)
            if (CollisionDispatcher.CollidesWith(shape, _hexes[coordinate]))
                return true;
        return false;
    }

    /// <inheritdoc/>
    public Vector2 GetSeparationVector(ICollidable other) => -GetSeparationFor(other);

    /// <inheritdoc/>
    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f) { }

    /// <inheritdoc/>
    public void ApplySeparationOffset(Vector2 offset) { }

    /// <inheritdoc/>
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }

    /// <inheritdoc/>
    public void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }

    /// <summary>Draws all occupied cells as one renderable.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible) return;
        foreach (var polygon in _hexes.Values)
        {
            polygon.Color = Color;
            polygon.IsFilled = IsFilled;
            polygon.OutlineThickness = OutlineThickness;
            polygon.Layer = Layer;
            polygon.Z = Z;
            polygon.IsVisible = true;
            polygon.Draw(spriteBatch, camera);
        }
    }

    /// <summary>Removes the cell at <paramref name="coordinate"/>. Missing cells are ignored.</summary>
    public void RemoveHexAtCell(HexCoordinate coordinate)
    {
        if (!_hexes.Remove(coordinate)) return;
        _orderedCoordinates.RemoveAt(GetCoordinateIndex(coordinate));
    }

    /// <summary>Removes the cell containing <paramref name="worldPosition"/>.</summary>
    public void RemoveHexAtWorld(Vector2 worldPosition) => RemoveHexAtCell(Grid.GetCellAt(worldPosition));

    internal Vector2 GetSeparationFor(ICollidable shape)
    {
        if (shape is not Entity) return GetSeparationForLeaf(shape);

        foreach (var leaf in Entity.GetLeafShapes(shape))
        {
            var separation = GetSeparationForLeaf(leaf);
            if (separation != Vector2.Zero)
                return separation;
        }

        return Vector2.Zero;
    }

    private Vector2 GetSeparationForLeaf(ICollidable shape)
    {
        // The first pass doubles as the overlap test: no penetration returns zero.
        var provisional = CopyToScratch(shape);
        if (provisional == null) return Vector2.Zero;

        Vector2 totalSeparation = Vector2.Zero;
        const int maximumIterations = 12;
        const float progressToleranceSquared = 0.000001f;

        for (int iteration = 0; iteration < maximumIterations; iteration++)
        {
            bool foundPenetration = false;
            GetCandidateCoordinates(provisional, _candidates);
            foreach (var coordinate in _candidates)
            {
                var polygon = _hexes[coordinate];
                if (!CollisionDispatcher.CollidesWith(provisional, polygon)) continue;

                var separation = CollisionDispatcher.GetSeparationVector(provisional, polygon);
                if (separation.LengthSquared() <= progressToleranceSquared)
                    continue;
                if (!float.IsFinite(separation.X) || !float.IsFinite(separation.Y))
                    return Vector2.Zero;

                foundPenetration = true;
                provisional.ApplySeparationOffset(separation);
                totalSeparation += separation;
                if (!float.IsFinite(totalSeparation.X) || !float.IsFinite(totalSeparation.Y))
                    return Vector2.Zero;
                break;
            }

            if (!foundPenetration) return totalSeparation;
        }

        return Vector2.Zero;
    }

    // Fills result (cleared first) with occupied cells whose area may overlap shape's bounds.
    internal void GetCandidateCoordinates(ICollidable shape, List<HexCoordinate> result)
    {
        result.Clear();
        var (minX, maxX, minY, maxY) = CollisionDispatcher.GetBounds(shape);
        if (!float.IsFinite(minX) || !float.IsFinite(maxX) || !float.IsFinite(minY) || !float.IsFinite(maxY))
            return;

        var lowerLeft = Grid.GetCellAtClamped(new Vector2(minX, minY));
        var lowerRight = Grid.GetCellAtClamped(new Vector2(maxX, minY));
        var upperLeft = Grid.GetCellAtClamped(new Vector2(minX, maxY));
        var upperRight = Grid.GetCellAtClamped(new Vector2(maxX, maxY));
        int minQ = ClampToInt((long)System.Math.Min(System.Math.Min(lowerLeft.Q, lowerRight.Q), System.Math.Min(upperLeft.Q, upperRight.Q)) - 2);
        int maxQ = ClampToInt((long)System.Math.Max(System.Math.Max(lowerLeft.Q, lowerRight.Q), System.Math.Max(upperLeft.Q, upperRight.Q)) + 2);
        int minR = ClampToInt((long)System.Math.Min(System.Math.Min(lowerLeft.R, lowerRight.R), System.Math.Min(upperLeft.R, upperRight.R)) - 2);
        int maxR = ClampToInt((long)System.Math.Max(System.Math.Max(lowerLeft.R, lowerRight.R), System.Math.Max(upperLeft.R, upperRight.R)) + 2);
        long candidateWidth = (long)maxQ - minQ + 1;
        long candidateHeight = (long)maxR - minR + 1;

        if (CandidateRangeExceedsOccupancy(candidateWidth, candidateHeight))
        {
            foreach (var coordinate in _orderedCoordinates)
                if (CellBoundsOverlap(coordinate, minX, maxX, minY, maxY))
                    result.Add(coordinate);
            return;
        }

        for (long q = minQ; q <= maxQ; q++)
            for (long r = minR; r <= maxR; r++)
            {
                var coordinate = new HexCoordinate((int)q, (int)r);
                if (_hexes.ContainsKey(coordinate))
                    result.Add(coordinate);
            }
    }

    private bool CandidateRangeExceedsOccupancy(long width, long height)
    {
        if (width > _hexes.Count || height > _hexes.Count)
            return true;
        return width * height > _hexes.Count;
    }

    private static int ClampToInt(long value) =>
        value < int.MinValue ? int.MinValue : value > int.MaxValue ? int.MaxValue : (int)value;

    private bool CellBoundsOverlap(HexCoordinate coordinate, float minX, float maxX, float minY, float maxY)
    {
        var center = Grid.GetCellCenter(coordinate);
        return center.X + Grid.Radius >= minX && center.X - Grid.Radius <= maxX
            && center.Y + Grid.Radius >= minY && center.Y - Grid.Radius <= maxY;
    }

    private int GetCoordinateIndex(HexCoordinate coordinate)
    {
        int low = 0;
        int high = _orderedCoordinates.Count - 1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            int comparison = CompareCoordinates(_orderedCoordinates[middle], coordinate);
            if (comparison == 0) return middle;
            if (comparison < 0) low = middle + 1;
            else high = middle - 1;
        }
        return ~low;
    }

    private int GetCoordinateInsertIndex(HexCoordinate coordinate)
    {
        int index = GetCoordinateIndex(coordinate);
        return index >= 0 ? index : ~index;
    }

    private static int CompareCoordinates(HexCoordinate first, HexCoordinate second)
    {
        int q = first.Q.CompareTo(second.Q);
        return q != 0 ? q : first.R.CompareTo(second.R);
    }

    // Copies shape's world geometry into a reused scratch shape the resolver can move freely.
    // Returns null for shapes hex collision doesn't resolve (Line).
    private ICollidable? CopyToScratch(ICollidable shape)
    {
        switch (shape)
        {
            case AARect rectangle:
                _scratchRectangle ??= new AARect();
                _scratchRectangle.Width = rectangle.Width;
                _scratchRectangle.Height = rectangle.Height;
                _scratchRectangle.X = rectangle.AbsoluteX;
                _scratchRectangle.Y = rectangle.AbsoluteY;
                return _scratchRectangle;
            case Circle circle:
                _scratchCircle ??= new Circle();
                _scratchCircle.Radius = circle.Radius;
                _scratchCircle.X = circle.AbsoluteX;
                _scratchCircle.Y = circle.AbsoluteY;
                return _scratchCircle;
            case Polygon polygon:
                _scratchPolygon ??= new Polygon();
                // SetPoints rebuilds convex parts, so only call it when the source points changed.
                if (!PointsEqual(_scratchPolygon.Points, polygon.Points))
                    _scratchPolygon.SetPoints(polygon.Points);
                _scratchPolygon.X = polygon.AbsoluteX;
                _scratchPolygon.Y = polygon.AbsoluteY;
                _scratchPolygon.Rotation = polygon.AbsoluteRotation;
                return _scratchPolygon;
            default:
                return null;
        }
    }

    private static bool PointsEqual(IReadOnlyList<Vector2> first, IReadOnlyList<Vector2> second)
    {
        if (first.Count != second.Count) return false;
        for (int i = 0; i < first.Count; i++)
            if (first[i] != second[i])
                return false;
        return true;
    }
}
