using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;

namespace ZeldaRoomsSample.Screens;

public class GameOverScreen : Screen
{
    public bool Win { get; set; } = false;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = Color.Black;

        var panel = new StackPanel { Spacing = 20 };
        panel.Anchor(Anchor.Center);

        var title = new Label { Text = Win ? "YOU WIN!" : "GAME OVER" };
        var subtitle = new Label { Text = "Press Space to restart" };

        panel.AddChild(title);
        panel.AddChild(subtitle);
        Add(panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Space))
            MoveToScreen<GameplayScreen>(s => s.RoomIndex = 0);
    }
}
