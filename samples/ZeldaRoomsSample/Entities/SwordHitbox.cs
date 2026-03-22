using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ZeldaRoomsSample.Entities;

public class SwordHitbox : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    // How long the hitbox stays active (seconds)
    private float _lifetime = 0f;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 48f,
            Height = 48f,
            Color = new Color(255, 220, 60, 200),
            IsVisible = true,
        };
        Add(Rectangle);
    }

    public void Activate(float duration)
    {
        _lifetime = duration;
    }

    public override void CustomActivity(FrameTime time)
    {
        _lifetime -= time.DeltaSeconds;
        if (_lifetime <= 0f)
            Destroy();
    }
}
