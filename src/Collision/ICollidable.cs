using System.Numerics;

namespace FlatRedBall2.Collision;

/// <summary>
/// Anything that can participate in collision: primitive shapes
/// (<see cref="AxisAlignedRectangle"/>, <see cref="Circle"/>, <see cref="Polygon"/>,
/// <see cref="Line"/>), <see cref="Entity"/> (an aggregate of leaf shapes), and static
/// geometry (<see cref="TileShapeCollection"/>).
/// </summary>
/// <remarks>
/// Most game code interacts with collision via
/// <see cref="Screen.AddCollisionRelationship{A,B}(System.Collections.Generic.IReadOnlyList{A}, System.Collections.Generic.IReadOnlyList{B})"/>
/// or <see cref="Entity.CollidesWith"/> rather than calling these methods directly. The
/// per-shape methods exist for advanced/manual collision flows.
/// </remarks>
public interface ICollidable
{
    /// <summary>Final world-space X after walking the parent chain (for attached shapes).</summary>
    float AbsoluteX { get; }
    /// <summary>Final world-space Y (Y+ up) after walking the parent chain.</summary>
    float AbsoluteY { get; }

    /// <summary>
    /// Conservative bounding radius used by sweep-and-prune broad phase.
    /// Returns <see cref="float.MaxValue"/> for shapes with no meaningful single center (e.g. <see cref="TileShapeCollection"/>).
    /// </summary>
    float BroadPhaseRadius { get; }

    /// <summary>
    /// Returns whether <paramref name="worldPoint"/> lies inside this shape (boundary inclusive).
    /// Implemented on primitive shapes (<see cref="AxisAlignedRectangle"/>, <see cref="Circle"/>,
    /// <see cref="Polygon"/>) for hit-testing — e.g. <c>cursor.IsOver(shape)</c>. The default
    /// implementation returns <c>false</c>; aggregate types (<see cref="Entity"/>,
    /// <see cref="TileShapeCollection"/>) are tested via dedicated entry points instead.
    /// </summary>
    bool Contains(Vector2 worldPoint) => false;

    /// <summary>
    /// Tests whether this object overlaps <paramref name="other"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations on <see cref="Entity"/> and <see cref="TileShapeCollection"/> iterate leaf shapes and handle
    /// any <c>ICollidable</c> as <paramref name="other"/>. Implementations on primitive shapes (e.g.
    /// <see cref="AxisAlignedRectangle"/>) delegate directly to <c>CollisionDispatcher</c>, which only recognises
    /// primitive shape types and <c>TileShapeCollection</c> — passing an <see cref="Entity"/> as <paramref name="other"/>
    /// from a primitive shape will return <c>false</c>. Always call this method on an <see cref="Entity"/> or
    /// <see cref="TileShapeCollection"/> when the other side may be an entity.
    /// </para>
    /// </remarks>
    bool CollidesWith(ICollidable other);

    /// <summary>
    /// Returns the minimum translation vector that pushes this object out of <paramref name="other"/>.
    /// Returns <see cref="Vector2.Zero"/> when there is no overlap.
    /// </summary>
    Vector2 GetSeparationVector(ICollidable other);

    /// <summary>
    /// Pushes this object out of <paramref name="other"/> using the mass-weighted share of the
    /// separation vector. See <see cref="Entity.SeparateFrom"/> for mass-ratio semantics.
    /// No-op on shapes that have no movable position of their own (e.g. <see cref="TileShapeCollection"/>).
    /// </summary>
    void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f);

    /// <summary>
    /// Reflects this object's velocity off <paramref name="other"/> using impulse physics.
    /// See <see cref="Entity.AdjustVelocityFrom"/> for mass and elasticity semantics.
    /// No-op on shapes (only <see cref="Entity"/> carries velocity).
    /// </summary>
    void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f);

    /// <summary>
    /// Directly applies a precomputed separation offset to this object.
    /// Entity: adjusts <c>Position</c> and <c>LastReposition</c>. Shapes: adjusts <c>X</c>/<c>Y</c>. Static geometry: no-op.
    /// Used by <see cref="Collision.CollisionRelationship{A,B}"/> when a per-shape selector is active so the
    /// response is applied to the owning entity rather than the selected child shape.
    /// </summary>
    void ApplySeparationOffset(Vector2 offset);

    /// <summary>
    /// Adjusts this object's velocity using a precomputed separation vector as the collision normal.
    /// Entity: applies impulse physics against <paramref name="other"/>. Shapes and static geometry: no-op.
    /// Used by <see cref="Collision.CollisionRelationship{A,B}"/> when a per-shape selector is active.
    /// </summary>
    void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f);

    /// <summary>
    /// Overload of <see cref="AdjustVelocityFromSeparation(Vector2, ICollidable, float, float, float)"/>
    /// that lets the caller flag <paramref name="sep"/> as a sum of axis-aligned per-tile
    /// contacts (wall + floor corner). When true and both components are non-zero, Entity
    /// applies per-axis impulses instead of a single diagonal normal — which is needed for
    /// corner contact but wrong for a genuine diagonal normal (slope polygon SAT). Default
    /// implementation delegates to the original signature; only Entity needs the override.
    /// </summary>
    void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass, float otherMass, float elasticity, bool axisAlignedSeparation)
        => AdjustVelocityFromSeparation(sep, other, thisMass, otherMass, elasticity);
}
