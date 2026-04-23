using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

/// <summary>
/// Default <see cref="IGamepad"/> implementation backed by MonoGame's
/// <see cref="Microsoft.Xna.Framework.Input.GamePad"/> at a fixed player index. Created and
/// updated by <see cref="InputManager"/> — game code should access gamepads via
/// <c>Engine.Input.GetGamepad(index)</c> rather than constructing this directly.
/// </summary>
/// <remarks>
/// Polling a disconnected controller is safe: <see cref="IsButtonDown"/> returns <c>false</c>
/// and <see cref="GetAxis"/> returns 0 until a controller is reconnected at this index.
/// </remarks>
public class Gamepad : IGamepad
{
    private readonly int _index;
    private GamePadState _current;
    private GamePadState _previous;

    internal Gamepad(int index) => _index = index;

    // Called once per frame by InputManager before entity/screen logic runs.
    internal void Update()
    {
        _previous = _current;
        _current = GamePad.GetState(_index);
    }

    /// <inheritdoc/>
    public bool IsButtonDown(Buttons button) => _current.IsButtonDown(button);

    /// <inheritdoc/>
    public bool WasButtonJustPressed(Buttons button) => !_previous.IsButtonDown(button) && _current.IsButtonDown(button);

    /// <inheritdoc/>
    public bool WasButtonJustReleased(Buttons button) => _previous.IsButtonDown(button) && !_current.IsButtonDown(button);

    /// <inheritdoc/>
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
