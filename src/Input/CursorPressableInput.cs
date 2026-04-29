namespace FlatRedBall2.Input;

/// <summary>
/// Adapts an <see cref="ICursor"/>'s primary button (left mouse / first touch) to
/// <see cref="IPressableInput"/>. Useful for touch-to-fire or tap-to-interact where the cursor's
/// primary state should drive the same logic as a keyboard/gamepad button.
/// </summary>
public class CursorPressableInput : IPressableInput
{
    private readonly ICursor _cursor;

    /// <summary>
    /// Creates a pressable input backed by the cursor's primary button.
    /// </summary>
    /// <param name="cursor">The cursor to read. Obtain via <c>Engine.Input.Cursor</c>.</param>
    public CursorPressableInput(ICursor cursor) => _cursor = cursor;

    /// <inheritdoc/>
    public bool IsDown => _cursor.PrimaryDown;

    /// <inheritdoc/>
    public bool WasJustPressed => _cursor.PrimaryPressed;

    /// <inheritdoc/>
    public bool WasJustReleased => _cursor.PrimaryClick;
}
