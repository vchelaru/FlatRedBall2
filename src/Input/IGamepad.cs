using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

public interface IGamepad
{
    bool IsButtonDown(Buttons button);
    float GetAxis(GamepadAxis axis);
}
