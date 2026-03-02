using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SampleProject1.Screens;

namespace SampleProject1.Screens;

public class GameOverScreen : Screen
{
    /// <summary>Set by the caller via MoveToScreen configure before CustomInitialize runs.</summary>
    public int FinalScore { get; set; }

    /// <summary>True when the player cleared all levels.</summary>
    public bool Won { get; set; }

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(8, 8, 25);

        var panel = new StackPanel();
        panel.Spacing = 16;
        panel.Anchor(Anchor.Center);

        var titleLabel = new Label();
        titleLabel.Text = Won ? "You Win!" : "Game Over";
        panel.AddChild(titleLabel);

        var scoreLabel = new Label();
        scoreLabel.Text = $"Score: {FinalScore}";
        panel.AddChild(scoreLabel);

        var highScoreLabel = new Label();
        highScoreLabel.Text = $"Best: {GameScreen.HighScore}";
        panel.AddChild(highScoreLabel);

        var hintLabel = new Label();
        hintLabel.Text = "Press SPACE to play again";
        panel.AddChild(hintLabel);

        AddGum(panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.Space))
            MoveToScreen<GameScreen>();
    }
}
