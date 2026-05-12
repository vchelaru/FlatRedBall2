using System;

namespace FlatRedBall2.Movement;

/// <summary>
/// Extension methods that apply a JSON-loaded <see cref="PlatformerConfig"/> onto a runtime
/// <see cref="PlatformerBehavior"/>.
/// </summary>
public static class PlatformerConfigExtensions
{
    /// <summary>
    /// Replaces <paramref name="behavior"/>'s movement slots with the state described by
    /// <paramref name="config"/>. This is a <b>full replace</b>, not an overlay — the config is
    /// the complete description of the behavior's movement state after the call returns:
    /// <list type="bullet">
    ///   <item><description>A slot present in the JSON populates the matching behavior slot.
    ///   Fields absent within that slot reset to their <see cref="PlatformerValues"/> defaults —
    ///   no residual values from a prior <c>ApplyTo</c> survive.</description></item>
    ///   <item><description>A slot absent from the JSON nulls the matching behavior slot
    ///   (<see cref="PlatformerBehavior.GroundMovement"/>,
    ///   <see cref="PlatformerBehavior.AfterDoubleJump"/>,
    ///   <see cref="PlatformerBehavior.ClimbingMovement"/>).
    ///   <see cref="PlatformerBehavior.AirMovement"/> is non-nullable; when the JSON omits
    ///   <c>air</c>, it is reset to a default <see cref="PlatformerValues"/> instance.</description></item>
    /// </list>
    /// Replace semantics let a second-context config (water/ice/power-up) swap cleanly: omit a
    /// slot to disable it, populate it to use it. No code-side null-outs required.
    /// <para>
    /// <b>In-place mutation:</b> when a behavior slot is already populated and the JSON also
    /// has that slot, this method mutates the existing <see cref="PlatformerValues"/> instance
    /// instead of allocating a new one — per-frame <c>ApplyTo</c> for context swaps is therefore
    /// zero-allocation on the steady state.
    /// </para>
    /// <para>
    /// When <paramref name="config"/> has no <c>movement</c> block at all, the behavior is left
    /// untouched — an empty config is a no-op, not a clear.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">A slot specifies both derived jump fields
    /// (<c>minJumpHeight</c>/<c>maxJumpHeight</c>) and raw jump fields (<c>JumpVelocity</c>/
    /// <c>JumpApplyLength</c>/<c>JumpApplyByButtonHold</c>).</exception>
    public static void ApplyTo(this PlatformerConfig config, PlatformerBehavior behavior)
    {
        var movement = config.Movement;
        if (movement == null) return;

        // Airborne gravity governs the jump trajectory regardless of which slot initiates the
        // jump — collision cancels ground gravity, so a grounded jump's arc runs entirely under
        // the air slot's gravity. Resolve once so every slot's derived-mode calculation uses the
        // same trajectory gravity.
        float? airGravity = movement.Air?.Gravity;

        behavior.GroundMovement = movement.Ground != null
            ? ToPlatformerValues(movement.Ground, "ground", airGravity, behavior.GroundMovement)
            : null;

        // AirMovement is non-nullable on the behavior; on JSON absence we reset in-place (or
        // allocate once if somehow null) rather than assigning null.
        if (movement.Air != null)
        {
            behavior.AirMovement = ToPlatformerValues(movement.Air, "air", airGravity, behavior.AirMovement);
        }
        else
        {
            behavior.AirMovement ??= new PlatformerValues();
            behavior.AirMovement.ResetToDefaults();
        }

        behavior.AfterDoubleJump = movement.AfterDoubleJump != null
            ? ToPlatformerValues(movement.AfterDoubleJump, "afterDoubleJump", airGravity, behavior.AfterDoubleJump)
            : null;

        behavior.ClimbingMovement = movement.Climbing != null
            ? ToPlatformerValues(movement.Climbing, "climbing", airGravity, behavior.ClimbingMovement)
            : null;
    }

    private static PlatformerValues ToPlatformerValues(MovementSlot slot, string slotName, float? airGravity, PlatformerValues? existing)
    {
        PlatformerValues values;
        if (existing != null)
        {
            // Reset before populating so fields absent from this slot revert to their defaults
            // instead of retaining values from a prior ApplyTo call.
            existing.ResetToDefaults();
            values = existing;
        }
        else
        {
            values = new PlatformerValues();
        }

        if (slot.MaxSpeedX.HasValue) values.MaxSpeedX = slot.MaxSpeedX.Value;
        if (slot.ClimbingSpeed.HasValue) values.ClimbingSpeed = slot.ClimbingSpeed.Value;
        if (slot.AccelerationTimeX.HasValue) values.AccelerationTimeX = TimeSpan.FromSeconds(slot.AccelerationTimeX.Value);
        if (slot.DecelerationTimeX.HasValue) values.DecelerationTimeX = TimeSpan.FromSeconds(slot.DecelerationTimeX.Value);
        if (slot.CoyoteTime.HasValue) values.CoyoteTime = TimeSpan.FromSeconds(slot.CoyoteTime.Value);
        if (slot.JumpInputBufferDuration.HasValue) values.JumpInputBufferDuration = TimeSpan.FromSeconds(slot.JumpInputBufferDuration.Value);

        // Gravity must be applied before SetJumpHeights — SetJumpHeights throws if Gravity is not positive.
        if (slot.Gravity.HasValue) values.Gravity = slot.Gravity.Value;
        if (slot.MaxFallSpeed.HasValue) values.MaxFallSpeed = slot.MaxFallSpeed.Value;

        if (slot.SlopeSnapDistance.HasValue) values.SlopeSnapDistance = slot.SlopeSnapDistance.Value;
        if (slot.SlopeSnapMaxAngleDegrees.HasValue) values.SlopeSnapMaxAngleDegrees = slot.SlopeSnapMaxAngleDegrees.Value;
        if (slot.UphillFullSpeedSlope.HasValue) values.UphillFullSpeedSlope = slot.UphillFullSpeedSlope.Value;
        if (slot.UphillStopSpeedSlope.HasValue) values.UphillStopSpeedSlope = slot.UphillStopSpeedSlope.Value;
        if (slot.DownhillFullSpeedSlope.HasValue) values.DownhillFullSpeedSlope = slot.DownhillFullSpeedSlope.Value;
        if (slot.DownhillMaxSpeedSlope.HasValue) values.DownhillMaxSpeedSlope = slot.DownhillMaxSpeedSlope.Value;
        if (slot.DownhillMaxSpeedMultiplier.HasValue) values.DownhillMaxSpeedMultiplier = slot.DownhillMaxSpeedMultiplier.Value;
        if (slot.CanFallThroughOneWayCollision.HasValue) values.CanFallThroughOneWayCollision = slot.CanFallThroughOneWayCollision.Value;

        ApplyJumpMode(slot, values, slotName, airGravity);

        return values;
    }

    private static void ApplyJumpMode(MovementSlot slot, PlatformerValues values, string slotName, float? airGravity)
    {
        bool hasDerived = slot.MinJumpHeight.HasValue || slot.MaxJumpHeight.HasValue;
        bool hasRaw = slot.JumpVelocity.HasValue || slot.JumpApplyLength.HasValue || slot.JumpApplyByButtonHold.HasValue;

        if (hasDerived && hasRaw)
        {
            throw new InvalidOperationException(
                $"Movement slot '{slotName}' specifies both derived jump fields (minJumpHeight/maxJumpHeight) " +
                "and raw jump fields (JumpVelocity/JumpApplyLength/JumpApplyByButtonHold). Use one or the other.");
        }

        if (hasDerived)
        {
            if (!slot.MinJumpHeight.HasValue)
            {
                throw new InvalidOperationException(
                    $"Movement slot '{slotName}' specifies maxJumpHeight without minJumpHeight. Derived jump mode requires minJumpHeight.");
            }
            // Trajectory gravity: air.Gravity if the air slot specifies one, else fall back to
            // this slot's own Gravity. The fallback preserves existing behavior for ground-only
            // configs (no air slot authored) while fixing the ground/air gravity-mismatch bug.
            float jumpGravity = airGravity ?? values.Gravity;
            values.SetJumpHeights(slot.MinJumpHeight.Value, slot.MaxJumpHeight, jumpGravity);
            return;
        }

        if (slot.JumpVelocity.HasValue) values.JumpVelocity = slot.JumpVelocity.Value;
        if (slot.JumpApplyLength.HasValue) values.JumpApplyLength = TimeSpan.FromSeconds(slot.JumpApplyLength.Value);
        if (slot.JumpApplyByButtonHold.HasValue) values.JumpApplyByButtonHold = slot.JumpApplyByButtonHold.Value;
    }
}
