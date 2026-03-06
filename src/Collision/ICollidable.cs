using System.Numerics;

namespace FlatRedBall2.Collision;

public interface ICollidable
{
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
    Vector2 GetSeparationVector(ICollidable other);
    void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f);
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
}
