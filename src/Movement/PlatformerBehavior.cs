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

    /// <summary>
    /// The horizontal direction the entity is currently facing.
    /// Updated each frame from <see cref="MovementInput"/>: non-zero X input sets the direction;
    /// zero input leaves it unchanged (last direction is remembered).
    /// </summary>
    public HorizontalDirection DirectionFacing { get; private set; } = HorizontalDirection.Right;

    /// <summary>
    /// True while the jump sustain is active — the jump button is held and
    /// <see cref="PlatformerValues.JumpApplyLength"/> has not yet elapsed.
    /// </summary>
    public bool IsApplyingJump { get; private set; }

    private TimeSpan _jumpStartTime;
    private PlatformerValues? _jumpValues;

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

        if (!current.UsesAcceleration || (current.AccelerationTimeX == TimeSpan.Zero && current.DecelerationTimeX == TimeSpan.Zero))
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
                ? (current.AccelerationTimeX > TimeSpan.Zero ? current.MaxSpeedX / (float)current.AccelerationTimeX.TotalSeconds : float.MaxValue)
                : (current.DecelerationTimeX > TimeSpan.Zero ? current.MaxSpeedX / (float)current.DecelerationTimeX.TotalSeconds : float.MaxValue);

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
            _jumpStartTime = time.SinceGameStart;
            _jumpValues = current;
            IsApplyingJump = true;
        }

        if (IsApplyingJump && _jumpValues != null)
        {
            if (entity.LastReposition.Y < 0)
            {
                // Ceiling hit — cancel sustain and kill upward velocity so the entity drops immediately
                IsApplyingJump = false;
                entity.VelocityY = 0f;
            }
            else if (_jumpValues.JumpApplyByButtonHold && JumpInput?.IsDown == false)
            {
                IsApplyingJump = false;
            }
            else if (time.SinceGameStart - _jumpStartTime >= _jumpValues.JumpApplyLength)
            {
                IsApplyingJump = false;
            }
            else
            {
                entity.AccelerationY = 0f;
                entity.VelocityY = _jumpValues.JumpVelocity;
            }
        }

        // D. Clamp fall speed (after jump sustain)
        entity.VelocityY = MathF.Max(-current.MaxFallSpeed, entity.VelocityY);
    }
}
