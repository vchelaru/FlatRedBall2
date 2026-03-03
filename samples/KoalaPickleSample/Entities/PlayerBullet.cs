using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace KoalaPickleSample.Entities;

public class PlayerBullet : Entity
{
    private const float BulletSpeed = 600f;
    private const float Lifetime = 3f;

    private AxisAlignedRectangle _rect = null!;
    private float _remainingLife = Lifetime;

    public AxisAlignedRectangle Rectangle => _rect;

    /// <summary>Call immediately after Create() to set direction (+1 right, -1 left).</summary>
    public void Launch(float directionX, float originX, float originY)
    {
        X = originX;
        Y = originY;
        VelocityX = directionX * BulletSpeed;
    }

    public override void CustomInitialize()
    {
        _rect = new AxisAlignedRectangle
        {
            Width = 12f,
            Height = 6f,
            Visible = true,
            Color = new XnaColor(255, 130, 180, 255),
        };
        AddChild(_rect);
    }

    public override void CustomActivity(FrameTime time)
    {
        _remainingLife -= time.DeltaSeconds;
        if (_remainingLife <= 0f)
            Destroy();
    }

    public override void CustomDestroy()
    {
        _rect.Destroy();
    }
}
