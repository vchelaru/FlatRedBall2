using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

public class Ball : Entity
{
    public Circle Circle { get; private set; } = null!;

    private float _trailTimer;

    public override void CustomInitialize()
    {
        Circle = new Circle
        {
            Radius = 9f,
            Color = new Color(255, 255, 200),
            Visible = true,
        };
        Add(Circle);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Spawn trail particles behind the ball
        _trailTimer -= time.DeltaSeconds;
        if (_trailTimer <= 0f)
        {
            _trailTimer = 0.03f;
            var particle = Engine.GetFactory<TrailParticle>().Create();
            particle.X = X;
            particle.Y = Y;
        }
    }
}
