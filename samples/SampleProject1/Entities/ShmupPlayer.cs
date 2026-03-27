using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SampleProject1.Entities;

public class ShmupPlayer : Entity
{
    private KeyboardInput2D _movement = null!;
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public int MaxHp { get; set; } = 3;
    public int Hp { get; private set; }
    public bool IsDead => Hp <= 0;

    private float _invincibilityTimer;
    private const float InvincibilityDuration = 1.5f;
    public bool IsInvincible => _invincibilityTimer > 0f;

    private float _fireCooldown;
    private const float FireInterval = 0.2f;

    private const float MoveSpeed = 250f;
    private const float SpreadAngle = 0.15f; // ~8.6 degrees — narrow fan

    public override void CustomInitialize()
    {
        Hp = MaxHp;

        Rectangle = new AxisAlignedRectangle
        {
            Width = 24,
            Height = 24,
            Color = new Color(80, 180, 255),
            IsVisible = true,
            IsFilled = true,
        };
        Add(Rectangle);

        _movement = new KeyboardInput2D(
            Engine.Input.Keyboard,
            Keys.Left, Keys.Right, Keys.Up, Keys.Down);
    }

    public override void CustomActivity(FrameTime time)
    {
        VelocityX = _movement.X * MoveSpeed;
        VelocityY = _movement.Y * MoveSpeed;

        // Invincibility timer
        if (_invincibilityTimer > 0f)
        {
            _invincibilityTimer -= time.DeltaSeconds;
            // Blink effect: toggle visibility every few frames
            Rectangle.IsVisible = ((int)(_invincibilityTimer * 10f) % 2) == 0;
        }
        else
        {
            Rectangle.IsVisible = true;
        }

        // Shooting
        _fireCooldown -= time.DeltaSeconds;
        if (_fireCooldown <= 0f && Engine.Input.Keyboard.IsKeyDown(Keys.Space))
        {
            FireSpread();
            _fireCooldown = FireInterval;
        }
    }

    public void TakeDamage()
    {
        if (IsInvincible || IsDead) return;
        Hp--;
        _invincibilityTimer = InvincibilityDuration;
    }

    private void FireSpread()
    {
        var factory = Engine.GetFactory<ShmupBullet>();

        // Center bullet — straight up
        SpawnBullet(factory, 0f);
        // Left bullet — angled left
        SpawnBullet(factory, -SpreadAngle);
        // Right bullet — angled right
        SpawnBullet(factory, SpreadAngle);
    }

    private void SpawnBullet(Factory<ShmupBullet> factory, float angleOffset)
    {
        const float BulletSpeed = 500f;
        var bullet = factory.Create();
        bullet.X = X;
        bullet.Y = Y + Rectangle.Height / 2f;

        // Base direction is straight up (0, 1), rotated by angleOffset
        float sin = MathF.Sin(angleOffset);
        float cos = MathF.Cos(angleOffset);
        bullet.VelocityX = sin * BulletSpeed;
        bullet.VelocityY = cos * BulletSpeed;
    }
}
