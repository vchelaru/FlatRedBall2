using System;

namespace FlatRedBall2.Movement;

/// <summary>
/// Extension methods that apply a JSON-loaded <see cref="TopDownConfig"/> onto a runtime
/// <see cref="TopDownBehavior"/>.
/// </summary>
public static class TopDownConfigExtensions
{
    /// <summary>
    /// Replaces <paramref name="behavior"/>'s <see cref="TopDownBehavior.MovementValues"/> with
    /// the state described by <paramref name="config"/>. This is a <b>full replace</b>, not an
    /// overlay:
    /// <list type="bullet">
    ///   <item><description>When the JSON has a <c>movement</c> block, the behavior's
    ///   <c>MovementValues</c> is populated from it. Fields absent in the JSON reset to their
    ///   <see cref="TopDownValues"/> defaults — no residual values from a prior <c>ApplyTo</c>
    ///   survive.</description></item>
    ///   <item><description>When the JSON omits the <c>movement</c> block entirely, the behavior
    ///   is left untouched — an empty config is a no-op, not a clear.</description></item>
    /// </list>
    /// <para>
    /// <b>In-place mutation:</b> when <see cref="TopDownBehavior.MovementValues"/> is already
    /// populated, this method mutates the existing <see cref="TopDownValues"/> instance instead of
    /// allocating a new one — per-frame <c>ApplyTo</c> for context swaps is zero-allocation on
    /// the steady state.
    /// </para>
    /// </summary>
    public static void ApplyTo(this TopDownConfig config, TopDownBehavior behavior)
    {
        var movement = config.Movement;
        if (movement == null) return;

        TopDownValues values;
        if (behavior.MovementValues != null)
        {
            // Reset before populating so fields absent from the JSON revert to their defaults
            // instead of retaining values from a prior ApplyTo call.
            behavior.MovementValues.ResetToDefaults();
            values = behavior.MovementValues;
        }
        else
        {
            values = new TopDownValues();
        }

        if (movement.MaxSpeed.HasValue) values.MaxSpeed = movement.MaxSpeed.Value;
        if (movement.AccelerationTime.HasValue) values.AccelerationTime = TimeSpan.FromSeconds(movement.AccelerationTime.Value);
        if (movement.DecelerationTime.HasValue) values.DecelerationTime = TimeSpan.FromSeconds(movement.DecelerationTime.Value);
        if (movement.UpdateDirectionFromInput.HasValue) values.UpdateDirectionFromInput = movement.UpdateDirectionFromInput.Value;
        if (movement.UpdateDirectionFromVelocity.HasValue) values.UpdateDirectionFromVelocity = movement.UpdateDirectionFromVelocity.Value;
        if (movement.IsUsingCustomDeceleration.HasValue) values.IsUsingCustomDeceleration = movement.IsUsingCustomDeceleration.Value;
        if (movement.CustomDecelerationValue.HasValue) values.CustomDecelerationValue = movement.CustomDecelerationValue.Value;

        behavior.MovementValues = values;
    }
}
