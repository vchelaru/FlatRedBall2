using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ShmupSample.Entities;

/// <summary>
/// Heavy enemy. Large rectangle, takes 5 hits, moves slowly in a straight line.
/// </summary>
public class HeavyEnemy : Entity
{
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;
    private AxisAlignedRectangle _outline = null!;

    private int _health = 5;
    private const int MaxHealth = 5;
    public bool IsAlive => _health > 0;

    private float _hitFlashTimer;
    private const float HitFlashDuration = 0.15f;
    private Color _normalColor;

    private float _shootTimer = 2.5f;
    private const float ShootInterval = 3.0f;

    public bool EscapedScreen { get; private set; }

    public override void CustomInitialize()
    {
        _normalColor = new Color(200, 60, 220, 230);

        CollisionRect = new AxisAlignedRectangle
        {
            Width = 52,
            Height = 36,
            Color = _normalColor,
            IsFilled = true,
            Visible = true,
        };
        Add(CollisionRect);

        _outline = new AxisAlignedRectangle
        {
            Width = 52,
            Height = 36,
            Color = new Color(255, 100, 255, 255),
            IsFilled = false,
            OutlineThickness = 2f,
            Visible = true,
        };
        Add(_outline);
    }

    public override void CustomActivity(FrameTime time)
    {
        _shootTimer -= time.DeltaSeconds;
        if (_shootTimer <= 0f)
        {
            FireSpread();
            _shootTimer = ShootInterval;
        }

        // Hit flash — single white burst, then back to health-tinted normal
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            CollisionRect.Color = new Color(255, 255, 255, 255);
        }
        else
        {
            // Tint based on remaining health
            float healthFrac = (float)_health / MaxHealth;
            byte r = (byte)(200 + (int)((1f - healthFrac) * 55));
            CollisionRect.Color = new Color(r, (byte)60, (byte)220, (byte)230);
        }

        // Off screen
        if (Y < -(Engine.Camera.TargetHeight / 2f + 30f))
        {
            EscapedScreen = true;
            Destroy();
        }
    }

    private void FireSpread()
    {
        var players = Engine.GetFactory<PlayerShip>().Instances;
        if (players.Count == 0) return;

        float dx = players[0].X - X;
        float dy = players[0].Y - Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len == 0f) return;

        float nx = dx / len;
        float ny = dy / len;

        // Three bullets: center aimed at player, ±25° on either side
        const float Speed = 180f;
        const float SpreadRad = 0.436f; // 25 degrees
        foreach (float angle in new[] { -SpreadRad, 0f, SpreadRad })
        {
            float cos = MathF.Cos(angle);
            float sin = MathF.Sin(angle);
            var bullet = Engine.GetFactory<EnemyBullet>().Create();
            bullet.X = X;
            bullet.Y = Y - 18f;
            bullet.VelocityX = (nx * cos - ny * sin) * Speed;
            bullet.VelocityY = (nx * sin + ny * cos) * Speed;
        }
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
        _outline.Destroy();
    }
}
