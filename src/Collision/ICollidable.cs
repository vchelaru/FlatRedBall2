using System.Numerics;

namespace FlatRedBall2.Collision;

public interface ICollidable
{
    bool CollidesWith(ICollidable other);
    Vector2 GetSeparationVector(ICollidable other);
    void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f);
    void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f);
}
