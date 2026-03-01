using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

public class GamepadPressableInput : IPressableInput
{
    private readonly IGamepad _gamepad;
    private readonly Buttons _button;

    public GamepadPressableInput(IGamepad gamepad, Buttons button)
    {
        _gamepad = gamepad;
        _button = button;
    }

    public bool IsDown => _gamepad.IsButtonDown(_button);
    public bool WasJustPressed => false; // TODO: needs previous state tracking
    public bool WasJustReleased => false; // TODO: needs previous state tracking
}
