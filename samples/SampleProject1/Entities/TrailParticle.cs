using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

/// <summary>
/// Short-lived circle that fades and shrinks behind the ball to create a motion trail.
/// </summary>
public class TrailParticle : Entity
{
    private const float MaxLifetime = 0.2f;

    private Circle _circle = null!;
    private float _lifetime = MaxLifetime;

    public override void CustomInitialize()
    {
        _circle = new Circle
        {
            Radius = 8f,
            Visible = true,
        };
        AddChild(_circle);
    }

    public override void CustomActivity(FrameTime time)
    {
        _lifetime -= time.DeltaSeconds;
        if (_lifetime <= 0f)
        {
            Destroy();
            return;
        }

        float frac = _lifetime / MaxLifetime;
        byte alpha = (byte)(180 * frac);
        _circle.Color = new Color((byte)255, (byte)220, (byte)120, alpha);
        _circle.Radius = 8f * frac;
    }
}
