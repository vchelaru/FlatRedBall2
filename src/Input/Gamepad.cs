using System.Collections.Generic;
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

    // The standard digital buttons — triggers/thumbstick axes are read via GetAxis, not queried
    // for press/release, so they're intentionally excluded here.
    private static readonly Buttons[] DigitalButtons =
    {
        Buttons.A, Buttons.B, Buttons.X, Buttons.Y,
        Buttons.LeftShoulder, Buttons.RightShoulder,
        Buttons.Back, Buttons.Start, Buttons.BigButton,
        Buttons.LeftStick, Buttons.RightStick,
        Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
    };

    // Buttons held at the moment SuppressHeldReleases() was called (e.g. a screen transition) —
    // each one's next release must not be reported, since that press belongs to whichever screen
    // was active when it started. Removed from this set the frame its release actually happens.
    private readonly HashSet<Buttons> _heldAtSuppression = new();
    private readonly HashSet<Buttons> _releaseSuppressedThisFrame = new();

    internal Gamepad(int index) => _index = index;

    /// <summary>
    /// Marks every digital button currently down so its next release is not reported through
    /// <see cref="WasButtonJustReleased"/>. Called by the engine on every screen transition; a
    /// button that was already up is unaffected, and the very next fresh press/release cycle
    /// after the suppressed release behaves normally again.
    /// </summary>
    internal void SuppressHeldReleases()
    {
        foreach (var button in DigitalButtons)
            if (_current.IsButtonDown(button))
                _heldAtSuppression.Add(button);
    }

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

        _releaseSuppressedThisFrame.Clear();
        if (_heldAtSuppression.Count > 0)
        {
            foreach (var button in DigitalButtons)
            {
                if (_heldAtSuppression.Contains(button) && _previous.IsButtonDown(button) && !_current.IsButtonDown(button))
                {
                    _releaseSuppressedThisFrame.Add(button);
                    _heldAtSuppression.Remove(button);
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool IsButtonDown(Buttons button) => _current.IsButtonDown(button);

    /// <inheritdoc/>
    public bool WasButtonJustPressed(Buttons button) => !_previous.IsButtonDown(button) && _current.IsButtonDown(button);

    /// <inheritdoc/>
    public bool WasButtonJustReleased(Buttons button) =>
        _previous.IsButtonDown(button) && !_current.IsButtonDown(button) && !_releaseSuppressedThisFrame.Contains(button);

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
