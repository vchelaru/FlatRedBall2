using Microsoft.Xna.Framework;
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
    private bool _hasInjection;
    private Buttons _injectedButtons;
    private float _leftStickX, _leftStickY, _rightStickX, _rightStickY, _leftTrigger, _rightTrigger;

    internal Gamepad(int index) => _index = index;

    internal void InjectButton(Buttons button, bool down)
    {
        _hasInjection = true;
        if (down) _injectedButtons |= button;
        else _injectedButtons &= ~button;
    }

    internal void InjectAxis(GamepadAxis axis, float value)
    {
        _hasInjection = true;
        if (axis == GamepadAxis.LeftStickX)       _leftStickX  = value;
        else if (axis == GamepadAxis.LeftStickY)  _leftStickY  = value;
        else if (axis == GamepadAxis.RightStickX) _rightStickX = value;
        else if (axis == GamepadAxis.RightStickY) _rightStickY = value;
        else if (axis == GamepadAxis.LeftTrigger)  _leftTrigger  = value;
        else if (axis == GamepadAxis.RightTrigger) _rightTrigger = value;
    }

    // Called once per frame by InputManager before entity/screen logic runs.
    internal void Update()
    {
        _previous = _current;
        if (_hasInjection)
            _current = new GamePadState(
                new GamePadThumbSticks(new Vector2(_leftStickX, _leftStickY), new Vector2(_rightStickX, _rightStickY)),
                new GamePadTriggers(_leftTrigger, _rightTrigger),
                new GamePadButtons(_injectedButtons),
                new GamePadDPad());
        else
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
