using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Strikers1945Sample.Entities;

public class Boss : Entity
{
    private Sprite _sprite = null!;
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    private int _maxHealth;
    private int _health;
    public int Health => _health;
    public int MaxHealth => _maxHealth;
    public bool IsAlive => _health > 0;
    public float HealthPercent => (float)_health / _maxHealth;

    // Phase tracking
    private bool _isPhase2;
    public bool IsPhase2 => _isPhase2;
    private float _transformTimer;
    private bool _transforming;

    // Rage mode (below 25% HP)
    private bool _rageMode;

    private float _hitFlashTimer;

    // Attack timers
    private float _attackTimer;
    private float _spiralTimer;
    private float _sweepTimer;
    private float _sweepAngle;
    private bool _sweepActive;
    private float _moveTimer;
    private float _moveTargetX;

    // Swoop movement
    private float _swoopTimer;
    private float _swoopTargetY;
    private bool _swooping;
    private float _holdY;

    // Events
    public event Action? Destroyed;
    public event Action? PhaseChanged;

    private string _phase2SpriteName = "";

    public override void CustomInitialize()
    {
        CollisionRect = new AxisAlignedRectangle
        {
            Width = 80,
            Height = 60,
            Visible = false,
        };
        Add(CollisionRect);
    }

    public void Configure(string spriteName, string phase2SpriteName, int health)
    {
        var texture = Engine.ContentManager.Load<Texture2D>(spriteName);
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 4f, // bosses are big
            FlipVertical = true,
        };
        Add(_sprite);

        _phase2SpriteName = phase2SpriteName;
        _maxHealth = health;
        _health = health;

        // Enter from top
        Y = Engine.Camera.TargetHeight / 2f + 80f;
        VelocityY = -80f;
        _attackTimer = 2f; // grace period before first attack
        _spiralTimer = 3f;
        _swoopTimer = 5f;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_transforming)
        {
            HandleTransformation(time);
            return;
        }

        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            if (_hitFlashTimer <= 0f)
                _sprite.Color = _rageMode ? new Color(255, 100, 100) : Color.White;
        }

        // Stop at hold position
        if (Y > Engine.Camera.TargetHeight / 2f - 120f)
            return; // still entering
        if (VelocityY < 0f && !_swooping)
        {
            VelocityY = 0f;
            _holdY = Engine.Camera.TargetHeight / 2f - 120f;
            Y = _holdY;
        }

        float rageMultiplier = _rageMode ? 0.5f : 1f;

        // Movement (lateral + swoop)
        HandleMovement(time);
        HandleSwoop(time);

        // Main attack pattern
        _attackTimer -= time.DeltaSeconds;
        if (_attackTimer <= 0f)
        {
            if (_isPhase2)
                Phase2Attack();
            else
                Phase1Attack();

            float cooldown = _isPhase2 ? 0.72f : 1.8f; // phase 2 fires faster (was 1.2, reduced 40%)
            _attackTimer = cooldown * rageMultiplier;
        }

        // Spiral ring (phase 1 and onward)
        _spiralTimer -= time.DeltaSeconds;
        if (_spiralTimer <= 0f)
        {
            FireSpiralRing();
            _spiralTimer = 3f * rageMultiplier;
        }

        // Sweep pattern (phase 2 only)
        if (_isPhase2)
        {
            HandleSweep(time, rageMultiplier);
        }
    }

    private void HandleMovement(FrameTime time)
    {
        _moveTimer -= time.DeltaSeconds;
        if (_moveTimer <= 0f)
        {
            var halfW = Engine.Camera.TargetWidth / 2f - 60f;
            _moveTargetX = Engine.Random.Between(-halfW, halfW);
            _moveTimer = Engine.Random.Between(1.5f, 3f);
        }

        float speedCap = _isPhase2 ? 180f : 100f;
        if (_rageMode) speedCap *= 2f;

        float dx = _moveTargetX - X;
        VelocityX = Math.Clamp(dx * 3f, -speedCap, speedCap);
    }

    private void HandleSwoop(FrameTime time)
    {
        if (_swooping)
        {
            float dy = _swoopTargetY - Y;
            if (MathF.Abs(dy) < 5f)
            {
                // Reached target, return to hold position
                _swoopTargetY = _holdY;
                if (MathF.Abs(Y - _holdY) < 10f)
                {
                    _swooping = false;
                    Y = _holdY;
                    VelocityY = 0f;
                }
                else
                {
                    float returnSpeed = _rageMode ? 160f : 80f;
                    VelocityY = MathF.Sign(dy) * returnSpeed;
                }
            }
            return;
        }

        _swoopTimer -= time.DeltaSeconds;
        if (_swoopTimer <= 0f)
        {
            _swooping = true;
            // Swoop toward center of screen (closer to player)
            _swoopTargetY = 0f;
            float swoopSpeed = _rageMode ? 200f : 120f;
            VelocityY = -swoopSpeed;
            _swoopTimer = Engine.Random.Between(4f, 7f);
        }
    }

    private void HandleSweep(FrameTime time, float rageMultiplier)
    {
        if (_sweepActive)
        {
            _sweepTimer -= time.DeltaSeconds;
            if (_sweepTimer <= 0f)
            {
                var factory = Engine.GetFactory<EnemyBullet>();
                var bullet = factory.Create();
                bullet.X = X;
                bullet.Y = Y - 30f;
                float speed = 250f * EnemyBullet.SpeedMultiplier;
                bullet.VelocityX = MathF.Cos(_sweepAngle) * speed;
                bullet.VelocityY = MathF.Sin(_sweepAngle) * speed;

                _sweepAngle += 0.15f;
                _sweepTimer = 0.06f * rageMultiplier;

                // End sweep after crossing from left to right
                if (_sweepAngle > MathF.PI * 1.8f)
                    _sweepActive = false;
            }
        }
        else
        {
            _sweepTimer -= time.DeltaSeconds;
            if (_sweepTimer <= 0f)
            {
                _sweepActive = true;
                _sweepAngle = MathF.PI * 1.15f; // start slightly left of straight down
                _sweepTimer = 0f;
            }
        }
    }

    private void Phase1Attack()
    {
        var factory = Engine.GetFactory<EnemyBullet>();
        var players = Engine.GetFactory<PlayerShip>().Instances;
        if (players.Count == 0) return;

        float dx = players[0].X - X;
        float dy = players[0].Y - Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len == 0f) return;

        float speed = 200f * EnemyBullet.SpeedMultiplier;
        // Triple aimed shot
        for (int i = -1; i <= 1; i++)
        {
            var bullet = factory.Create();
            bullet.X = X + i * 25f;
            bullet.Y = Y - 30f;
            float angle = MathF.Atan2(dy, dx) + i * 0.08f;
            bullet.VelocityX = MathF.Cos(angle) * speed;
            bullet.VelocityY = MathF.Sin(angle) * speed;
        }
    }

    private void FireSpiralRing()
    {
        var factory = Engine.GetFactory<EnemyBullet>();
        float speed = 150f * EnemyBullet.SpeedMultiplier;
        const int Count = 12;

        for (int i = 0; i < Count; i++)
        {
            float angle = MathF.PI * 2f / Count * i;
            var bullet = factory.Create();
            bullet.X = X;
            bullet.Y = Y;
            bullet.VelocityX = MathF.Cos(angle) * speed;
            bullet.VelocityY = MathF.Sin(angle) * speed;
        }
    }

    private void Phase2Attack()
    {
        var factory = Engine.GetFactory<EnemyBullet>();
        float fanSpeed = 190f * EnemyBullet.SpeedMultiplier;

        // Wide bullet fan of 7
        int bulletCount = 7;
        for (int i = 0; i < bulletCount; i++)
        {
            var bullet = factory.Create();
            bullet.X = X;
            bullet.Y = Y - 30f;
            float angle = MathF.PI * 1.5f + (i - bulletCount / 2f) * 0.18f;
            bullet.VelocityX = MathF.Cos(angle) * fanSpeed;
            bullet.VelocityY = MathF.Sin(angle) * fanSpeed;
        }

        // Aimed burst at player (5 bullets at staggered speeds)
        var players = Engine.GetFactory<PlayerShip>().Instances;
        if (players.Count > 0)
        {
            float dx = players[0].X - X;
            float dy = players[0].Y - Y;
            float len = MathF.Sqrt(dx * dx + dy * dy);
            if (len > 0f)
            {
                for (int i = 0; i < 5; i++)
                {
                    var bullet = factory.Create();
                    bullet.X = X;
                    bullet.Y = Y - 30f;
                    float speed = (220f + i * 25f) * EnemyBullet.SpeedMultiplier;
                    bullet.VelocityX = dx / len * speed;
                    bullet.VelocityY = dy / len * speed;
                }
            }
        }
    }

    public void TakeDamage(int amount)
    {
        if (_transforming) return;
        _health -= amount;

        // Check for phase transition at 50%
        if (!_isPhase2 && _health <= _maxHealth / 2)
        {
            StartTransformation();
            return;
        }

        // Enter rage mode at 25% HP
        if (!_rageMode && _health <= _maxHealth / 4 && _health > 0)
            _rageMode = true;

        Flash();

        if (_health <= 0)
        {
            Destroyed?.Invoke();
            Destroy();
        }
    }

    public void Flash()
    {
        _hitFlashTimer = 0.1f;
        _sprite.Color = new Color(255, 255, 255, 255);
    }

    private void StartTransformation()
    {
        _transforming = true;
        _transformTimer = 1.5f;
        VelocityX = 0f;
        VelocityY = 0f;
    }

    private void HandleTransformation(FrameTime time)
    {
        _transformTimer -= time.DeltaSeconds;

        // Flash rapidly during transformation
        bool flash = ((int)(_transformTimer / 0.05f) & 1) == 0;
        _sprite.Alpha = flash ? 1f : 0.2f;

        if (_transformTimer <= 0.5f && !_isPhase2)
        {
            // Swap to phase 2 sprite
            _isPhase2 = true;
            if (!string.IsNullOrEmpty(_phase2SpriteName))
            {
                var tex = Engine.ContentManager.Load<Texture2D>(_phase2SpriteName);
                _sprite.Texture = tex;
                _sprite.TextureScale = 5f; // phase 2 is bigger
            }
            CollisionRect.Width = 100;
            CollisionRect.Height = 80;
            PhaseChanged?.Invoke();
        }

        if (_transformTimer <= 0f)
        {
            _transforming = false;
            _sprite.Alpha = 1f;
            _attackTimer = 0.5f;
            _sweepTimer = 2f; // start sweep pattern shortly after phase 2
        }
    }

    public override void CustomDestroy()
    {
        _sprite?.Destroy();
        CollisionRect.Destroy();
    }
}
