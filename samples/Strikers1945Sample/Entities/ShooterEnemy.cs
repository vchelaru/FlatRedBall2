using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strikers1945Sample.Screens;

namespace Strikers1945Sample.Entities;

public class ShooterEnemy : Entity
{
    private Sprite _sprite = null!;
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    private float _shootTimer;
    private const float ShootInterval = 1.5f; // faster than ShmupSample — Strikers pacing
    private const float TelegraphDuration = 0.3f;
    private float _telegraphTimer;

    private float _holdY;
    private bool _holding;
    private float _holdTimer;
    private const float HoldDuration = 4f; // hold position then exit

    private int _baseHealth = 3;
    private int _health = 3;
    public bool IsAlive => _health > 0;

    private float _hitFlashTimer;

    public event Action? Escaped;

    public override void CustomInitialize()
    {
        var texture = Engine.ContentManager.Load<Texture2D>("ship_0012");
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 2.2f,
            FlipVertical = true,
        };
        Add(_sprite);

        CollisionRect = new AxisAlignedRectangle
        {
            Width = 36,
            Height = 28,
            Visible = false,
        };
        Add(CollisionRect);

        _health = Math.Max(1, (int)(_baseHealth * GameplayScreen.EnemyHpMultiplier));
        _shootTimer = ShootInterval * 0.4f;
    }

    public void Launch(float velY, float holdY)
    {
        VelocityY = velY;
        _holdY = holdY;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            if (_hitFlashTimer <= 0f)
                _sprite.Color = Color.White;
        }

        if (!_holding && Y <= _holdY)
        {
            Y = _holdY;
            VelocityY = 0f;
            _holding = true;
        }

        if (_holding)
        {
            if (_telegraphTimer > 0f)
            {
                _telegraphTimer -= time.DeltaSeconds;
                if (_telegraphTimer <= 0f)
                {
                    Fire();
                    _sprite.Color = Color.White;
                    _shootTimer = ShootInterval;
                }
            }
            else
            {
                _shootTimer -= time.DeltaSeconds;
                if (_shootTimer <= 0f)
                {
                    // Start telegraph: flash yellow before firing
                    _telegraphTimer = TelegraphDuration;
                    _sprite.Color = Color.Yellow;
                }
            }

            _holdTimer += time.DeltaSeconds;
            if (_holdTimer >= HoldDuration)
            {
                VelocityY = -150f; // exit downward
                _holding = false;
            }
        }

        // Off-screen cleanup
        if (Y < -(Engine.Camera.TargetHeight / 2f + 50f))
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

        float speed = 220f * EnemyBullet.SpeedMultiplier;
        var bullet = Engine.GetFactory<EnemyBullet>().Create();
        bullet.X = X;
        bullet.Y = Y - 15f;
        bullet.VelocityX = dx / len * speed;
        bullet.VelocityY = dy / len * speed;
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
