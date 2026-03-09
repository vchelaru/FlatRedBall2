namespace FlatRedBall2.Collision;

internal interface ICollisionRelationship
{
    void RunCollisions();
    int DeepCollisionCount { get; }
}
