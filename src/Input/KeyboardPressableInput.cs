using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

public class KeyboardPressableInput : IPressableInput
{
    private readonly IKeyboard _keyboard;
    private readonly Keys _key;

    public KeyboardPressableInput(IKeyboard keyboard, Keys key)
    {
        _keyboard = keyboard;
        _key = key;
    }

    public bool IsDown => _keyboard.IsKeyDown(_key);
    public bool WasJustPressed => _keyboard.WasKeyPressed(_key);
    public bool WasJustReleased => _keyboard.WasKeyJustReleased(_key);
}
