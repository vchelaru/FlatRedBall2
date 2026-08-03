using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Strikers1945Sample.Screens;

public class VictoryScreen : Screen
{
    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 10, 30);

        var title = new Label();
        title.Text = "CONGRATULATIONS!";
        title.Anchor(Anchor.TopLeft);
        title.X = 90;
        title.Y = 180;
        Add(title);

        var scoreLabel = new Label();
        scoreLabel.Text = $"Final Score: {GameplayScreen.LastScore}";
        scoreLabel.Anchor(Anchor.TopLeft);
        scoreLabel.X = 110;
        scoreLabel.Y = 280;
        Add(scoreLabel);

        var message = new Label();
        message.Text = "You have defeated the enemy forces!";
        message.Anchor(Anchor.TopLeft);
        message.X = 40;
        message.Y = 340;
        Add(message);

        var prompt = new Label();
        prompt.Text = "Press Z to return to title";
        prompt.Anchor(Anchor.TopLeft);
        prompt.X = 90;
        prompt.Y = 440;
        Add(prompt);
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.InputManager.Keyboard;
        if (kb.WasKeyPressed(Keys.Z) || kb.WasKeyPressed(Keys.Space))
        {
            MoveToScreen<TitleScreen>();
        }
    }

    public override void CustomDestroy() { }
}
