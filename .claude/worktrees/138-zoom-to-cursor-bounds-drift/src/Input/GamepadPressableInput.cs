using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Adapts a single gamepad <see cref="Buttons"/> value to the <see cref="IPressableInput"/>
/// interface. Use to pass a button into systems that accept any pressable input and to
/// combine with other pressable sources (e.g. a keyboard key) via
/// <see cref="IPressableInputExtensions.Or"/>.
/// </summary>
public class GamepadPressableInput : IPressableInput
{
    private readonly IGamepad _gamepad;
    private readonly Buttons _button;

    /// <summary>
    /// Creates a pressable input wrapping <paramref name="button"/> on <paramref name="gamepad"/>.
    /// Typical usage: <c>new GamepadPressableInput(Engine.Input.GetGamepad(0), Buttons.A)</c>.
    /// </summary>
    public GamepadPressableInput(IGamepad gamepad, Buttons button)
    {
        _gamepad = gamepad;
        _button = button;
    }

    /// <inheritdoc/>
    public bool IsDown => _gamepad.IsButtonDown(_button);

    /// <inheritdoc/>
    public bool WasJustPressed => _gamepad.WasButtonJustPressed(_button);

    /// <inheritdoc/>
    public bool WasJustReleased => _gamepad.WasButtonJustReleased(_button);
}
