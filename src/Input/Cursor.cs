using System.Numerics;
using Microsoft.Xna.Framework.Input;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Input;

public class Cursor : ICursor
{
    private MouseState _current;
    private MouseState _previous;
    private Camera? _camera;

    internal void SetCamera(Camera camera) => _camera = camera;

    internal void Update()
    {
        _previous = _current;
        _current = Mouse.GetState();
    }

    public Vector2 ScreenPosition => new Vector2(_current.X, _current.Y);

    public Vector2 WorldPosition => _camera != null
        ? _camera.ScreenToWorld(ScreenPosition)
        : ScreenPosition;

    public bool PrimaryDown => _current.LeftButton == ButtonState.Pressed;
    public bool PrimaryPressed => _current.LeftButton == ButtonState.Pressed &&
                                   _previous.LeftButton == ButtonState.Released;
}
