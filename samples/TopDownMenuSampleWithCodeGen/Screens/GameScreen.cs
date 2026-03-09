using FlatRedBall2;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TopDownMenuSampleWithCodeGen.Screens;

public class GameScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 35, 20);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Escape))
            MoveToScreen<PauseMenuScreen>();
    }
}
