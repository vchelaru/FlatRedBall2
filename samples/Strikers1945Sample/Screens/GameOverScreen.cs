using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Strikers1945Sample.Screens;

public class GameOverScreen : Screen
{
    public int FinalScore { get; set; }
    public int LevelReached { get; set; }

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 10, 20);

        // Screen is 480 wide — center labels around x=170-190 area

        var title = new Label();
        title.Text = "GAME OVER";
        title.Anchor(Anchor.TopLeft);
        title.X = 170;
        title.Y = 180;
        Add(title);

        var scoreLabel = new Label();
        scoreLabel.Text = $"Final Score: {FinalScore:N0}";
        scoreLabel.Anchor(Anchor.TopLeft);
        scoreLabel.X = 130;
        scoreLabel.Y = 260;
        Add(scoreLabel);

        var levelLabel = new Label();
        levelLabel.Text = $"Level Reached: {LevelReached}";
        levelLabel.Anchor(Anchor.TopLeft);
        levelLabel.X = 130;
        levelLabel.Y = 290;
        Add(levelLabel);

        var enemiesLabel = new Label();
        enemiesLabel.Text = $"Enemies Defeated: {GameplayScreen.EnemiesDefeated}";
        enemiesLabel.Anchor(Anchor.TopLeft);
        enemiesLabel.X = 130;
        enemiesLabel.Y = 320;
        Add(enemiesLabel);

        var chainLabel = new Label();
        chainLabel.Text = $"Highest Chain: {GameplayScreen.HighestChain}";
        chainLabel.Anchor(Anchor.TopLeft);
        chainLabel.X = 130;
        chainLabel.Y = 350;
        Add(chainLabel);

        var restart = new Label();
        restart.Text = "Press Z to Restart";
        restart.Anchor(Anchor.TopLeft);
        restart.X = 140;
        restart.Y = 430;
        Add(restart);
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.InputManager.Keyboard;
        if (kb.WasKeyPressed(Keys.Z) || kb.WasKeyPressed(Keys.Space))
        {
            MoveToScreen<GameplayScreen>();
        }
    }

    public override void CustomDestroy() { }
}
