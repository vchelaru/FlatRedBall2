namespace FlatRedBall2.Input;

/// <summary>
/// String-keyed action interface for input devices. Intended for action-binding scenarios
/// (e.g. "Jump", "Fire") where the concrete key/button is configured elsewhere and the
/// gameplay code only knows the action name.
/// </summary>
/// <remarks>
/// Not currently implemented by <see cref="Keyboard"/>, <see cref="Gamepad"/>, or
/// <see cref="Cursor"/>. Reserved for a future action-binding system; today, callers should
/// use <see cref="IKeyboard"/>, <see cref="IGamepad"/>, or <see cref="IPressableInput"/>
/// (with <see cref="KeyboardPressableInput"/> / <see cref="GamepadPressableInput"/>) directly.
/// </remarks>
public interface IInputDevice
{
    /// <summary>True every frame the named action is held.</summary>
    bool IsActionDown(string action);

    /// <summary>True only on the first frame the named action transitions from up to down.</summary>
    bool WasActionPressed(string action);
}
