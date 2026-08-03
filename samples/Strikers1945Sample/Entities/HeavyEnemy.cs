using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strikers1945Sample.Screens;

namespace Strikers1945Sample.Entities;

public class HeavyEnemy : Entity
{
    private Sprite _sprite = null!;
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    private float _shootTimer;
    private const float ShootInterval = 1.8f;
    private const float TelegraphDuration = 0.5f;
    private float _telegraphTimer;

    private const int BaseHealth = 10;
    private int _health = 10;
    public bool IsAlive => _health > 0;

    private float _hitFlashTimer;

    public override void CustomInitialize()
    {
        var texture = Engine.ContentManager.Load<Texture2D>("ship_0016");
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 2.5f,
            FlipVertical = true,
        };
        Add(_sprite);

        CollisionRect = new AxisAlignedRectangle
        {
            Width = 50,
            Height = 40,
            Visible = false,
        };
        Add(CollisionRect);

        _health = Math.Max(1, (int)(BaseHealth * GameplayScreen.EnemyHpMultiplier));
        _shootTimer = 1.0f;
        VelocityY = -50f; // slow descent
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            if (_hitFlashTimer <= 0f)
                _sprite.Color = Color.White;
        }

        if (_telegraphTimer > 0f)
        {
            _telegraphTimer -= time.DeltaSeconds;
            if (_telegraphTimer <= 0f)
            {
                FireSpread();
                _sprite.Color = Color.White;
                _shootTimer = ShootInterval;
            }
        }
        else
        {
            _shootTimer -= time.DeltaSeconds;
            if (_shootTimer <= 0f)
            {
                _telegraphTimer = TelegraphDuration;
                _sprite.Color = Color.Yellow;
            }
        }

        if (Y < -(Engine.Camera.TargetHeight / 2f + 60f))
            Destroy();
    }

    private void FireSpread()
    {
        var factory = Engine.GetFactory<EnemyBullet>();
        float speed = 180f * EnemyBullet.SpeedMultiplier;

        // 5-bullet fan spread
        for (int i = -2; i <= 2; i++)
        {
            var bullet = factory.Create();
            bullet.X = X;
            bullet.Y = Y - 20f;
            float angle = MathF.PI * 1.5f + i * 0.18f; // downward with spread
            bullet.VelocityX = MathF.Cos(angle) * speed;
            bullet.VelocityY = MathF.Sin(angle) * speed;
        }
    }

    public void TakeDamage(int amount)
    {
        _health -= amount;
        Flash();
        if (_health <= 0)
            Destroy();
    }

    public void Flash()
    {
        _hitFlashTimer = 0.1f;
        _sprite.Color = new Color(255, 255, 255, 255);
    }

    public override void CustomDestroy()
    {
        _sprite.Destroy();
        CollisionRect.Destroy();
    }
}
