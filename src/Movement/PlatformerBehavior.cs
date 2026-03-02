using System;
using FlatRedBall2.Input;

namespace FlatRedBall2.Movement;

public class PlatformerBehavior
{
    /// <summary>
    /// Movement values used while the entity is on the ground. Defaults to
    /// <see cref="AirMovement"/> if null.
    /// </summary>
    public PlatformerValues? GroundMovement { get; set; }

    public PlatformerValues AirMovement { get; set; } = new();

    // Input — must be set before Update is called
    public IPressableInput? JumpInput { get; set; }
    public I2DInput? MovementInput { get; set; }

    /// <summary>Reflects the ground state determined during the most recent <see cref="Update"/> call.</summary>
    public bool IsOnGround { get; private set; }

    public HorizontalDirection DirectionFacing { get; private set; } = HorizontalDirection.Right;

    private double _jumpStartTime;
    private PlatformerValues? _jumpValues;
    private bool _isApplyingJump;

    /// <summary>
    /// Applies platformer movement to <paramref name="entity"/> for the current frame.
    /// Must be called AFTER collision resolution — reads <c>entity.LastReposition</c>
    /// to determine whether the entity is on the ground.
    /// </summary>
    public void Update(Entity entity, FrameTime time)
    {
        if (time.DeltaSeconds == 0f) return;

        // A. Determine ground state
        IsOnGround = entity.LastReposition.Y > 0;

        // B. Horizontal input
        float inputX = MovementInput?.X ?? 0f;

        if (inputX > 0f)
            DirectionFacing = HorizontalDirection.Right;
        else if (inputX < 0f)
            DirectionFacing = HorizontalDirection.Left;

        var current = IsOnGround ? (GroundMovement ?? AirMovement) : AirMovement;

        if (!current.UsesAcceleration || (current.AccelerationTimeX == 0f && current.DecelerationTimeX == 0f))
        {
            entity.VelocityX = inputX * current.MaxSpeedX;
        }
        else
        {
            float targetSpeed = inputX * current.MaxSpeedX;
            float velocityX = entity.VelocityX;
            float diff = targetSpeed - velocityX;

            // Use AccelerationTimeX when pressing toward target; DecelerationTimeX when releasing or braking.
            // "Speeding up" = diff and target are in the same direction (both nonzero and same sign).
            bool speedingUp = targetSpeed != 0f && diff != 0f && MathF.Sign(diff) == MathF.Sign(targetSpeed);

            float accelMagnitude = speedingUp
                ? (current.AccelerationTimeX > 0f ? current.MaxSpeedX / current.AccelerationTimeX : float.MaxValue)
                : (current.DecelerationTimeX > 0f ? current.MaxSpeedX / current.DecelerationTimeX : float.MaxValue);

            float maxDeltaV = accelMagnitude * time.DeltaSeconds;
            float clampedDiff = MathF.Abs(diff) <= maxDeltaV ? diff : maxDeltaV * MathF.Sign(diff);
            entity.AccelerationX = clampedDiff / time.DeltaSeconds;
        }

        // C. Apply gravity
        entity.AccelerationY = -current.Gravity;

        // E. Handle jump (before fall-speed clamp so jump velocity is applied first)
        if (JumpInput?.WasJustPressed == true && IsOnGround)
        {
            entity.VelocityY = current.JumpVelocity;
            _jumpStartTime = time.SinceGameStart.TotalSeconds;
            _jumpValues = current;
            _isApplyingJump = true;
        }

        if (_isApplyingJump && _jumpValues != null)
        {
            if (_jumpValues.JumpApplyByButtonHold && JumpInput?.IsDown == false)
            {
                _isApplyingJump = false;
            }
            else if (time.SinceGameStart.TotalSeconds - _jumpStartTime >= _jumpValues.JumpApplyLength)
            {
                _isApplyingJump = false;
            }
            else
            {
                entity.VelocityY = _jumpValues.JumpVelocity;
            }
        }

        // D. Clamp fall speed (after jump sustain)
        entity.VelocityY = MathF.Max(-current.MaxFallSpeed, entity.VelocityY);
    }
}
