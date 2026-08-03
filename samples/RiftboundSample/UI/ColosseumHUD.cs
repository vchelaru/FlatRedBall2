using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;

namespace RiftboundSample.UI;

/// <summary>
/// HUD for colosseum mode: shows wave number, total rewards, and between-wave choices.
/// </summary>
public class ColosseumHUD
{
    private Screen _screen = null!;
    private Label _waveLabel = null!;
    private Label _rewardsLabel = null!;

    // Between-wave panel
    private Panel _betweenWavePanel = null!;
    private int _selectedOption; // 0 = Continue, 1 = Leave
    private Label _continueLabel = null!;
    private Label _leaveLabel = null!;

    // Summary panel
    private Panel _summaryPanel = null!;
    private StackPanel _summaryList = null!;

    public bool IsBetweenWaveVisible => _betweenWavePanel?.Visual.Visible ?? false;
    public bool IsSummaryVisible => _summaryPanel?.Visual.Visible ?? false;

    public event Action? ContinueSelected;
    public event Action? LeaveSelected;
    public event Action? SummaryDismissed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _waveLabel = new Label { Text = "Wave: 1" };
        _waveLabel.Anchor(Anchor.TopLeft);
        _waveLabel.X = 8;
        _waveLabel.Y = 8;
        _screen.Add(_waveLabel);

        _rewardsLabel = new Label { Text = "XP: 0  Gold: 0" };
        _rewardsLabel.Anchor(Anchor.TopLeft);
        _rewardsLabel.X = 8;
        _rewardsLabel.Y = 22;
        _screen.Add(_rewardsLabel);

        // Between-wave panel
        _betweenWavePanel = new Panel();
        _betweenWavePanel.Anchor(Anchor.Center);
        _betweenWavePanel.Visual.Visible = false;

        var bwBg = new ColoredRectangleRuntime
        {
            Width = 200, Height = 80,
            Red = 15, Green = 15, Blue = 30, Alpha = 230,
        };
        _betweenWavePanel.Visual.Children.Add(bwBg);

        var bwLayout = new StackPanel { Spacing = 8 };
        bwLayout.X = 10;
        bwLayout.Y = 10;

        bwLayout.AddChild(new Label { Text = "Wave Complete!" });

        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        _continueLabel = new Label { Text = "> [Continue]" };
        _leaveLabel = new Label { Text = "  [Leave]" };
        buttonRow.AddChild(_continueLabel);
        buttonRow.AddChild(_leaveLabel);
        bwLayout.AddChild(buttonRow);

        _betweenWavePanel.AddChild(bwLayout);
        _screen.Add(_betweenWavePanel);

        // Summary panel
        _summaryPanel = new Panel();
        _summaryPanel.Dock(Dock.Fill);
        _summaryPanel.Visual.Visible = false;

        var sumBg = new ColoredRectangleRuntime
        {
            Width = 0, Height = 0,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 10, Green = 10, Blue = 20, Alpha = 240,
        };
        _summaryPanel.Visual.Children.Add(sumBg);

        _summaryList = new StackPanel { Spacing = 6 };
        _summaryList.X = 20;
        _summaryList.Y = 20;
        _summaryPanel.AddChild(_summaryList);

        _screen.Add(_summaryPanel);
    }

    public void UpdateWaveInfo(ColosseumState state)
    {
        _waveLabel.Text = $"Wave: {state.CurrentWave}";
        _rewardsLabel.Text = $"XP: {state.TotalXPEarned}  Gold: {state.TotalGoldEarned}";
    }

    public void ShowBetweenWave()
    {
        _selectedOption = 0;
        UpdateBetweenWaveHighlight();
        _betweenWavePanel.Visual.Visible = true;
    }

    public void HideBetweenWave()
    {
        _betweenWavePanel.Visual.Visible = false;
    }

    public void ShowSummary(ColosseumState state, bool defeated)
    {
        ClearSummary();
        _summaryList.AddChild(new Label { Text = defeated ? "Defeated!" : "Colosseum Results" });
        _summaryList.AddChild(new Label { Text = $"Highest Wave: {state.HighestWave}" });
        _summaryList.AddChild(new Label { Text = $"Total XP: {state.TotalXPEarned}" });
        _summaryList.AddChild(new Label { Text = $"Total Gold: {state.TotalGoldEarned}" });

        foreach (var (itemId, count) in state.ItemsEarned)
            _summaryList.AddChild(new Label { Text = $"  {itemId} x{count}" });

        _summaryList.AddChild(new Label { Text = "Press Enter to exit" });
        _summaryPanel.Visual.Visible = true;
    }

    public void UpdateBetweenWaveInput(FlatRedBallService engine)
    {
        if (!IsBetweenWaveVisible) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Left) || kb.WasKeyPressed(Keys.Right))
        {
            _selectedOption = _selectedOption == 0 ? 1 : 0;
            UpdateBetweenWaveHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            HideBetweenWave();
            if (_selectedOption == 0)
                ContinueSelected?.Invoke();
            else
                LeaveSelected?.Invoke();
        }
    }

    public void UpdateSummaryInput(FlatRedBallService engine)
    {
        if (!IsSummaryVisible) return;

        if (engine.InputManager.Keyboard.WasKeyPressed(Keys.Enter))
        {
            _summaryPanel.Visual.Visible = false;
            SummaryDismissed?.Invoke();
        }
    }

    private void UpdateBetweenWaveHighlight()
    {
        _continueLabel.Text = _selectedOption == 0 ? "> [Continue]" : "  [Continue]";
        _leaveLabel.Text = _selectedOption == 1 ? "> [Leave]" : "  [Leave]";
    }

    private void ClearSummary()
    {
        var children = _summaryList.Visual.Children;
        for (int i = children.Count - 1; i >= 0; i--)
            children.RemoveAt(i);
    }
}
