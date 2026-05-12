namespace FlatRedBall2.Input;

/// <summary>Identifies a gamepad analog input. Pass to <see cref="IGamepad.GetAxis"/>.</summary>
public enum GamepadAxis
{
    /// <summary>Left thumbstick horizontal axis. −1 = left, +1 = right.</summary>
    LeftStickX,

    /// <summary>Left thumbstick vertical axis. −1 = down, +1 = up (Y+ up, matching world space).</summary>
    LeftStickY,

    /// <summary>Right thumbstick horizontal axis. −1 = left, +1 = right.</summary>
    RightStickX,

    /// <summary>Right thumbstick vertical axis. −1 = down, +1 = up (Y+ up, matching world space).</summary>
    RightStickY,

    /// <summary>Left trigger. 0 = released, +1 = fully pressed.</summary>
    LeftTrigger,

    /// <summary>Right trigger. 0 = released, +1 = fully pressed.</summary>
    RightTrigger,
}
