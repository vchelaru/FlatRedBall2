using System.Numerics;

namespace FlatRedBall2.Input;

public interface ICursor
{
    Vector2 WorldPosition { get; }
    Vector2 ScreenPosition { get; }
    bool PrimaryDown { get; }
    bool PrimaryPressed { get; }
}
