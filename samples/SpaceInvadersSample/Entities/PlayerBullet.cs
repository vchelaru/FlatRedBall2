using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SpaceInvadersSample.Entities;

public class PlayerBullet : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 4f,
            Height = 16f,
            IsVisible = true,
            Color = new XnaColor(255, 255, 80, 255),
        };
        Add(Rectangle);

        VelocityY = 550f;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Y > 390f)
            Destroy();
    }

    public override void CustomDestroy()
    {
        Rectangle.Destroy();
    }
}
