using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace KoalaPickleSample.Entities;

public class EnemyBullet : Entity
{
    private const float BulletSpeed = 280f;
    private const float Lifetime = 5f;

    private AxisAlignedRectangle _rect = null!;
    private float _remainingLife = Lifetime;

    public AxisAlignedRectangle Rectangle => _rect;

    /// <summary>
    /// Call immediately after Create() to fire toward a target world position.
    /// Enemy bullets pass through platforms — no platform collision is registered for them.
    /// </summary>
    public void Launch(float originX, float originY, float targetX, float targetY)
    {
        X = originX;
        Y = originY;

        float dx = targetX - originX;
        float dy = targetY - originY;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        if (length > 0f)
        {
            VelocityX = (dx / length) * BulletSpeed;
            VelocityY = (dy / length) * BulletSpeed;
        }
        else
        {
            // Fallback: shoot straight left if on top of the player
            VelocityX = -BulletSpeed;
        }
    }

    public override void CustomInitialize()
    {
        _rect = new AxisAlignedRectangle
        {
            Width = 14f,
            Height = 8f,
            IsVisible = true,
            Color = new XnaColor(80, 220, 80, 255),
        };
        Add(_rect);
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
