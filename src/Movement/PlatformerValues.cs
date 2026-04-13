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

    /// <summary>Time to reach <see cref="MaxSpeedX"/> from rest. <see cref="TimeSpan.Zero"/> (the default) means instant.</summary>
    public TimeSpan AccelerationTimeX;

    /// <summary>Time to decelerate from <see cref="MaxSpeedX"/> to rest when input is released. <see cref="TimeSpan.Zero"/> (the default) means instant.</summary>
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
    /// Maximum downward distance the entity will "snap" onto a lower surface after losing ground
    /// contact while it was grounded the previous frame. Enables the standard platformer feel of
    /// hugging a downslope or stepping off an up-ramp onto flat ground without floating for a
    /// frame. Set to <c>0</c> to disable snapping entirely (e.g., a ball/wheel movement mode that
    /// wants Sonic-style launch physics). Default <c>16</c> matches a typical one-tile step;
    /// significantly larger values feel teleport-y. Requires
    /// <see cref="PlatformerBehavior.CollisionShape"/> to be set and at least one collision
    /// relationship in <see cref="FlatRedBall2.Collision.SlopeCollisionMode.PlatformerFloor"/> mode —
    /// each such relationship contributes its <see cref="TileShapeCollection"/> as a snap probe target.
    /// </summary>
    public float SlopeSnapDistance { get; set; } = 16f;

    /// <summary>
    /// Surfaces whose upward normal is within this many degrees of straight up are considered
    /// walkable for ground-snap. Surfaces beyond this angle (walls and near-walls) will not
    /// trigger a snap — the entity falls normally. Default 60° accepts steep slopes but rejects
    /// walls.
    /// </summary>
    public float SlopeSnapMaxAngleDegrees { get; set; } = 60f;

    /// <summary>
    /// Slope angle (degrees, 0-90) at or below which walking uphill still uses full
    /// <see cref="MaxSpeedX"/>. Default <c>0</c> — falloff begins as soon as the surface tilts.
    /// Set equal to <see cref="UphillStopSpeedSlope"/> to disable uphill slowdown.
    /// </summary>
    public float UphillFullSpeedSlope { get; set; } = 0f;

    /// <summary>
    /// Slope angle (degrees, 0-90) at or above which walking uphill produces zero speed.
    /// Between <see cref="UphillFullSpeedSlope"/> and this value the speed is linearly
    /// interpolated. Default <c>60</c> (matches FRB1's predefined platformer profile).
    /// Set equal to <see cref="UphillFullSpeedSlope"/> to disable uphill slowdown.
    /// </summary>
    public float UphillStopSpeedSlope { get; set; } = 60f;

    /// <summary>
    /// Slope angle (degrees, 0-90) at or below which walking downhill uses unmodified
    /// <see cref="MaxSpeedX"/>. Above this, speed is scaled toward
    /// <see cref="DownhillMaxSpeedMultiplier"/>, reaching it at <see cref="DownhillMaxSpeedSlope"/>.
    /// Default <c>0</c>.
    /// </summary>
    public float DownhillFullSpeedSlope { get; set; } = 0f;

    /// <summary>
    /// Slope angle (degrees, 0-90) at which the full <see cref="DownhillMaxSpeedMultiplier"/>
    /// is applied while walking downhill. Between <see cref="DownhillFullSpeedSlope"/> and this
    /// value the multiplier is linearly interpolated from <c>1</c>. Default <c>60</c>.
    /// </summary>
    public float DownhillMaxSpeedSlope { get; set; } = 60f;

    /// <summary>
    /// Multiplier applied to <see cref="MaxSpeedX"/> when walking downhill at or beyond
    /// <see cref="DownhillMaxSpeedSlope"/>. Default <c>1.5</c> (matches FRB1's predefined
    /// platformer profile — a 50% speed boost at max slope). Set to <c>1</c> to disable
    /// downhill acceleration; values below <c>1</c> would slow the entity on descents.
    /// </summary>
    public float DownhillMaxSpeedMultiplier { get; set; } = 1.5f;
}
