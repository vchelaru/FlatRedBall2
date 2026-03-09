using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace KoalaPickleSample.Entities;

public class Player : Entity
{
    private const float HitFlashSeconds = 0.15f;
    private const int MaxAirJumps = 1;  // 1 = double jump (one extra jump while airborne)

    private static readonly XnaColor NormalColor = new(255, 120, 180, 255);
    private static readonly XnaColor HitColor    = new(255, 255, 255, 255);

    private readonly PlatformerBehavior _platformer = new();

    private AxisAlignedRectangle _rect = null!;

    // Input — set from either keyboard or gamepad in CustomInitialize
    private float _hitFlashTimer;
    private int   _airJumpsRemaining;
    private float _shootCooldown;

    private const float ShootInterval = 1f / 6f; // 3 shots per second

    public AxisAlignedRectangle Rectangle => _rect;

    public int Health { get; private set; } = 3;
    public bool IsDead => Health <= 0;

    /// <summary>
    /// True if the player was killed this frame (Health just reached 0).
    /// Consumed by the screen each frame.
    /// </summary>
    public bool DiedThisFrame { get; private set; }

    public override void CustomInitialize()
    {
        _rect = new AxisAlignedRectangle
        {
            Width = 40f,
            Height = 40f,
            IsVisible = true,
            Color = NormalColor,
        };
        Add(_rect);

        var groundValues = new PlatformerValues
        {
            MaxSpeedX          = 220f,
            AccelerationTimeX  = TimeSpan.FromSeconds(0.07),
            DecelerationTimeX  = TimeSpan.FromSeconds(0.05),
            Gravity            = 900f,
            MaxFallSpeed       = 700f,
            JumpVelocity       = 480f,
            JumpApplyLength    = TimeSpan.FromSeconds(0.18),
            JumpApplyByButtonHold = true,
            UsesAcceleration   = true,
        };

        var airValues = new PlatformerValues
        {
            MaxSpeedX          = 220f,
            AccelerationTimeX  = TimeSpan.FromSeconds(0.14),
            DecelerationTimeX  = TimeSpan.FromSeconds(0.25),
            Gravity            = 900f,
            MaxFallSpeed       = 700f,
            JumpVelocity       = 480f,
            JumpApplyLength    = TimeSpan.FromSeconds(0.18),
            JumpApplyByButtonHold = true,
            UsesAcceleration   = true,
        };

        _platformer.GroundMovement = groundValues;
        _platformer.AirMovement    = airValues;

        // Support both gamepad and keyboard so the game is playable either way.
        // GamepadPressableInput.WasJustPressed is stubbed (always false), so we use
        // IGamepad.WasButtonJustPressed directly for jump in CustomActivity.
        var gamepad  = Engine.Input.GetGamepad(0);
        var keyboard = Engine.Input.Keyboard;

        // Movement: merge keyboard and gamepad — whichever has the larger magnitude wins.
        // This lets DirectionFacing be driven entirely inside PlatformerBehavior.
        var keyboardInput2D = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down);
        var gamepadInput2D  = new GamepadInput2D(gamepad, GamepadAxis.LeftStickX, GamepadAxis.LeftStickY, deadzone: 0.15f);
        _platformer.MovementInput = keyboardInput2D.Or(gamepadInput2D);

        // Jump: keyboard Space — gamepad A is handled manually in CustomActivity
        _platformer.JumpInput = new KeyboardPressableInput(keyboard, Keys.Space);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (IsDead) return;
        DiedThisFrame = false;

        // Gamepad A jump is handled manually because GamepadPressableInput.WasJustPressed is stubbed.
        var gamepad = Engine.Input.GetGamepad(0);
        bool gamepadJumpPressed = gamepad.WasButtonJustPressed(Buttons.A);

        // Movement (keyboard + gamepad merged via MovementInput) and DirectionFacing are
        // handled entirely inside PlatformerBehavior.
        _platformer.Update(this, time);

        if (gamepadJumpPressed && _platformer.IsOnGround)
        {
            float jumpVel = _platformer.GroundMovement?.JumpVelocity ?? 480f;
            VelocityY = jumpVel;
        }

        // --- Double jump ---
        // Reset air jumps when the platformer confirms we're on the ground.
        if (_platformer.IsOnGround)
            _airJumpsRemaining = MaxAirJumps;

        // Allow an extra jump while airborne (gamepad or keyboard), consuming one air jump.
        bool jumpJustPressed = Engine.Input.Keyboard.WasKeyPressed(Keys.Space)
                             || gamepadJumpPressed;
        if (jumpJustPressed && !_platformer.IsOnGround && !_platformer.IsApplyingJump && _airJumpsRemaining > 0)
        {
            float jumpVel = _platformer.AirMovement.JumpVelocity;
            VelocityY = jumpVel;
            _airJumpsRemaining--;
        }

        // --- Auto-fire (hold to shoot at ShootInterval) ---
        _shootCooldown -= time.DeltaSeconds;

        bool fireHeld = Engine.Input.Keyboard.IsKeyDown(Keys.Z)
                     || gamepad.IsButtonDown(Buttons.RightTrigger)
                     || gamepad.IsButtonDown(Buttons.X);

        if (fireHeld && _shootCooldown <= 0f)
        {
            float dir = _platformer.DirectionFacing == FlatRedBall2.Movement.HorizontalDirection.Right ? 1f : -1f;
            var bullet = Engine.GetFactory<PlayerBullet>().Create();
            bullet.Launch(dir, X, Y);
            _shootCooldown = ShootInterval;
        }

        // --- Hit flash ---
        _hitFlashTimer -= time.DeltaSeconds;
        _rect.Color = _hitFlashTimer > 0f ? HitColor : NormalColor;
    }

    /// <summary>Called by the screen when an enemy bullet hits the player.</summary>
    public void TakeHit()
    {
        if (IsDead) return;

        Health--;
        _hitFlashTimer = HitFlashSeconds;

        if (Health <= 0)
            DiedThisFrame = true;
    }

    public override void CustomDestroy()
    {
        _rect.Destroy();
    }
}
