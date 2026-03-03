using System;

namespace FlatRedBall2.Input;

public class GamepadInput2D : I2DInput
{
    private readonly IGamepad _gamepad;
    private readonly GamepadAxis _xAxis;
    private readonly GamepadAxis _yAxis;
    private readonly float _deadzone;

    /// <param name="deadzone">
    /// Axis values with absolute magnitude below this threshold are treated as zero.
    /// Typical gamepad sticks need 0.1–0.2 to avoid drift. Defaults to 0 (no deadzone).
    /// </param>
    public GamepadInput2D(IGamepad gamepad, GamepadAxis xAxis, GamepadAxis yAxis, float deadzone = 0f)
    {
        _gamepad = gamepad;
        _xAxis = xAxis;
        _yAxis = yAxis;
        _deadzone = deadzone;
    }

    public float X => Apply(_gamepad.GetAxis(_xAxis));
    public float Y => Apply(_gamepad.GetAxis(_yAxis));

    private float Apply(float value) => MathF.Abs(value) < _deadzone ? 0f : value;
}
