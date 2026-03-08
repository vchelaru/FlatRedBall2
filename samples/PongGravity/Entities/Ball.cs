using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace PongGravity.Entities;

public class Ball : Entity
{
    public Circle Circle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Circle = new Circle { Radius = 12f, IsVisible = true, Color = new Color(255, 240, 80, 255) };
        Add(Circle);
    }

    public void Launch()
    {
        float sign = Engine.Random.NextSign();
        float angle = Engine.Random.Between(-MathF.PI / 5f, MathF.PI / 5f);
        const float Speed = 350f;
        VelocityX = sign * MathF.Cos(angle) * Speed;
        VelocityY = MathF.Sin(angle) * Speed;
    }
}
