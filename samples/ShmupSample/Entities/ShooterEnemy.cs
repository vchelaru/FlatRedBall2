using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ShmupSample.Entities;

/// <summary>
/// Orange shooter enemy. Flies in, pauses at a hold position, fires at the player periodically.
/// Takes 2 hits to destroy.
/// </summary>
public class ShooterEnemy : Entity
{
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;
    private AxisAlignedRectangle _innerRect = null!;

    private float _shootTimer;
    private const float ShootInterval = 2.0f;

    private float _holdY;
    private bool _holding;

    // Health
    private int _health = 2;
    public bool IsAlive => _health > 0;

    // Flash on hit
    private float _hitFlashTimer;
    private const float HitFlashDuration = 0.12f;
    private Color _normalColor;

    /// <summary>Fired when this shooter exits the bottom of the screen without being destroyed.</summary>
    public event Action? Escaped;

    public override void CustomInitialize()
    {
        _normalColor = new Color(255, 140, 0, 230);

        CollisionRect = new AxisAlignedRectangle
        {
            Width = 24,
            Height = 20,
            Color = _normalColor,
            IsFilled = true,
            IsVisible = true,
        };
        Add(CollisionRect);

        // Inner accent square
        _innerRect = new AxisAlignedRectangle
        {
            Width = 10,
            Height = 10,
            Color = new Color(255, 220, 80, 255),
            IsFilled = true,
            IsVisible = true,
        };
        Add(_innerRect);

        _shootTimer = ShootInterval * 0.5f;  // first shot sooner
    }

    /// <summary>
    /// Sets where the shooter will stop and hold before shooting.
    /// </summary>
    public void Launch(float velY, float holdY)
    {
        VelocityY = velY;
        _holdY = holdY;
    }

    public override void CustomActivity(FrameTime time)
    {
        // Stop at hold position
        if (!_holding && Y <= _holdY)
        {
            Y = _holdY;
            VelocityY = 0f;
            _holding = true;
        }

        // Shoot periodically once holding
        if (_holding)
        {
            _shootTimer -= time.DeltaSeconds;
            if (_shootTimer <= 0f)
            {
                Fire();
                _shootTimer = ShootInterval;
            }
        }

        // Hit flash — single white burst, then back to normal
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            CollisionRect.Color = new Color(255, 255, 255, 255);
        }
        else
        {
            CollisionRect.Color = _normalColor;
        }

        // Fall off screen if not holding
        if (!_holding && Y < -(Engine.Camera.TargetHeight / 2f + 30f))
        {
            Escaped?.Invoke();
            Destroy();
        }
    }

    private void Fire()
    {
        var players = Engine.GetFactory<PlayerShip>().Instances;
        if (players.Count == 0) return;

        float dx = players[0].X - X;
        float dy = players[0].Y - Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len == 0f) return;

        const float Speed = 200f;
        var bullet = Engine.GetFactory<EnemyBullet>().Create();
        bullet.X = X;
        bullet.Y = Y - 10f;
        bullet.VelocityX = dx / len * Speed;
        bullet.VelocityY = dy / len * Speed;
    }

    public void TakeDamage(int amount)
    {
        _health -= amount;
        _hitFlashTimer = HitFlashDuration;
        if (_health <= 0)
            Destroy();
    }

    public override void CustomDestroy()
    {
        CollisionRect.Destroy();
        _innerRect.Destroy();
    }
}
