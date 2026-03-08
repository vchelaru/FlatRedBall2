using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ShmupSample.Entities;

public class EnemyBullet : Entity
{
    public Circle CollisionCircle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        CollisionCircle = new Circle
        {
            Radius = 5,
            Color = new Color(255, 80, 80, 230),
            IsFilled = true,
            IsVisible = true,
        };
        Add(CollisionCircle);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Destroy when off the bottom of the screen
        if (Y < -(Engine.CurrentScreen.Camera.TargetHeight / 2f + 30f))
            Destroy();
    }

    public override void CustomDestroy()
    {
        CollisionCircle.Destroy();
    }
}
