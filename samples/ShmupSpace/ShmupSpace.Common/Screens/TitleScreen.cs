using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ShmupSpace.Screens;

public class TitleScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(8, 8, 24);

        var panel = new StackPanel();
        panel.Spacing = 12;
        panel.Anchor(Anchor.Center);

        var title = new Label { Text = "SHMUP SPACE" };
        var prompt = new Label { Text = "Press Space or Tap to Start" };
        panel.AddChild(title);
        panel.AddChild(prompt);

        Add(panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.Input.Keyboard;
        if (kb.WasKeyPressed(Keys.Space) || kb.WasKeyPressed(Keys.Enter) ||
            Engine.Input.Cursor.PrimaryPressed)
            MoveToScreen<GameScreen>();
    }
}
