using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Default <see cref="IKeyboard"/> implementation backed by MonoGame's
/// <see cref="Microsoft.Xna.Framework.Input.Keyboard"/>. Created and updated by
/// <see cref="InputManager"/> — game code should access keyboard input via
/// <c>Engine.Input.Keyboard</c> rather than constructing this directly.
/// </summary>
public class Keyboard : IKeyboard
{
    private KeyboardState _current;
    private KeyboardState _previous;

    // Called once per frame by InputManager before entity/screen logic runs.
    internal void Update()
    {
        _previous = _current;
        _current = Microsoft.Xna.Framework.Input.Keyboard.GetState();
    }

    /// <inheritdoc/>
    public bool IsKeyDown(Keys key) => _current.IsKeyDown(key);

    /// <inheritdoc/>
    public bool WasKeyPressed(Keys key) => _current.IsKeyDown(key) && !_previous.IsKeyDown(key);

    /// <inheritdoc/>
    public bool WasKeyJustReleased(Keys key) => _previous.IsKeyDown(key) && !_current.IsKeyDown(key);
}
