using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Strikers1945Sample.Screens;

namespace Strikers1945Sample.Entities;

public class DiveBomberEnemy : Entity
{
    private Sprite _sprite = null!;
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    private const int BaseHealth = 4;
    private int _health = 4;
    public bool IsAlive => _health > 0;

    private float _hitFlashTimer;

    private float _diveTimer;
    private bool _hasFired;
    private float _startX;

    public override void CustomInitialize()
    {
        var texture = Engine.ContentManager.Load<Texture2D>("ship_0014");
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
            Height = 30,
            Visible = false,
        };
        Add(CollisionRect);

        _health = Math.Max(1, (int)(BaseHealth * GameplayScreen.EnemyHpMultiplier));
    }

    /// <summary>
    /// Configure the dive bomber's entry. It swoops in an arc.
    /// </summary>
    public void Launch(float startX, float startY)
    {
        X = startX;
        Y = startY;
        _startX = startX;
        VelocityY = -200f; // dive downward
        VelocityX = startX > 0 ? -80f : 80f; // curve toward center
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            if (_hitFlashTimer <= 0f)
                _sprite.Color = Color.White;
        }

        _diveTimer += time.DeltaSeconds;

        // At bottom of arc, fire a burst and curve back up
        if (_diveTimer > 0.8f && !_hasFired)
        {
            FireBurst();
            _hasFired = true;
            VelocityY = 100f; // pull up
            VelocityX = _startX > 0 ? 120f : -120f; // fly off to original side
        }

        if (_diveTimer > 2.5f)
            Destroy();
    }

    private void FireBurst()
    {
        var factory = Engine.GetFactory<EnemyBullet>();
        var players = Engine.GetFactory<PlayerShip>().Instances;
        if (players.Count == 0) return;

        float dx = players[0].X - X;
        float dy = players[0].Y - Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len == 0f) return;

        float speed = 250f * EnemyBullet.SpeedMultiplier;
        // 3-bullet burst aimed at player
        for (int i = -1; i <= 1; i++)
        {
            var bullet = factory.Create();
            bullet.X = X;
            bullet.Y = Y - 10f;
            float angle = MathF.Atan2(dy, dx) + i * 0.12f;
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
