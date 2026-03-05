using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ShmupSample.Entities;

public class PlayerBullet : Entity
{
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    private const float ScreenBoundY = 400f;  // destroy when off-screen

    public override void CustomInitialize()
    {
        CollisionRect = new AxisAlignedRectangle
        {
            Width = 5,
            Height = 16,
            Color = new Color(0, 220, 255, 255),
            IsFilled = true,
            Visible = true,
        };
        Add(CollisionRect);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Destroy when off the top of the screen
        if (Y > Engine.CurrentScreen.Camera.TargetHeight / 2f + 30f)
            Destroy();
    }

    public override void CustomDestroy()
    {
        CollisionRect.Destroy();
    }
}
