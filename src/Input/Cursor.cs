using System;
using System.Numerics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Input;

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

    public Vector2 ScreenPosition => _touchActive
        ? _touchScreenPos
        : new Vector2(_currentMouse.X, _currentMouse.Y);

    public Vector2 WorldPosition => _camera != null
        ? _camera.ScreenToWorld(ScreenPosition)
        : ScreenPosition;

    public bool PrimaryDown => _touchActive
        ? true
        : _currentMouse.LeftButton == ButtonState.Pressed;

    public bool PrimaryPressed => _touchActive
        ? !_touchActivePrev
        : _currentMouse.LeftButton == ButtonState.Pressed &&
          _previousMouse.LeftButton == ButtonState.Released;
}
