using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Adapts a single keyboard <see cref="Keys"/> value to the <see cref="IPressableInput"/>
/// interface. Use to pass a key into systems that accept any pressable input (jump action,
/// menu confirm, etc.) and to combine with other pressable sources via
/// <see cref="IPressableInputExtensions.Or"/>.
/// </summary>
public class KeyboardPressableInput : IPressableInput
{
    private readonly IKeyboard _keyboard;
    private readonly Keys _key;

    /// <summary>
    /// Creates a pressable input wrapping <paramref name="key"/> on <paramref name="keyboard"/>.
    /// Typical usage: <c>new KeyboardPressableInput(Engine.Input.Keyboard, Keys.Space)</c>.
    /// </summary>
    public KeyboardPressableInput(IKeyboard keyboard, Keys key)
    {
        _keyboard = keyboard;
        _key = key;
    }

    /// <inheritdoc/>
    public bool IsDown => _keyboard.IsKeyDown(_key);

    /// <inheritdoc/>
    public bool WasJustPressed => _keyboard.WasKeyPressed(_key);

    /// <inheritdoc/>
    public bool WasJustReleased => _keyboard.WasKeyJustReleased(_key);
}
