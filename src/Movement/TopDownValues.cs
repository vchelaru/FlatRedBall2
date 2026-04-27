using System;

namespace FlatRedBall2.Movement;

/// <summary>
/// Defines the movement characteristics (speed, acceleration, deceleration, etc.) for top-down entities.
/// <para>
/// Duration fields are <see cref="TimeSpan"/>, matching the engine-wide convention (see
/// <see cref="TimeManager"/>). Set them with <c>TimeSpan.FromSeconds(0.1)</c> — a bare
/// <c>0.1f</c> will not compile.
/// </para>
/// </summary>
public class TopDownValues
{
    /// <summary>
    /// Resets every field to the same state as a freshly constructed <see cref="TopDownValues"/>.
    /// Called by <see cref="TopDownConfigExtensions.ApplyTo"/> before re-populating a reused
    /// instance so leftover values from a prior apply don't leak into the new state. When adding a
    /// new field to this class, also add it here.
    /// </summary>
    internal void ResetToDefaults()
    {
        MaxSpeed = 0f;
        AccelerationTime = TimeSpan.Zero;
        DecelerationTime = TimeSpan.Zero;
        UpdateDirectionFromInput = true;
        UpdateDirectionFromVelocity = false;
        IsUsingCustomDeceleration = false;
        CustomDecelerationValue = 0f;
    }

    /// <summary>Maximum movement speed in world units per second.</summary>
    public float MaxSpeed;

    /// <summary>
    /// Time to accelerate from rest to <see cref="MaxSpeed"/>. <see cref="TimeSpan.Zero"/> means instant
    /// (velocity is set directly to the input-scaled <see cref="MaxSpeed"/>).
    /// </summary>
    public TimeSpan AccelerationTime;

    /// <summary>
    /// Time to decelerate from <see cref="MaxSpeed"/> to rest when input is released.
    /// <see cref="TimeSpan.Zero"/> means instant.
    /// </summary>
    public TimeSpan DecelerationTime;

    /// <summary>
    /// When true, <see cref="TopDownBehavior.DirectionFacing"/> is updated from the input direction each frame.
    /// Takes priority over <see cref="UpdateDirectionFromVelocity"/>.
    /// </summary>
    public bool UpdateDirectionFromInput = true;

    /// <summary>
    /// When true and <see cref="UpdateDirectionFromInput"/> is false, <see cref="TopDownBehavior.DirectionFacing"/>
    /// is updated from the entity's actual velocity.
    /// </summary>
    public bool UpdateDirectionFromVelocity;

    /// <summary>
    /// When true and the entity is moving faster than <see cref="MaxSpeed"/>,
    /// deceleration uses <see cref="CustomDecelerationValue"/> instead of the acceleration derived
    /// from <see cref="DecelerationTime"/>. Useful for overriding slow-down after an impulse or knockback.
    /// </summary>
    public bool IsUsingCustomDeceleration;

    /// <summary>Deceleration magnitude (units/s²) used when <see cref="IsUsingCustomDeceleration"/> is true.</summary>
    public float CustomDecelerationValue;
}
