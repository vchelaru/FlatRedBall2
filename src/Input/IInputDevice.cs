namespace FlatRedBall2.Input;

public interface IInputDevice
{
    bool IsActionDown(string action);
    bool WasActionPressed(string action);
}
