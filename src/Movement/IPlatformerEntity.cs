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
    PlatformerBehavior Platformer { get; }
}
