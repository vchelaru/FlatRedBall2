using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

public class Keyboard : IKeyboard
{
    private KeyboardState _current;
    private KeyboardState _previous;

    internal void Update()
    {
        _previous = _current;
        _current = Microsoft.Xna.Framework.Input.Keyboard.GetState();
    }

    public bool IsKeyDown(Keys key) => _current.IsKeyDown(key);
    public bool WasKeyPressed(Keys key) => _current.IsKeyDown(key) && !_previous.IsKeyDown(key);
}
