using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SpaceInvadersSample.Screens;

public class GameOverScreen : Screen
{
    public int FinalScore { get; set; }
    public bool Won { get; set; }

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(5, 5, 15);

        var panel = new StackPanel();
        panel.Spacing = 16;
        panel.Anchor(Anchor.Center);

        var titleLabel = new Label();
        titleLabel.Text = Won ? "You Win!" : "Game Over";
        panel.AddChild(titleLabel);

        var scoreLabel = new Label();
        scoreLabel.Text = $"Score: {FinalScore}";
        panel.AddChild(scoreLabel);

        var hintLabel = new Label();
        hintLabel.Text = "Press SPACE to play again";
        panel.AddChild(hintLabel);

        AddGum(panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.Space))
            MoveToScreen<SpaceInvadersScreen>();
    }
}
