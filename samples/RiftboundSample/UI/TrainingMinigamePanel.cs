using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// UI overlay for the pet training minigame.
/// Call Initialize once, then Update each frame while the minigame is active.
/// </summary>
public class TrainingMinigamePanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private Label _titleLabel = null!;
    private Label _instructionLabel = null!;
    private Label _roundLabel = null!;
    private Label _resultLabel = null!;

    // Timing game visuals
    private ColoredRectangleRuntime _timingBar = null!;
    private ColoredRectangleRuntime _timingMarker = null!;
    private ColoredRectangleRuntime _timingTarget = null!;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Anchor(Anchor.Center);
        _root.Visual.Visible = false;

        var layout = new StackPanel { Spacing = 8 };

        _titleLabel = new Label { Text = "Training!" };
        layout.AddChild(_titleLabel);

        _instructionLabel = new Label { Text = "" };
        layout.AddChild(_instructionLabel);

        _roundLabel = new Label { Text = "" };
        layout.AddChild(_roundLabel);

        // Timing bar area
        _timingBar = new ColoredRectangleRuntime
        {
            Width = 200, Height = 20, Red = 60, Green = 60, Blue = 60
        };
        _timingTarget = new ColoredRectangleRuntime
        {
            Width = 40, Height = 20, Red = 60, Green = 180, Blue = 60
        };
        _timingMarker = new ColoredRectangleRuntime
        {
            Width = 4, Height = 24, Red = 255, Green = 255, Blue = 255
        };

        layout.Visual.Children.Add(_timingBar);
        layout.Visual.Children.Add(_timingTarget);
        layout.Visual.Children.Add(_timingMarker);

        _resultLabel = new Label { Text = "" };
        _resultLabel.Visual.Visible = false;
        layout.AddChild(_resultLabel);

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    public void Show(TrainingMinigame minigame)
    {
        _root.Visual.Visible = true;
        _resultLabel.Visual.Visible = false;

        _titleLabel.Text = minigame.Type switch
        {
            MinigameType.Timing => "Timing Training",
            MinigameType.Memory => "Memory Training",
            MinigameType.Reaction => "Reaction Training",
            _ => "Training"
        };

        _instructionLabel.Text = minigame.Type switch
        {
            MinigameType.Timing => "Press Enter when the marker is in the green zone!",
            MinigameType.Memory => "Watch the sequence, then repeat it with 1-4 keys!",
            MinigameType.Reaction => "Press Enter as fast as you can when prompted!",
            _ => ""
        };

        bool showTimingVisuals = minigame.Type == MinigameType.Timing;
        _timingBar.Visible = showTimingVisuals;
        _timingTarget.Visible = showTimingVisuals;
        _timingMarker.Visible = showTimingVisuals;
    }

    public void Update(TrainingMinigame minigame)
    {
        if (!_root.Visual.Visible) return;

        _roundLabel.Text = $"Round: {minigame.CurrentRound + 1}";

        if (minigame.Type == MinigameType.Timing)
        {
            _timingMarker.X = minigame.TimingMarkerPosition * 200f;
            _timingTarget.X = (minigame.TimingTargetCenter - minigame.TimingTargetHalfWidth) * 200f;
            _timingTarget.Width = minigame.TimingTargetHalfWidth * 2 * 200f;
        }
        else if (minigame.Type == MinigameType.Reaction)
        {
            _instructionLabel.Text = minigame.IsReactionPromptActive
                ? ">>> PRESS ENTER NOW! <<<"
                : "Wait for it...";
        }

        if (minigame.Phase == MinigamePhase.Complete)
        {
            _resultLabel.Visual.Visible = true;
            int pct = (int)(minigame.Score * 100);
            _resultLabel.Text = $"Score: {pct}%  Training +{minigame.TrainingReward:F0}";
        }
    }

    public void Hide()
    {
        if (_root != null)
            _root.Visual.Visible = false;
    }
}
