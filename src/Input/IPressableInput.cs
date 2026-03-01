namespace FlatRedBall2.Input;

public interface IPressableInput
{
    bool IsDown { get; }
    bool WasJustPressed { get; }
    bool WasJustReleased { get; }
}
