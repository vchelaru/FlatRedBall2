using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Per-frame state for one gamepad controller. Obtain via <c>Engine.Input.GetGamepad(index)</c>.
/// Returns zeroed/unpressed state when no controller is connected at that index — safe to poll without checking connection.
/// </summary>
public interface IGamepad
{
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
