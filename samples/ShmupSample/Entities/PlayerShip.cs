using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ShmupSample.Entities;

public class PlayerShip : Entity
{
    // Player ship is a bright cyan arrow-like shape (two overlapping rectangles)
    private AxisAlignedRectangle _body = null!;
    private AxisAlignedRectangle _nose = null!;
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    private KeyboardInput2D _movement = null!;

    private float _fireCooldown;
    private int _gunCount = 2;  // starts with dual guns

    // Acceleration-ramp movement
    private const float MaxSpeed = 320f;
    private const float AccelForce = 3200f;

    public int MaxHealth { get; } = 5;
    public int Health { get; private set; } = 5;

    // Multiplier tracking: report when damage is taken
    public event Action? DamageTaken;
    public bool IsAlive => Health > 0;

    // Flash state for hit feedback
    private float _hitFlashTimer;
    private const float HitFlashDuration = 0.12f;

    // Damage cooldown — prevents instant death when overlapping an enemy body
    private float _damageCooldown;
    private const float DamageCooldownDuration = 0.5f;

    public override void CustomInitialize()
    {
        // Main body
        _body = new AxisAlignedRectangle
        {
            Width = 28,
            Height = 36,
            Color = new Color(0, 220, 255, 230),
            IsFilled = true,
            Visible = true,
        };
        AddChild(_body);

        // Nose — narrower piece at the top to give arrow shape
        _nose = new AxisAlignedRectangle
        {
            Width = 12,
            Height = 18,
            // Y offset: place nose above center of body (body height/2 + nose height/2 offset = 18+9=27 but we overlap)
            Y = 16,
            Color = new Color(0, 255, 220, 255),
            IsFilled = true,
            Visible = true,
        };
        AddChild(_nose);

        // Collision rectangle matches body bounds
        CollisionRect = new AxisAlignedRectangle
        {
            Width = 28,
            Height = 36,
            Visible = false,
        };
        AddChild(CollisionRect);

        _movement = new KeyboardInput2D(
            Engine.InputManager.Keyboard,
            Keys.Left, Keys.Right, Keys.Up, Keys.Down);

        // Drag snaps the ship to a stop quickly when keys are released.
        // Terminal velocity = AccelForce / Drag = 3200 / 10 = 320 = MaxSpeed, so the clamp
        // only ever activates during diagonal input.
        Drag = 10f;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_damageCooldown > 0f) _damageCooldown -= time.DeltaSeconds;
        HandleMovement(time);
        HandleFiring(time);
        HandleHitFlash(time);
    }

    private void HandleMovement(FrameTime time)
    {
        // Apply acceleration in input direction; drag handles deceleration
        AccelerationX = _movement.X * AccelForce;
        AccelerationY = _movement.Y * AccelForce;

        // Clamp to max speed
        float speed = MathF.Sqrt(VelocityX * VelocityX + VelocityY * VelocityY);
        if (speed > MaxSpeed)
        {
            float scale = MaxSpeed / speed;
            VelocityX *= scale;
            VelocityY *= scale;
        }

        // Clamp position to screen bounds
        var halfW = Engine.Camera.TargetWidth / 2f;
        var halfH = Engine.Camera.TargetHeight / 2f;
        X = Math.Clamp(X, -halfW + 20, halfW - 20);
        Y = Math.Clamp(Y, -halfH + 20, halfH - 20);
    }

    private void HandleFiring(FrameTime time)
    {
        _fireCooldown -= time.DeltaSeconds;

        var kb = Engine.InputManager.Keyboard;
        bool fireHeld = kb.IsKeyDown(Keys.Z) || kb.IsKeyDown(Keys.Space);

        if (fireHeld && _fireCooldown <= 0f)
        {
            SpawnBullets();
            _fireCooldown = 0.12f;
        }
    }

    private void SpawnBullets()
    {
        var factory = Engine.GetFactory<PlayerBullet>();

        // Gun positions spread across ship width
        float[] gunOffsets = _gunCount switch
        {
            2 => new[] { -10f, 10f },
            3 => new[] { -14f, 0f, 14f },
            4 => new[] { -18f, -6f, 6f, 18f },
            _ => new[] { -22f, -11f, 0f, 11f, 22f },
        };

        foreach (float offset in gunOffsets)
        {
            var bullet = factory.Create();
            bullet.X = X + offset;
            bullet.Y = Y + 22f;  // fire from top of ship
            bullet.VelocityY = 700f;
        }
    }

    private void HandleHitFlash(FrameTime time)
    {
        if (_hitFlashTimer > 0f)
        {
            _hitFlashTimer -= time.DeltaSeconds;
            bool flashOn = ((int)(_hitFlashTimer / 0.03f) & 1) == 0;
            var flashColor = new Color(255, 255, 255, 255);
            var normalColor = new Color(0, 220, 255, 230);
            _body.Color = flashOn ? flashColor : normalColor;
            _nose.Color = flashOn ? flashColor : new Color(0, 255, 220, 255);
        }
        else
        {
            _body.Color = new Color(0, 220, 255, 230);
            _nose.Color = new Color(0, 255, 220, 255);
        }
    }

    public void TakeDamage(int amount)
    {
        if (!IsAlive || _damageCooldown > 0f) return;
        Health -= amount;
        _damageCooldown = DamageCooldownDuration;
        _hitFlashTimer = HitFlashDuration;
        DamageTaken?.Invoke();
    }

    /// <summary>
    /// Increases gun count (capped at 5) for firepower upgrade.
    /// </summary>
    public void UpgradeGuns()
    {
        if (_gunCount < 5) _gunCount++;
    }

    public override void CustomDestroy()
    {
        _body.Destroy();
        _nose.Destroy();
        CollisionRect.Destroy();
    }
}
