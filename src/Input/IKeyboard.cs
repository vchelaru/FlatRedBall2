using Microsoft.Xna.Framework.Input;

namespace FlatRedBall2.Input;

public interface IKeyboard
{
    bool IsKeyDown(Keys key);
    bool WasKeyPressed(Keys key);
}
