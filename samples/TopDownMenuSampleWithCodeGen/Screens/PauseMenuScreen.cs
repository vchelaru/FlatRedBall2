using FlatRedBall2;
using Microsoft.Xna.Framework;

namespace TopDownMenuSampleWithCodeGen.Screens;

public class PauseMenuScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 35, 20);

        var ui = new PauseMenuScreenGum();
        Add(ui);

        ui.ResumeButton.Click += (_, _) => MoveToScreen<GameScreen>();
        ui.ExitToMenuButton.Click += (_, _) => MoveToScreen<MainMenuScreen>();
    }

    public override void CustomActivity(FrameTime time) { }
}
