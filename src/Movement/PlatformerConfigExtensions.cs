using System;

namespace FlatRedBall2.Movement;

public static class PlatformerConfigExtensions
{
    /// <summary>
    /// Applies <paramref name="config"/>'s movement slots to <paramref name="behavior"/>.
    /// Slots not present in the JSON are left untouched. An <c>afterDoubleJump</c> slot is parsed
    /// but intentionally ignored — <see cref="PlatformerBehavior"/> has no double-jump slot yet;
    /// the schema reserves the name so files authored today stay valid once the slot lands.
    /// </summary>
    /// <exception cref="InvalidOperationException">A slot specifies both derived jump fields
    /// (<c>minJumpHeight</c>/<c>maxJumpHeight</c>) and raw jump fields (<c>JumpVelocity</c>/
    /// <c>JumpApplyLength</c>/<c>JumpApplyByButtonHold</c>).</exception>
    public static void ApplyTo(this PlatformerConfig config, PlatformerBehavior behavior)
    {
        var movement = config.Movement;
        if (movement == null) return;

        if (movement.Ground != null)
            behavior.GroundMovement = ToPlatformerValues(movement.Ground, "ground");

        if (movement.Air != null)
            behavior.AirMovement = ToPlatformerValues(movement.Air, "air");

        // afterDoubleJump is parsed (for forward-compat) but has no behavior slot to receive it.
        if (movement.AfterDoubleJump != null)
            _ = ToPlatformerValues(movement.AfterDoubleJump, "afterDoubleJump");
    }

    private static PlatformerValues ToPlatformerValues(MovementSlot slot, string slotName)
    {
        var values = new PlatformerValues();

        if (slot.MaxSpeedX.HasValue) values.MaxSpeedX = slot.MaxSpeedX.Value;
        if (slot.AccelerationTimeX.HasValue) values.AccelerationTimeX = TimeSpan.FromSeconds(slot.AccelerationTimeX.Value);
        if (slot.DecelerationTimeX.HasValue) values.DecelerationTimeX = TimeSpan.FromSeconds(slot.DecelerationTimeX.Value);

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

        ApplyJumpMode(slot, values, slotName);

        return values;
    }

    private static void ApplyJumpMode(MovementSlot slot, PlatformerValues values, string slotName)
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
            values.SetJumpHeights(slot.MinJumpHeight.Value, slot.MaxJumpHeight);
            return;
        }

        if (slot.JumpVelocity.HasValue) values.JumpVelocity = slot.JumpVelocity.Value;
        if (slot.JumpApplyLength.HasValue) values.JumpApplyLength = TimeSpan.FromSeconds(slot.JumpApplyLength.Value);
        if (slot.JumpApplyByButtonHold.HasValue) values.JumpApplyByButtonHold = slot.JumpApplyByButtonHold.Value;
    }
}
