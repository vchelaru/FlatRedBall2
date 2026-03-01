using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

public class Gamepad : IGamepad
{
    private readonly int _index;
    private GamePadState _current;
    private GamePadState _previous;

    internal Gamepad(int index) => _index = index;

    internal void Update()
    {
        _previous = _current;
        _current = GamePad.GetState(_index);
    }

    public bool IsButtonDown(Buttons button) => _current.IsButtonDown(button);

    public float GetAxis(GamepadAxis axis) => axis switch
    {
        GamepadAxis.LeftStickX  => _current.ThumbSticks.Left.X,
        GamepadAxis.LeftStickY  => _current.ThumbSticks.Left.Y,
        GamepadAxis.RightStickX => _current.ThumbSticks.Right.X,
        GamepadAxis.RightStickY => _current.ThumbSticks.Right.Y,
        GamepadAxis.LeftTrigger  => _current.Triggers.Left,
        GamepadAxis.RightTrigger => _current.Triggers.Right,
        _ => 0f
    };
}
