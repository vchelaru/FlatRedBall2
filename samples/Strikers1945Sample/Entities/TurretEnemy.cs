using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strikers1945Sample.Screens;

namespace Strikers1945Sample.Entities;

/// <summary>
/// Stationary enemy that rotates to track the player and fires aimed shots.
/// </summary>
public class TurretEnemy : Entity
{
    private Sprite _sprite = null!;
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    private float _shootTimer;
    private const float ShootInterval = 1.5f;
    private const float TelegraphDuration = 0.4f;
    private float _telegraphTimer;

    private const int BaseHealth = 5;
    private int _health = 5;
    public bool IsAlive => _health > 0;

    private float _hitFlashTimer;

    public override void CustomInitialize()
    {
        var texture = Engine.ContentManager.Load<Texture2D>("ship_0013");
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

        _health = Math.Max(1, (int)(BaseHealth * GameplayScreen.EnemyHpMultiplier));
        _shootTimer = ShootInterval * 0.5f;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            if (_hitFlashTimer <= 0f && _telegraphTimer <= 0f)
                _sprite.Color = Color.White;
        }

        // Rotate sprite to face the player
        var players = Engine.GetFactory<PlayerShip>().Instances;
        if (players.Count > 0)
        {
            float dx = players[0].X - X;
            float dy = players[0].Y - Y;
            // Angle convention: 0 = up, positive = clockwise. Atan2(dx, dy) gives clockwise from Y+.
            // Sprite is flipped vertical (faces down), so add PI to compensate.
            _sprite.Rotation = Angle.FromRadians(MathF.Atan2(dx, dy) + MathF.PI);
        }

        // Telegraph and shoot
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
                _telegraphTimer = TelegraphDuration;
                _sprite.Color = Color.Yellow;
            }
        }

        // Off-screen cleanup (scrolled off bottom)
        if (Y < -(Engine.Camera.TargetHeight / 2f + 50f))
            Destroy();
    }

    private void Fire()
    {
        var players = Engine.GetFactory<PlayerShip>().Instances;
        if (players.Count == 0) return;

        float dx = players[0].X - X;
        float dy = players[0].Y - Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len == 0f) return;

        float speed = 200f * EnemyBullet.SpeedMultiplier;
        var bullet = Engine.GetFactory<EnemyBullet>().Create();
        bullet.X = X;
        bullet.Y = Y;
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
