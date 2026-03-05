using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace KoalaPickleSample.Entities;

/// <summary>
/// The green rectangle enemy. Runs a looping Pace -> Hop -> Shoot cycle.
/// It does not interact with gravity or platforms — it is placed at a fixed position
/// and stays there.
/// </summary>
public class Enemy : Entity
{
    // --- Visual ---
    private static readonly XnaColor NormalColor = new(60, 200, 60, 255);
    private static readonly XnaColor HitColor    = new(200, 255, 200, 255);

    private AxisAlignedRectangle _rect = null!;

    // --- Stats (configurable per level after Create) ---
    public int MaxHealth { get; private set; } = 6;
    public int Health { get; private set; }
    public bool IsDead => Health <= 0;
    public bool DiedThisFrame { get; private set; }

    // --- Pace parameters ---
    private float _paceRange  = 60f;   // how far left/right from spawn it walks
    private float _paceSpeed  = 80f;
    private float _paceOriginX;

    // --- Cycle timing ---
    private float _paceTime   = 2.2f;  // seconds spent pacing before hopping
    private float _hopTime    = 0.6f;  // seconds spent hopping before shooting
    private float _shootDelay = 0.22f; // seconds between individual shots in a burst
    private int   _burstCount = 3;     // shots per burst

    // --- Runtime state ---
    private enum Phase { Pace, Hop, Shoot }
    private Phase _phase = Phase.Pace;
    private float _phaseTimer;
    private float _hitFlashTimer;
    private float _shootBurstTimer;
    private int   _shotsRemaining;
    private float _hopVelocity = 260f;
    private bool  _hopApplied;
    private float _baseY;  // Y to snap back to after a hop

    // Reference to player set by screen so the enemy can aim
    private Player? _player;

    public AxisAlignedRectangle Rectangle => _rect;

    public override void CustomInitialize()
    {
        _rect = new AxisAlignedRectangle
        {
            Width  = 32f,
            Height = 56f,
            Visible = true,
            Color  = NormalColor,
        };
        Add(_rect);

        Health      = MaxHealth;
        _phaseTimer = _paceTime;
    }

    /// <summary>Call after Create() to configure difficulty for a specific level.</summary>
    public void Configure(int health, float paceRange, float paceSpeed,
                          float paceTime, float hopTime, float shootDelay, int burstCount)
    {
        MaxHealth   = health;
        Health      = health;
        _paceRange  = paceRange;
        _paceSpeed  = paceSpeed;
        _paceTime   = paceTime;
        _hopTime    = hopTime;
        _shootDelay = shootDelay;
        _burstCount = burstCount;
        _phaseTimer = _paceTime;
    }

    /// <summary>Must be called by the screen after spawning so the enemy can aim at the player.</summary>
    public void SetPlayer(Player player)
    {
        _player = player;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (IsDead) return;
        DiedThisFrame = false;

        _phaseTimer      -= time.DeltaSeconds;
        _hitFlashTimer   -= time.DeltaSeconds;
        _rect.Color       = _hitFlashTimer > 0f ? HitColor : NormalColor;

        switch (_phase)
        {
            case Phase.Pace:
                UpdatePace(time);
                if (_phaseTimer <= 0f)
                    EnterHop();
                break;

            case Phase.Hop:
                UpdateHop(time);
                if (_phaseTimer <= 0f)
                    EnterShoot();
                break;

            case Phase.Shoot:
                UpdateShoot(time);
                break;
        }
    }

    // ------------------------------------------------------------------ Pace

    private void UpdatePace(FrameTime time)
    {
        if (_paceOriginX == 0f)
            _paceOriginX = X;

        // VelocityX is applied by the engine's PhysicsUpdate — do not manually integrate here.
        if (VelocityX == 0f)
            VelocityX = _paceSpeed;

        float distFromOrigin = X - _paceOriginX;
        if (distFromOrigin >= _paceRange && VelocityX > 0f)
            VelocityX = -_paceSpeed;
        else if (distFromOrigin <= -_paceRange && VelocityX < 0f)
            VelocityX = _paceSpeed;
    }

    // ------------------------------------------------------------------ Hop

    private void EnterHop()
    {
        _baseY      = Y;  // remember platform Y so we can snap back after the hop
        VelocityX   = 0f;
        _phase      = Phase.Hop;
        _phaseTimer = _hopTime;
        _hopApplied = false;
    }

    private void UpdateHop(FrameTime time)
    {
        // Apply one upward impulse at the start of the hop phase; gravity will bring it back down.
        if (!_hopApplied)
        {
            AccelerationY = -900f;
            VelocityY     = _hopVelocity;
            _hopApplied   = true;
        }
    }

    // ------------------------------------------------------------------ Shoot

    private void EnterShoot()
    {
        AccelerationY = 0f;
        VelocityY     = 0f;
        Y             = _baseY;  // snap back to platform after hop
        _phase          = Phase.Shoot;
        _shotsRemaining = _burstCount;
        _shootBurstTimer = 0f;  // fire first shot immediately
    }

    private void UpdateShoot(FrameTime time)
    {
        _shootBurstTimer -= time.DeltaSeconds;

        if (_shotsRemaining > 0 && _shootBurstTimer <= 0f)
        {
            FireBullet();
            _shotsRemaining--;
            _shootBurstTimer = _shootDelay;
        }

        if (_shotsRemaining <= 0 && _shootBurstTimer <= 0f)
            EnterPace();
    }

    private void FireBullet()
    {
        if (_player == null) return;

        var bullet = Engine.GetFactory<EnemyBullet>().Create();
        bullet.Launch(X, Y, _player.X, _player.Y);
    }

    private void EnterPace()
    {
        _phase       = Phase.Pace;
        _phaseTimer  = _paceTime;
        _paceOriginX = X;  // reset origin to current position after hops may have drifted it
        VelocityX    = _paceSpeed;
    }

    // ------------------------------------------------------------------ Damage

    /// <summary>Called by the screen when a player bullet hits this enemy.</summary>
    public void TakeHit()
    {
        if (IsDead) return;

        Health--;
        _hitFlashTimer = 0.12f;

        if (Health <= 0)
        {
            DiedThisFrame = true;
            VelocityX     = 0f;
            VelocityY     = 0f;
            AccelerationY = 0f;
        }
    }

    public override void CustomDestroy()
    {
        _rect.Destroy();
    }
}
