namespace FlatRedBall2.Input;

public class GamepadInput2D : I2DInput
{
    private readonly IGamepad _gamepad;
    private readonly GamepadAxis _xAxis;
    private readonly GamepadAxis _yAxis;

    public GamepadInput2D(IGamepad gamepad, GamepadAxis xAxis, GamepadAxis yAxis)
    {
        _gamepad = gamepad;
        _xAxis = xAxis;
        _yAxis = yAxis;
    }

    public float X => _gamepad.GetAxis(_xAxis);
    public float Y => _gamepad.GetAxis(_yAxis);
}
