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
    private int _airJumpsRemaining;

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
            Visible = true,
            Color = NormalColor,
        };
        AddChild(_rect);

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
        // Movement uses GamepadInput2D which reads axes correctly.
        var gamepad = Engine.InputManager.GetGamepad(0);
        var keyboard = Engine.InputManager.Keyboard;

        // Movement: left stick X (gamepad) | left/right keys (keyboard fallback via keyboard 2D)
        // We use keyboard as the movement input so keyboard works too; gamepad is read directly
        // in CustomActivity for the axis override.
        _platformer.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down);

        // Jump: keyboard Space — gamepad A is handled manually in CustomActivity
        _platformer.JumpInput = new KeyboardPressableInput(keyboard, Keys.Space);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (IsDead) return;
        DiedThisFrame = false;

        // --- Gamepad movement override ---
        // Read left stick directly and override the keyboard-driven MovementInput result
        // by temporarily adjusting velocity after PlatformerBehavior.Update.
        var gamepad = Engine.InputManager.GetGamepad(0);
        float stickX = gamepad.GetAxis(GamepadAxis.LeftStickX);

        // --- Gamepad jump ---
        // GamepadPressableInput.WasJustPressed is stubbed. Use WasButtonJustPressed directly.
        bool gamepadJumpPressed = gamepad.WasButtonJustPressed(Buttons.A);

        // Inject a synthetic jump press if gamepad A was pressed and we're on the ground.
        // We do this by temporarily swapping JumpInput to a pre-pressed state — simpler:
        // just call the behavior normally (keyboard jump still works) and manually apply
        // the jump velocity here if gamepad triggered it.
        _platformer.Update(this, time);

        // Apply gamepad stick on top of keyboard movement (axis wins if non-trivial)
        if (MathF.Abs(stickX) > 0.15f)
        {
            float groundMax = _platformer.GroundMovement?.MaxSpeedX ?? 220f;
            VelocityX = stickX * groundMax;
        }

        // Apply gamepad jump manually (PlatformerBehavior.IsOnGround is set after Update)
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
        bool jumpJustPressed = Engine.InputManager.Keyboard.WasKeyPressed(Keys.Space)
                             || gamepadJumpPressed;
        if (jumpJustPressed && !_platformer.IsOnGround && !_platformer.IsApplyingJump && _airJumpsRemaining > 0)
        {
            float jumpVel = _platformer.AirMovement.JumpVelocity;
            VelocityY = jumpVel;
            _airJumpsRemaining--;
        }

        // --- Shooting (no cooldown — one bullet per press) ---
        bool fireJustPressed = Engine.InputManager.Keyboard.WasKeyPressed(Keys.Z)
                             || gamepad.WasButtonJustPressed(Buttons.RightTrigger)
                             || gamepad.WasButtonJustPressed(Buttons.X);

        if (fireJustPressed)
        {
            float dir = _platformer.DirectionFacing == FlatRedBall2.Movement.HorizontalDirection.Right ? 1f : -1f;
            var bullet = Engine.GetFactory<PlayerBullet>().Create();
            bullet.Launch(dir, X, Y);
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
