namespace FlatRedBall2.Movement;

/// <summary>
/// Implemented by entities that own a <see cref="PlatformerBehavior"/>. Enables
/// <see cref="FlatRedBall2.Collision.CollisionRelationship{A,B}"/> with
/// <see cref="FlatRedBall2.Collision.SlopeCollisionMode.PlatformerFloor"/> to contribute its
/// <see cref="FlatRedBall2.Collision.TileShapeCollection"/> as a ground-snap probe target automatically —
/// no manual wiring of a snap target on the behavior required.
/// </summary>
public interface IPlatformerEntity
{
    /// <summary>The behavior driving this entity's platformer movement. Must never be null
    /// once the entity is registered with the engine — the collision system dereferences it
    /// during ground-snap and slope-probe dispatch.</summary>
    PlatformerBehavior Platformer { get; }
}
