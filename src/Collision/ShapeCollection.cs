using System.Collections.Generic;
using System.Numerics;

namespace FlatRedBall2.Collision;

/// <summary>
/// A heterogeneous bag of collision shapes that act as a single <see cref="ICollidable"/>
/// for query and response purposes. Use when several primitives logically form one collider
/// but you don't want to attach them to an entity (e.g., authored static geometry).
/// </summary>
/// <remarks>
/// For grid-aligned static tile geometry use <see cref="TileShapeCollection"/> instead — it
/// has spatial partitioning and adjacency-aware <see cref="RepositionDirections"/> updates.
/// <see cref="BroadPhaseRadius"/> is <see cref="float.MaxValue"/> because a collection has no
/// single meaningful center, so every broad-phase pair against this collection is checked.
/// </remarks>
public class ShapeCollection : ICollidable
{
    private readonly List<ICollidable> _shapes = new();

    /// <summary>Adds a rectangle to the collection.</summary>
    public void Add(AxisAlignedRectangle rect) => _shapes.Add(rect);
    /// <summary>Adds a circle to the collection.</summary>
    public void Add(Circle circle) => _shapes.Add(circle);
    /// <summary>Adds a line segment to the collection.</summary>
    public void Add(Line line) => _shapes.Add(line);
    /// <summary>Adds a polygon to the collection.</summary>
    public void Add(Polygon polygon) => _shapes.Add(polygon);

    /// <summary>Always <c>0</c> — a collection has no single meaningful center.</summary>
    public float AbsoluteX => 0f;
    /// <summary>Always <c>0</c> — a collection has no single meaningful center.</summary>
    public float AbsoluteY => 0f;
    /// <inheritdoc/>
    public float BroadPhaseRadius => float.MaxValue;

    /// <inheritdoc/>
    public bool CollidesWith(ICollidable other)
    {
        foreach (var shape in _shapes)
            if (shape.CollidesWith(other))
                return true;
        return false;
    }

    /// <summary>
    /// Returns the first non-zero separation found across all shapes in the collection — not a
    /// combined MTV across multiple overlapping shapes.
    /// </summary>
    public Vector2 GetSeparationVector(ICollidable other)
    {
        foreach (var shape in _shapes)
        {
            var sep = shape.GetSeparationVector(other);
            if (sep != Vector2.Zero) return sep;
        }
        return Vector2.Zero;
    }

    /// <summary>
    /// ShapeCollection is treated as static geometry — only <paramref name="other"/> is moved.
    /// </summary>
    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f)
    {
        foreach (var shape in _shapes)
            if (shape.CollidesWith(other))
                other.SeparateFrom(shape, otherMass, thisMass);
    }

    /// <summary>No-op — ShapeCollection is treated as static geometry.</summary>
    public void ApplySeparationOffset(Vector2 offset) { }

    /// <summary>
    /// Reflects <paramref name="other"/>'s velocity off each overlapping shape in the collection.
    /// The collection itself is treated as immovable static geometry.
    /// </summary>
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f)
    {
        foreach (var shape in _shapes)
            if (shape.CollidesWith(other))
                other.AdjustVelocityFrom(shape, otherMass, thisMass, elasticity);
    }

    /// <summary>No-op — see <see cref="AdjustVelocityFrom"/>.</summary>
    public void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
}
