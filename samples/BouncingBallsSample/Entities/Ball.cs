using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace BouncingBallsSample.Entities;

public class Ball : Entity
{
    public Circle Circle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Circle = new Circle { Radius = 14f, IsVisible = true };
        Add(Circle);
        AccelerationY = -500f;
    }
}
