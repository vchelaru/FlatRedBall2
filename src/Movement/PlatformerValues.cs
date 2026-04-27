using System;

namespace FlatRedBall2.Movement;

/// <summary>
/// Tunable parameters consumed by <see cref="PlatformerBehavior"/> for one movement state
/// (ground, air, climbing, etc.). One instance per slot — assign them to the matching property on
/// <see cref="PlatformerBehavior"/>. Authored directly in C# or loaded from JSON via
/// <see cref="PlatformerConfig"/> + <see cref="PlatformerConfigExtensions.ApplyTo"/>.
/// <para>
/// <b>Slot semantics:</b> while climbing, only <see cref="MaxSpeedX"/>,
/// <see cref="AccelerationTimeX"/>/<see cref="DecelerationTimeX"/>, <see cref="ClimbingSpeed"/>,
/// and the jump fields are read; gravity and slope fields are ignored. While airborne the slope
/// fields are also ignored (they apply only to grounded motion).
/// </para>
/// </summary>
public class PlatformerValues
{
    /// <summary>
    /// Resets every field to the same state as a freshly constructed <see cref="PlatformerValues"/>.
    /// Called by <see cref="PlatformerConfigExtensions.ApplyTo"/> before re-populating a reused
    /// instance so leftover values from a prior apply don't leak into the new state. When adding a
    /// new field to this class, also add it here.
    /// </summary>
    internal void ResetToDefaults()
    {
        MaxSpeedX = 0f;
        AccelerationTimeX = TimeSpan.Zero;
        DecelerationTimeX = TimeSpan.Zero;
        Gravity = 0f;
        MaxFallSpeed = 0f;
        JumpVelocity = 0f;
        ClimbingSpeed = 0f;
        JumpApplyLength = TimeSpan.Zero;
        JumpApplyByButtonHold = false;
        SlopeSnapDistance = 8f;
        SlopeSnapMaxAngleDegrees = 60f;
        UphillFullSpeedSlope = 0f;
        UphillStopSpeedSlope = 60f;
        DownhillFullSpeedSlope = 0f;
        DownhillMaxSpeedSlope = 60f;
        DownhillMaxSpeedMultiplier = 1.5f;
        CanFallThroughOneWayCollision = true;
        CoyoteTime = TimeSpan.Zero;
        JumpInputBufferDuration = TimeSpan.Zero;
    }

    /// <summary>
    /// Sets <see cref="JumpVelocity"/>, <see cref="JumpApplyLength"/>, and <see cref="JumpApplyByButtonHold"/>
    /// so that a tap produces a jump of <paramref name="minHeight"/> world units and a full hold
    /// reaches <paramref name="maxHeight"/> world units. Uses this instance's <see cref="Gravity"/>
    /// as the trajectory gravity — appropriate for an airborne-origin jump (double-jump) or any
    /// slot where the jump arc runs under the same gravity as this slot. For a grounded jump that
    /// immediately transitions to airborne physics with a different gravity, use the overload
    /// taking an explicit <c>jumpGravity</c>.
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
        SetJumpHeights(minHeight, maxHeight, Gravity);
    }

    /// <summary>
    /// Overload that takes the gravity governing the jump trajectory explicitly. Use when the
    /// slot this <see cref="PlatformerValues"/> belongs to (e.g. a ground slot) has a different
    /// <see cref="Gravity"/> than the airborne slot the entity transitions into the instant it
    /// leaves the ground — the trajectory runs under the airborne gravity, so heights must be
    /// derived from that value. <see cref="PlatformerConfigExtensions.ApplyTo"/> wires this up
    /// automatically for JSON-authored configs.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minHeight"/> is not positive,
    /// <paramref name="maxHeight"/> is less than <paramref name="minHeight"/>, or
    /// <paramref name="jumpGravity"/> is not positive.</exception>
    public void SetJumpHeights(float minHeight, float? maxHeight, float jumpGravity)
    {
        if (jumpGravity <= 0f)
            throw new ArgumentOutOfRangeException(nameof(jumpGravity), "jumpGravity must be positive.");
        if (minHeight <= 0f)
            throw new ArgumentOutOfRangeException(nameof(minHeight), "minHeight must be positive.");
        if (maxHeight.HasValue && maxHeight.Value < minHeight)
            throw new ArgumentOutOfRangeException(nameof(maxHeight), "maxHeight must be >= minHeight.");

        JumpVelocity = MathF.Sqrt(2f * jumpGravity * minHeight);

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

    /// <summary>Maximum horizontal speed in world units/second when input is at full magnitude.
    /// Slope-walking may scale this further per
    /// <see cref="UphillFullSpeedSlope"/>/<see cref="UphillStopSpeedSlope"/> /
    /// <see cref="DownhillFullSpeedSlope"/>/<see cref="DownhillMaxSpeedSlope"/>/
    /// <see cref="DownhillMaxSpeedMultiplier"/>.</summary>
    public float MaxSpeedX;

    /// <summary>Time to reach <see cref="MaxSpeedX"/> from rest. <see cref="TimeSpan.Zero"/> (the default) means instant.</summary>
    public TimeSpan AccelerationTimeX;

    /// <summary>Time to decelerate from <see cref="MaxSpeedX"/> to rest when input is released. <see cref="TimeSpan.Zero"/> (the default) means instant.</summary>
    public TimeSpan DecelerationTimeX;

    /// <summary>
    /// Downward acceleration applied to the entity while airborne. While grounded, collision
    /// resolution cancels gravity so this field has no visible effect — it only governs the
    /// trajectory during a jump or fall. A ground slot's <c>Gravity</c> is therefore effectively
    /// a hint for <see cref="SetJumpHeights(float, float?)"/>'s fallback path; the actual jump arc runs under
    /// the companion airborne slot's gravity (see the <c>jumpGravity</c> overload).
    /// </summary>
    public float Gravity;

    /// <summary>Positive cap on downward speed magnitude in world units/second. Applied each frame
    /// after jump sustain so the entity never falls faster than this. Ignored while climbing.</summary>
    public float MaxFallSpeed;

    /// <summary>Initial upward velocity in world units/second applied at jump start. Set directly
    /// for raw-mode tuning, or have <see cref="SetJumpHeights(float, float?)"/> derive it from a target height.
    /// On the climbing slot, applied when the player presses jump to leave a ladder — a value of
    /// <c>0</c> there means "drop off without upward velocity" (see also the <c>climbing JumpVelocity == 0</c>
    /// footgun noted in <c>design/TODOS.md</c>).</summary>
    public float JumpVelocity;

    /// <summary>
    /// Vertical speed (world units/sec) applied during climbing when <c>MovementInput.Y</c> is at
    /// full magnitude. Only used when the containing values instance is assigned to
    /// <see cref="PlatformerBehavior.ClimbingMovement"/> and
    /// <see cref="PlatformerBehavior.IsClimbing"/> is true. Horizontal movement on a ladder uses
    /// <see cref="MaxSpeedX"/> from the same slot; <see cref="Gravity"/>, jump fields, and slope
    /// fields are ignored while climbing.
    /// </summary>
    public float ClimbingSpeed;

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
    /// hugging a downslope across tile seams without floating for a frame. Snap also requires
    /// that the entity was on a non-flat surface last frame (<see cref="PlatformerBehavior.CurrentSlope"/>
    /// non-zero) — walking off a flat ledge onto lower flat ground falls ballistically like a
    /// cliff drop, as in most platformers. Set to <c>0</c> to disable snapping entirely (e.g., a
    /// ball/wheel movement mode that wants Sonic-style launch physics). Default <c>8</c> — half
    /// a typical 16px tile — is aggressive enough to hug downslopes that briefly go airborne at
    /// seams but small enough not to reach onto a one-tile-lower step even if the slope gate were
    /// absent. Requires <see cref="PlatformerBehavior.CollisionShape"/> to be set and at least one
    /// collision relationship in <see cref="FlatRedBall2.Collision.SlopeCollisionMode.PlatformerFloor"/>
    /// mode — each such relationship contributes its <see cref="FlatRedBall2.Collision.TileShapeCollection"/> as a snap
    /// probe target.
    /// </summary>
    public float SlopeSnapDistance { get; set; } = 8f;

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

    /// <summary>
    /// Gates the behavior's drop-through and airborne-Down suppression of one-way collision
    /// relationships. When true (default): pressing Down+Jump while grounded triggers drop-through,
    /// and holding Down while airborne suppresses one-way collisions so the entity falls through
    /// jump-through platforms. When false, both triggers are inert — Down+Jump performs a normal
    /// jump, and airborne Down has no effect on one-way relationships.
    /// </summary>
    public bool CanFallThroughOneWayCollision { get; set; } = true;

    /// <summary>
    /// How long after walking off a ledge the player can still press jump and have it interpreted
    /// as a ground jump. Standard platformer-feel forgiveness — without it, a press one frame
    /// after the ledge edge does nothing. Read from the ground slot
    /// (<see cref="PlatformerBehavior.GroundMovement"/>, falling back to
    /// <see cref="PlatformerBehavior.AirMovement"/>) at the moment ground is lost; the climbing
    /// slot's value is unused. Default <see cref="TimeSpan.Zero"/> preserves the strict
    /// "must be grounded" check.
    /// <para>
    /// <b>Gravity is NOT suspended during the coyote window.</b> The character begins falling
    /// immediately when ground is lost; only the jump-press *interpretation* window is extended.
    /// This matches standard platformer convention (Celeste, Hollow Knight) — typical values of
    /// 80–150ms produce imperceptible fall distance, while a "freeze gravity" implementation
    /// would read as a visible hover bug.
    /// </para>
    /// </summary>
    public TimeSpan CoyoteTime;

    /// <summary>
    /// How long before landing a jump press is remembered and consumed on the landing frame.
    /// Standard platformer-feel forgiveness — without it, a press one frame before landing does
    /// nothing. Read from the ground slot the entity lands into
    /// (<see cref="PlatformerBehavior.GroundMovement"/>, falling back to
    /// <see cref="PlatformerBehavior.AirMovement"/>); the climbing slot's value is unused. A
    /// buffered press is consumed by the landing-frame ground jump only — it does not also
    /// trigger an air double-jump in between. Default <see cref="TimeSpan.Zero"/> preserves the
    /// strict "press exactly on landing frame" check.
    /// </summary>
    public TimeSpan JumpInputBufferDuration;
}
