using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;

namespace ShmupSample.Screens;

public class GameOverScreen : Screen
{
    public int FinalScore { get; set; }
    public bool Won { get; set; }

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(8, 8, 18);

        var panel = new StackPanel();
        panel.Spacing = 20;
        panel.Anchor(Anchor.Center);

        var titleLabel = new Label();
        titleLabel.Text = Won ? "YOU WIN" : "GAME OVER";
        panel.AddChild(titleLabel);

        var scoreLabel = new Label();
        scoreLabel.Text = $"Score: {FinalScore}";
        panel.AddChild(scoreLabel);

        var restartLabel = new Label();
        restartLabel.Text = "Press Space or Z to restart";
        panel.AddChild(restartLabel);

        Add(panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.Input.Keyboard;
        if (kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Space) ||
            kb.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Z))
        {
            MoveToScreen<GameplayScreen>();
        }
    }

    public override void CustomDestroy() { }
}
