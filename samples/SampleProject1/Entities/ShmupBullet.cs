using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

public class ShmupBullet : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    private const float MaxLifetime = 3f;
    private float _lifetime;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 6,
            Height = 10,
            Color = Color.Yellow,
            IsVisible = true,
            IsFilled = true,
        };
        Add(Rectangle);
    }

    public override void CustomActivity(FrameTime time)
    {
        _lifetime += time.DeltaSeconds;
        if (_lifetime >= MaxLifetime)
            Destroy();
    }
}
