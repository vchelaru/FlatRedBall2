using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Per-frame state for one gamepad controller. Obtain via <c>Engine.Input.GetGamepad(index)</c>.
/// Returns zeroed/unpressed state when no controller is connected at that index — safe to poll without checking
/// <see cref="IsConnected"/>; check it only to prompt the player or detect a join.
/// </summary>
public interface IGamepad
{
    /// <summary>
    /// True while a controller is plugged in at this index. Updated once per frame with the rest of the input state,
    /// and always true while input is injected (automation). Subscribe to <see cref="InputManager.GamepadConnectionChanged"/>
    /// to react to plugging in or unplugging.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Thumbstick deadzone in the range [0, 1), applied to both sticks by <see cref="GetAxis"/> (triggers are unaffected).
    /// Values past the deadzone are rescaled so output still ramps from 0 to 1. Null (the default) leaves MonoGame's
    /// built-in thresholds (about 0.24 left, 0.27 right) in place. Throws <see cref="System.ArgumentOutOfRangeException"/>
    /// outside [0, 1).
    /// </summary>
    float? Deadzone { get; set; }

    /// <summary>
    /// Whether <see cref="Deadzone"/> is measured per axis (default) or radially. Applies to the default thresholds too.
    /// </summary>
    GamepadDeadzoneType DeadzoneType { get; set; }

    /// <summary>
    /// Sets the rumble motors, each 0 (off) to 1 (full); out-of-range values are clamped. The setting persists until
    /// changed, so call <c>SetVibration(0, 0)</c> to stop. Returns false if the controller is missing or rejects it.
    /// <paramref name="leftMotor"/> is the low-frequency (heavy) motor, <paramref name="rightMotor"/> the high-frequency one.
    /// </summary>
    bool SetVibration(float leftMotor, float rightMotor);

    /// <summary>True every frame the button is held down.</summary>
    bool IsButtonDown(Buttons button);

    /// <summary>True only on the first frame the button transitions from up to down.</summary>
    bool WasButtonJustPressed(Buttons button);

    /// <summary>True only on the first frame the button transitions from down to up.</summary>
    bool WasButtonJustReleased(Buttons button);

    /// <summary>
    /// Returns the axis value for the given analog input.
    /// Sticks return −1.0 to +1.0; stick Y+ is up, matching world-space coordinates.
    /// Triggers return 0.0 to +1.0.
    /// </summary>
    float GetAxis(GamepadAxis axis);
}
