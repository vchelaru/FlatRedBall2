using System;
using System.Numerics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Input;

/// <summary>
/// Default <see cref="ICursor"/> implementation. Reports either the system mouse or the first
/// active touch, with touch taking precedence when present. Created and updated by
/// <see cref="InputManager"/> — game code should access the cursor via <c>Engine.Input.Cursor</c>
/// rather than constructing this directly.
/// </summary>
/// <remarks>
/// <see cref="WorldPosition"/> requires a <see cref="Camera"/> reference (injected by the engine via
/// <c>InputManager.SetCamera</c>); until one is set, world coordinates fall back to screen coordinates.
/// On platforms without a touch panel (most desktop runs), touch polling is permanently disabled
/// after the first failure and the cursor reports mouse state only.
/// </remarks>
public class Cursor : ICursor
{
    private MouseState _currentMouse;
    private MouseState _previousMouse;
    private Camera? _camera;

    private bool _touchActive;
    private bool _touchActivePrev;
    private Vector2 _touchScreenPos;
    private bool _touchAvailable = true;

    internal void SetCamera(Camera camera) => _camera = camera;

    // Called once per frame by InputManager before entity/screen logic runs.
    internal void Update()
    {
        _previousMouse = _currentMouse;
        _currentMouse = Mouse.GetState();

        _touchActivePrev = _touchActive;
        _touchActive = false;

        if (_touchAvailable)
            UpdateTouch();
    }

    private void UpdateTouch()
    {
        try
        {
            var touches = TouchPanel.GetState();
            if (touches.Count > 0)
            {
                var first = touches[0];
                if (first.State != TouchLocationState.Released)
                {
                    _touchActive = true;
                    _touchScreenPos = new Vector2(first.Position.X, first.Position.Y);
                }
            }
        }
        catch (NullReferenceException)
        {
            // TouchPanel requires an initialized GameWindow; permanently disable in this environment.
            _touchAvailable = false;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Returns the active touch position when a touch is in progress; otherwise the mouse position.</remarks>
    public Vector2 ScreenPosition => _touchActive
        ? _touchScreenPos
        : new Vector2(_currentMouse.X, _currentMouse.Y);

    /// <inheritdoc/>
    /// <remarks>
    /// Falls back to <see cref="ScreenPosition"/> if no <see cref="Camera"/> has been registered yet
    /// (e.g. very early in startup before the first screen is loaded).
    /// </remarks>
    public Vector2 WorldPosition => _camera != null
        ? _camera.ScreenToWorld(ScreenPosition)
        : ScreenPosition;

    /// <inheritdoc/>
    /// <remarks>True whenever a touch is active or the left mouse button is held.</remarks>
    public bool PrimaryDown => _touchActive
        ? true
        : _currentMouse.LeftButton == ButtonState.Pressed;

    /// <inheritdoc/>
    /// <remarks>
    /// True on the frame a touch begins or the frame the left mouse button transitions
    /// from up to down.
    /// </remarks>
    public bool PrimaryPressed => _touchActive
        ? !_touchActivePrev
        : _currentMouse.LeftButton == ButtonState.Pressed &&
          _previousMouse.LeftButton == ButtonState.Released;
}
