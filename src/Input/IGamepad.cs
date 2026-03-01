using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

public interface IGamepad
{
    bool IsButtonDown(Buttons button);
    bool WasButtonJustPressed(Buttons button);
    bool WasButtonJustReleased(Buttons button);
    float GetAxis(GamepadAxis axis);
}
