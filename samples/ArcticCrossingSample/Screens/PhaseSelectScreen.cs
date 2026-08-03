using ArcticCrossingSample.Data;
using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ArcticCrossingSample.Screens;

public class PhaseSelectScreen : Screen
{
    public GameState State { get; set; } = new();

    private int _selectedPhase = 1;
    private Label _descriptionLabel = null!;
    private readonly List<Button> _phaseButtons = new();

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 35, 65);

        var title = new Label();
        title.Text = "Select Phase";
        title.Anchor(Anchor.Top);
        title.Y = 30;
        Add(title);

        var buttonPanel = new StackPanel();
        buttonPanel.Orientation = Orientation.Horizontal;
        buttonPanel.Spacing = 16;
        buttonPanel.Anchor(Anchor.Center);
        buttonPanel.Y = -20;

        for (int i = 1; i <= PhaseDefinitions.TotalPhases; i++)
        {
            int phase = i; // capture for closure
            var btn = new Button();
            bool unlocked = phase <= State.HighestUnlockedPhase;
            string phaseName = PhaseDefinitions.GetPhase(phase).PhaseName;

            btn.Text = unlocked ? $"{phase}" : "?";

            if (unlocked)
            {
                btn.Click += (_, _) =>
                {
                    MoveToScreen<GameplayScreen>(s =>
                    {
                        s.PhaseIndex = phase;
                        s.State = State;
                    });
                };
            }

            buttonPanel.AddChild(btn);
            _phaseButtons.Add(btn);
        }

        Add(buttonPanel);

        _descriptionLabel = new Label();
        _descriptionLabel.Anchor(Anchor.Center);
        _descriptionLabel.Y = 60;
        Add(_descriptionLabel);

        // High scores
        var scoresPanel = new StackPanel();
        scoresPanel.Spacing = 4;
        scoresPanel.Anchor(Anchor.Center);
        scoresPanel.Y = 100;

        for (int i = 1; i <= PhaseDefinitions.TotalPhases; i++)
        {
            if (State.HighScores[i] > 0)
            {
                var scoreLabel = new Label();
                var phaseName = PhaseDefinitions.GetPhase(i).PhaseName;
                scoreLabel.Text = $"Phase {i} ({phaseName}): {State.HighScores[i]}";
                scoresPanel.AddChild(scoreLabel);
            }
        }

        Add(scoresPanel);

        var backLabel = new Label();
        backLabel.Text = "Press Escape to go back";
        backLabel.Anchor(Anchor.BottomLeft);
        backLabel.X = 20;
        backLabel.Y = -30;
        Add(backLabel);

        UpdateDescription();
    }

    private void UpdateDescription()
    {
        if (_selectedPhase <= State.HighestUnlockedPhase)
        {
            var data = PhaseDefinitions.GetPhase(_selectedPhase);
            _descriptionLabel.Text = $"Phase {_selectedPhase}: {data.PhaseName}";
        }
        else
        {
            _descriptionLabel.Text = "Locked";
        }
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Escape))
            MoveToScreen<TitleScreen>(s => s.State = State);
    }
}
