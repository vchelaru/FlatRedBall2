using System;

namespace FlatRedBall2.Movement;

public class PlatformerValues
{
    /// <summary>
    /// Sets <see cref="JumpVelocity"/>, <see cref="JumpApplyLength"/>, and <see cref="JumpApplyByButtonHold"/>
    /// so that a tap produces a jump of <paramref name="minHeight"/> world units and a full hold
    /// reaches <paramref name="maxHeight"/> world units. <see cref="Gravity"/> must be set before calling
    /// this method, and changing it afterward will invalidate the computed jump values.
    /// When <paramref name="maxHeight"/> is null or equal to <paramref name="minHeight"/>,
    /// the jump is fixed-height (button hold has no effect).
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="Gravity"/> is not positive.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minHeight"/> is not positive,
    /// or <paramref name="maxHeight"/> is less than <paramref name="minHeight"/>.</exception>
    public void SetJumpHeights(float minHeight, float? maxHeight = null)
    {
        if (Gravity <= 0f)
            throw new InvalidOperationException("Gravity must be positive before calling SetJumpHeights.");
        if (minHeight <= 0f)
            throw new ArgumentOutOfRangeException(nameof(minHeight), "minHeight must be positive.");
        if (maxHeight.HasValue && maxHeight.Value < minHeight)
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "maxHeight must be >= minHeight.");

        JumpVelocity = MathF.Sqrt(2f * Gravity * minHeight);

        if (maxHeight.HasValue && maxHeight.Value > minHeight)
        {
            float sustainSeconds = (maxHeight.Value - minHeight) / JumpVelocity;
            JumpApplyLength = TimeSpan.FromSeconds(sustainSeconds);
            JumpApplyByButtonHold = true;
        }
        else
        {
            JumpApplyLength = TimeSpan.Zero;
            JumpApplyByButtonHold = false;
        }
    }

    public float MaxSpeedX;

    /// <summary>Time to reach <see cref="MaxSpeedX"/> from rest. <see cref="TimeSpan.Zero"/> means instant.</summary>
    public TimeSpan AccelerationTimeX;

    /// <summary>Time to decelerate from <see cref="MaxSpeedX"/> to rest when input is released. <see cref="TimeSpan.Zero"/> means instant.</summary>
    public TimeSpan DecelerationTimeX;

    public float Gravity;
    public float MaxFallSpeed;
    public float JumpVelocity;

    /// <summary>How long the jump velocity continues to be applied while the button is held.</summary>
    public TimeSpan JumpApplyLength;

    /// <summary>
    /// When true, <see cref="JumpApplyLength"/> is only consumed while the jump button is held;
    /// releasing early cuts the jump short.
    /// </summary>
    public bool JumpApplyByButtonHold;

    /// <summary>
    /// When false, <see cref="AccelerationTimeX"/> and <see cref="DecelerationTimeX"/> are ignored
    /// and velocity is set directly to the input-scaled <see cref="MaxSpeedX"/>.
    /// </summary>
    public bool UsesAcceleration;
}
