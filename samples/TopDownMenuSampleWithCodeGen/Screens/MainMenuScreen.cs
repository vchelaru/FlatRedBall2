using FlatRedBall2;
using Microsoft.Xna.Framework;

namespace TopDownMenuSampleWithCodeGen.Screens;

public class MainMenuScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 15, 40);

        var ui = new MainMenuScreenGum();
        Add(ui);

        ui.StartGameButton.Click += (_, _) => MoveToScreen<GameScreen>();
        ui.ExitButton.Click += (_, _) => Engine.Game.Exit();
    }

    public override void CustomActivity(FrameTime time) { }
}
