using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ZombieSample.Entities;

public class Zombie : Entity
{
    private const float Speed = 80f;

    private static readonly Color ZombieColor = new(80, 180, 60, 230);

    private Circle  _circle = null!;
    private Player? _target;

    public void SetTarget(Player player) => _target = player;

    public override void CustomInitialize()
    {
        _circle = new Circle { Radius = 18f, IsVisible = true, Color = ZombieColor };
        Add(_circle);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_target == null || _target.IsDead)
        {
            VelocityX = 0f;
            VelocityY = 0f;
            return;
        }

        float dx  = _target.X - X;
        float dy  = _target.Y - Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);

        if (len > 0.1f)
        {
            VelocityX = dx / len * Speed;
            VelocityY = dy / len * Speed;
        }
    }

    public override void CustomDestroy()
    {
        _circle.Destroy();
    }
}
