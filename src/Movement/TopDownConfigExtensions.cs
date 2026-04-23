using System;

namespace FlatRedBall2.Movement;

/// <summary>
/// Extension methods that apply a JSON-loaded <see cref="TopDownConfig"/> onto a runtime
/// <see cref="TopDownBehavior"/>.
/// </summary>
public static class TopDownConfigExtensions
{
    /// <summary>
    /// Applies <paramref name="config"/>'s movement fields to <paramref name="behavior"/>'s
    /// <see cref="TopDownBehavior.MovementValues"/>. If <c>MovementValues</c> is null, a new
    /// <see cref="TopDownValues"/> is created. Fields not present in the JSON are left at
    /// their existing value (or the <see cref="TopDownValues"/> default for a fresh instance).
    /// </summary>
    public static void ApplyTo(this TopDownConfig config, TopDownBehavior behavior)
    {
        var movement = config.Movement;
        if (movement == null) return;

        var values = behavior.MovementValues ?? new TopDownValues();

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
