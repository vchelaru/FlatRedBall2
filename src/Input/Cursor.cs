using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Input;

/// <summary>
/// Default <see cref="ICursor"/> implementation. Reports either the system mouse or the first
/// active touch, with touch taking precedence when present. Created and updated by
/// <see cref="InputManager"/> — game code should access the cursor via <c>Engine.Input.Cursor</c>
/// rather than constructing this directly.
/// </summary>
/// <remarks>
/// <see cref="WorldPosition"/> requires <see cref="Camera"/>s to be registered (injected by the
/// engine via <c>InputManager.SetCameras</c>); until at least one is set, world coordinates fall
/// back to screen coordinates. With multiple cameras (split-screen), the cursor sticks to whichever
/// camera's viewport contains the cursor's pixel position; if the cursor enters a letterbox gap,
/// the previous active camera continues to be used.
/// On platforms without a touch panel (most desktop runs), touch polling is permanently disabled
/// after the first failure and the cursor reports mouse state only.
/// </remarks>
public class Cursor : ICursor
{
    private MouseState _currentMouse;
    private MouseState _previousMouse;
    private IReadOnlyList<Camera>? _cameras;
    private Camera? _activeCamera;

    private bool _touchActive;
    private bool _touchActivePrev;
    private Vector2 _touchScreenPos;
    private bool _touchAvailable = true;

    // Wall-clock timestamps of the last detected press/release per button.
    // null means "never seen" — guarantees the first transition cannot accidentally register as a
    // double. Using nullable instead of TimeSpan.MinValue because the latter overflows on subtraction.
    private TimeSpan? _lastPrimaryPressTime;
    private TimeSpan? _lastPrimaryClickTime;
    private TimeSpan? _lastSecondaryPressTime;
    private TimeSpan? _lastSecondaryClickTime;

    private bool _primaryDoublePressed;
    private bool _primaryDoubleClick;
    private bool _secondaryDoublePressed;
    private bool _secondaryDoubleClick;

    /// <inheritdoc/>
    public TimeSpan DoubleClickThreshold { get; set; } = TimeSpan.FromMilliseconds(250);

    internal void SetCameras(IReadOnlyList<Camera> cameras)
    {
        _cameras = cameras;
        _activeCamera = null;
    }

    // Called once per frame by InputManager before entity/screen logic runs.
    internal void Update(TimeSpan realTimeSinceStart) => Update(Mouse.GetState(), realTimeSinceStart);

    // Test seam: lets unit tests drive mouse-derived properties without a real GameWindow.
    internal void Update(MouseState mouseState, TimeSpan realTimeSinceStart)
    {
        _previousMouse = _currentMouse;
        _currentMouse = mouseState;

        _touchActivePrev = _touchActive;
        _touchActive = false;

        if (_touchAvailable)
            UpdateTouch();

        UpdateActiveCamera();
        UpdateDoubleClicks(realTimeSinceStart);
    }

    // Sticky pick: keep the previously-chosen camera when the cursor leaves all viewports
    // (e.g., a letterbox gap between split-screen viewports). On first registration with no
    // hit yet, default to _cameras[0] so a freshly-set-up cursor has a sensible camera.
    private void UpdateActiveCamera()
    {
        if (_cameras == null || _cameras.Count == 0)
        {
            _activeCamera = null;
            return;
        }

        var screen = ScreenPosition;
        int sx = (int)screen.X;
        int sy = (int)screen.Y;
        for (int i = 0; i < _cameras.Count; i++)
        {
            var vp = _cameras[i].Viewport;
            if (sx >= vp.X && sx < vp.X + vp.Width &&
                sy >= vp.Y && sy < vp.Y + vp.Height)
            {
                _activeCamera = _cameras[i];
                return;
            }
        }

        _activeCamera ??= _cameras[0];
    }

    private void UpdateDoubleClicks(TimeSpan now)
    {
        TimeSpan threshold = DoubleClickThreshold;

        _primaryDoublePressed = false;
        _primaryDoubleClick = false;
        _secondaryDoublePressed = false;
        _secondaryDoubleClick = false;

        if (PrimaryPressed)
        {
            if (_lastPrimaryPressTime is { } prev && now - prev <= threshold) _primaryDoublePressed = true;
            _lastPrimaryPressTime = now;
        }
        if (PrimaryClick)
        {
            if (_lastPrimaryClickTime is { } prev && now - prev <= threshold) _primaryDoubleClick = true;
            _lastPrimaryClickTime = now;
        }
        if (SecondaryPressed)
        {
            if (_lastSecondaryPressTime is { } prev && now - prev <= threshold) _secondaryDoublePressed = true;
            _lastSecondaryPressTime = now;
        }
        if (SecondaryClick)
        {
            if (_lastSecondaryClickTime is { } prev && now - prev <= threshold) _secondaryDoubleClick = true;
            _lastSecondaryClickTime = now;
        }
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
    /// (e.g. very early in startup before the first screen is loaded). With split-screen, picks
    /// whichever camera's viewport currently contains the cursor; sticky in letterbox gaps.
    /// Use <see cref="GetWorldPosition(Camera)"/> to project through a specific camera regardless
    /// of cursor location.
    /// </remarks>
    public Vector2 WorldPosition => _activeCamera != null
        ? GetWorldPosition(_activeCamera)
        : ScreenPosition;

    /// <inheritdoc/>
    public Vector2 GetWorldPosition(Camera camera)
    {
        var vp = camera.Viewport;
        var local = ScreenPosition - new Vector2(vp.X, vp.Y);
        return camera.ScreenToWorld(local);
    }

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

    /// <inheritdoc/>
    /// <remarks>
    /// True on the frame a touch ends, or the frame the left mouse button transitions from down
    /// to up. Mirrors <see cref="PrimaryPressed"/> on the release edge.
    /// </remarks>
    public bool PrimaryClick => _touchActive
        ? false
        : _touchActivePrev
            ? true
            : _currentMouse.LeftButton == ButtonState.Released &&
              _previousMouse.LeftButton == ButtonState.Pressed;

    /// <inheritdoc/>
    public bool PrimaryDoublePressed => _primaryDoublePressed;

    /// <inheritdoc/>
    public bool PrimaryDoubleClick => _primaryDoubleClick;

    /// <inheritdoc/>
    public bool SecondaryDown => _currentMouse.RightButton == ButtonState.Pressed;

    /// <inheritdoc/>
    public bool SecondaryPressed =>
        _currentMouse.RightButton == ButtonState.Pressed &&
        _previousMouse.RightButton == ButtonState.Released;

    /// <inheritdoc/>
    public bool SecondaryClick =>
        _currentMouse.RightButton == ButtonState.Released &&
        _previousMouse.RightButton == ButtonState.Pressed;

    /// <inheritdoc/>
    public bool SecondaryDoublePressed => _secondaryDoublePressed;

    /// <inheritdoc/>
    public bool SecondaryDoubleClick => _secondaryDoubleClick;

    /// <inheritdoc/>
    public bool IsOver(ICollidable shape) => shape.Contains(WorldPosition);

    /// <inheritdoc/>
    public bool IsOver(ICollidable shape, Camera camera) => shape.Contains(GetWorldPosition(camera));

    /// <inheritdoc/>
    public bool IsOver(Entity entity)
    {
        var world = WorldPosition;
        foreach (var leaf in Entity.GetLeafShapes(entity))
        {
            if (leaf.Contains(world)) return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public bool IsOver(Entity entity, Camera camera)
    {
        var world = GetWorldPosition(camera);
        foreach (var leaf in Entity.GetLeafShapes(entity))
        {
            if (leaf.Contains(world)) return true;
        }
        return false;
    }
}
