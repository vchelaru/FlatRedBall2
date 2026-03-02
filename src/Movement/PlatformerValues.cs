namespace FlatRedBall2.Movement;

public class PlatformerValues
{
    public float MaxSpeedX;

    /// <summary>Seconds to reach <see cref="MaxSpeedX"/> from rest. 0 means instant.</summary>
    public float AccelerationTimeX;

    /// <summary>Seconds to decelerate from <see cref="MaxSpeedX"/> to rest when input is released. 0 means instant.</summary>
    public float DecelerationTimeX;

    public float Gravity;
    public float MaxFallSpeed;
    public float JumpVelocity;

    /// <summary>Time in seconds the jump velocity continues to be applied while the button is held.</summary>
    public float JumpApplyLength;

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
