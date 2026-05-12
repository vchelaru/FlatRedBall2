using System;

namespace FlatRedBall2.Input;

/// <summary>
/// Adapts two <see cref="GamepadAxis"/> values to <see cref="I2DInput"/>, suitable for stick-driven
/// movement. <see cref="X"/>/<see cref="Y"/> mirror the underlying axis ranges (sticks: −1..+1,
/// triggers: 0..+1) with an optional radial-style deadzone applied per axis.
/// </summary>
/// <remarks>
/// The d-pad is exposed as four buttons, not as axes. To drive directional movement from the d-pad,
/// wrap <see cref="GamepadPressableInput"/> values in a custom <see cref="I2DInput"/> implementation.
/// </remarks>
public class GamepadInput2D : I2DInput
{
    private readonly IGamepad _gamepad;
    private readonly GamepadAxis _xAxis;
    private readonly GamepadAxis _yAxis;
    private readonly float _deadzone;

    /// <summary>
    /// Creates a 2D input from two gamepad axes — typically <see cref="GamepadAxis.LeftStickX"/> and
    /// <see cref="GamepadAxis.LeftStickY"/> for character movement.
    /// </summary>
    /// <param name="gamepad">The source gamepad. Obtain via <c>Engine.Input.GetGamepad(index)</c>.</param>
    /// <param name="xAxis">Axis mapped to <see cref="X"/>.</param>
    /// <param name="yAxis">Axis mapped to <see cref="Y"/>.</param>
    /// <param name="deadzone">
    /// Per-axis deadzone: axis values with absolute magnitude below this threshold are reported as 0.
    /// Typical thumbsticks need 0.1–0.2 to avoid resting drift. Defaults to 0 (no deadzone).
    /// </param>
    public GamepadInput2D(IGamepad gamepad, GamepadAxis xAxis, GamepadAxis yAxis, float deadzone = 0f)
    {
        _gamepad = gamepad;
        _xAxis = xAxis;
        _yAxis = yAxis;
        _deadzone = deadzone;
    }

    /// <summary>Current value of the X axis after deadzone, in the range of the underlying <see cref="GamepadAxis"/>.</summary>
    public float X => Apply(_gamepad.GetAxis(_xAxis));

    /// <summary>Current value of the Y axis after deadzone, in the range of the underlying <see cref="GamepadAxis"/>. Y+ is up for stick axes.</summary>
    public float Y => Apply(_gamepad.GetAxis(_yAxis));

    private float Apply(float value) => MathF.Abs(value) < _deadzone ? 0f : value;
}
