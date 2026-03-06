namespace FlatRedBall2.Movement;

public class TopDownValues
{
    /// <summary>Maximum movement speed in world units per second.</summary>
    public float MaxSpeed;

    /// <summary>
    /// Seconds to accelerate from rest to <see cref="MaxSpeed"/>.
    /// Ignored when <see cref="UsesAcceleration"/> is false.
    /// </summary>
    public float AccelerationTime;

    /// <summary>
    /// Seconds to decelerate from <see cref="MaxSpeed"/> to rest when input is released.
    /// Ignored when <see cref="UsesAcceleration"/> is false.
    /// </summary>
    public float DecelerationTime;

    /// <summary>
    /// When false, velocity is set directly to the input-scaled <see cref="MaxSpeed"/> each frame
    /// (instantaneous response). When true, <see cref="AccelerationTime"/> and <see cref="DecelerationTime"/>
    /// are used to smoothly ramp speed.
    /// </summary>
    public bool UsesAcceleration;

    /// <summary>
    /// When true, <see cref="DirectionFacing"/> is updated from the input direction each frame.
    /// Takes priority over <see cref="UpdateDirectionFromVelocity"/>.
    /// </summary>
    public bool UpdateDirectionFromInput = true;

    /// <summary>
    /// When true and <see cref="UpdateDirectionFromInput"/> is false, <see cref="DirectionFacing"/>
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
