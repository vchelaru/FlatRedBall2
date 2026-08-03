using ArcticCrossingSample.Data;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Screens;

public class VictoryScreen : Screen
{
    public GameState State { get; set; } = new();

    private readonly List<AxisAlignedRectangle> _confetti = new();

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(25, 50, 90);

        // Mountain at top
        var mountain = new AxisAlignedRectangle
        {
            Width = 300f, Height = 400f,
            IsVisible = true, IsFilled = true,
            Color = new XnaColor(80, 90, 120, 200),
            Y = 100f,
        };
        Add(mountain);

        // Snow cap
        var snowCap = new AxisAlignedRectangle
        {
            Width = 150f, Height = 80f,
            IsVisible = true, IsFilled = true,
            Color = new XnaColor(240, 240, 255, 220),
            Y = 270f,
        };
        Add(snowCap);

        // Confetti
        var confettiColors = new[]
        {
            new XnaColor(255, 100, 100, 230),
            new XnaColor(100, 255, 100, 230),
            new XnaColor(100, 100, 255, 230),
            new XnaColor(255, 255, 100, 230),
            new XnaColor(255, 150, 255, 230),
        };

        for (int i = 0; i < 30; i++)
        {
            var piece = new AxisAlignedRectangle
            {
                Width = Engine.Random.Between(4f, 10f),
                Height = Engine.Random.Between(4f, 10f),
                IsVisible = true,
                IsFilled = true,
                Color = confettiColors[i % confettiColors.Length],
                X = Engine.Random.Between(-500f, 500f),
                Y = Engine.Random.Between(-300f, 350f),
            };
            Add(piece);
            _confetti.Add(piece);
        }

        // UI
        var menu = new StackPanel();
        menu.Spacing = 16;
        menu.Anchor(Anchor.Center);

        var congrats = new Label();
        congrats.Text = "YOU REACHED THE SUMMIT!";
        menu.AddChild(congrats);

        int totalScore = 0;
        for (int i = 1; i <= PhaseDefinitions.TotalPhases; i++)
            totalScore += State.HighScores[i];

        var scoreLabel = new Label();
        scoreLabel.Text = $"Total Score: {totalScore}";
        menu.AddChild(scoreLabel);

        var menuBtn = new Button();
        menuBtn.Text = "Back to Menu";
        menuBtn.Click += (_, _) => MoveToScreen<TitleScreen>(s => s.State = State);
        menu.AddChild(menuBtn);

        var replayBtn = new Button();
        replayBtn.Text = "Play Again";
        replayBtn.Click += (_, _) =>
        {
            MoveToScreen<CharacterSelectScreen>(s => s.State = new GameState());
        };
        menu.AddChild(replayBtn);

        Add(menu);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Confetti drifts down
        foreach (var piece in _confetti)
        {
            piece.Y -= 30f * time.DeltaSeconds;
            piece.X += MathF.Sin(piece.Y * 0.02f) * 40f * time.DeltaSeconds;

            if (piece.Y < -360f)
            {
                piece.Y = 360f;
                piece.X = Engine.Random.Between(-500f, 500f);
            }
        }

        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Escape))
            MoveToScreen<TitleScreen>(s => s.State = State);
    }
}
