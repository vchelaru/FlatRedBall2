using FlatRedBall2;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace PongGravity.Screens;

public class WinScreen : Screen
{
    public static int Winner { get; set; } = 1;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new XnaColor(8, 8, 20);

        var ui = new WinScreenGum();
        ui.WinnerLabel.Text = $"Player {Winner} Wins!";
        Add(ui);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Space) ||
            Engine.Input.Keyboard.WasKeyPressed(Keys.Enter))
        {
            MoveToScreen<GameScreen>();
        }
    }
}
