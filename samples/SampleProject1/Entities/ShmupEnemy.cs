using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

public class ShmupEnemy : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;
    public int PointValue { get; set; } = 100;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 28,
            Height = 28,
            Color = new Color(220, 60, 60),
            IsVisible = true,
            IsFilled = true,
        };
        Add(Rectangle);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Slow downward drift so enemies aren't purely stationary
        VelocityY = -20f;
    }
}
