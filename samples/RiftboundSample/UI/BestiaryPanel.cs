using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Full-screen bestiary overlay. Left panel lists encountered enemies;
/// right panel shows progressive info unlocked by defeat count.
/// </summary>
public class BestiaryPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;

    private StackPanel _enemyList = null!;
    private readonly List<Label> _enemyLabels = [];

    // Right side detail
    private Label _detailName = null!;
    private ColoredRectangleRuntime _preview = null!;
    private Label _defeatedLabel = null!;
    private StackPanel _statsPanel = null!;
    private StackPanel _dropsPanel = null!;

    // Rewards section
    private Label _completionLabel = null!;
    private StackPanel _rewardsPanel = null!;

    private List<BestiaryEntry> _entries = [];
    private BestiarySystem _bestiary = null!;
    private int _selectedIndex;

    public bool IsVisible => _root?.Visual.Visible ?? false;
    public event Action? Closed;
    public event Action<int>? RewardClaimed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Dock(Dock.Fill);
        _root.Visual.Visible = false;

        // Background
        var bg = new ColoredRectangleRuntime
        {
            Width = 0, Height = 0,
            WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
            HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
            Red = 15, Green = 20, Blue = 35, Alpha = 230,
        };
        _root.Visual.Children.Add(bg);

        var title = new Label { Text = "-- Bestiary --" };
        title.Anchor(Anchor.Top);
        title.Y = 8;
        _root.AddChild(title);

        var mainRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        mainRow.Anchor(Anchor.TopLeft);
        mainRow.X = 16;
        mainRow.Y = 32;

        // Left: enemy list
        _enemyList = new StackPanel { Spacing = 2 };
        mainRow.AddChild(_enemyList);

        // Right: detail
        var rightPanel = new StackPanel { Spacing = 6 };

        _detailName = new Label { Text = "" };
        rightPanel.AddChild(_detailName);

        _preview = new ColoredRectangleRuntime
        {
            Width = 48, Height = 48,
            Red = 180, Green = 60, Blue = 60,
        };
        rightPanel.Visual.Children.Add(_preview);

        _defeatedLabel = new Label { Text = "" };
        rightPanel.AddChild(_defeatedLabel);

        var statsHeader = new Label { Text = "Stats (defeat 3+ to reveal):" };
        rightPanel.AddChild(statsHeader);

        _statsPanel = new StackPanel { Spacing = 2 };
        rightPanel.AddChild(_statsPanel);

        var dropsHeader = new Label { Text = "Drops (defeat 10+ to reveal):" };
        rightPanel.AddChild(dropsHeader);

        _dropsPanel = new StackPanel { Spacing = 2 };
        rightPanel.AddChild(_dropsPanel);

        mainRow.AddChild(rightPanel);
        _root.AddChild(mainRow);

        // Completion and rewards
        _completionLabel = new Label { Text = "" };
        _completionLabel.Anchor(Anchor.TopRight);
        _completionLabel.X = -16;
        _completionLabel.Y = 32;
        _root.AddChild(_completionLabel);

        _rewardsPanel = new StackPanel { Spacing = 2 };
        _rewardsPanel.Anchor(Anchor.TopRight);
        _rewardsPanel.X = -16;
        _rewardsPanel.Y = 48;
        _root.AddChild(_rewardsPanel);

        var hint = new Label { Text = "Up/Down: select  C: claim reward  Esc: close" };
        hint.Anchor(Anchor.BottomLeft);
        hint.X = 16;
        hint.Y = -8;
        _root.AddChild(hint);

        _screen.Add(_root);
    }

    public void Show(BestiarySystem bestiary)
    {
        _bestiary = bestiary;
        _entries = bestiary.GetEntries();
        _selectedIndex = 0;
        RebuildEnemyList();
        UpdateDetail();
        UpdateRewardsDisplay();
        _root.Visual.Visible = true;
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
            return;
        }

        // Claim reward
        if (kb.WasKeyPressed(Keys.C))
        {
            TryClaimReward();
            return;
        }

        if (_entries.Count == 0) return;

        if (kb.WasKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + _entries.Count) % _entries.Count;
            UpdateEnemyHighlight();
            UpdateDetail();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % _entries.Count;
            UpdateEnemyHighlight();
            UpdateDetail();
        }
    }

    private void RebuildEnemyList()
    {
        foreach (var label in _enemyLabels)
            _enemyList.Visual.Children.Remove(label.Visual);
        _enemyLabels.Clear();

        for (int i = 0; i < _entries.Count; i++)
        {
            string prefix = i == _selectedIndex ? "> " : "  ";
            var label = new Label { Text = $"{prefix}{_entries[i].Name}" };
            _enemyList.AddChild(label);
            _enemyLabels.Add(label);
        }
    }

    private void UpdateEnemyHighlight()
    {
        for (int i = 0; i < _enemyLabels.Count && i < _entries.Count; i++)
        {
            string prefix = i == _selectedIndex ? "> " : "  ";
            _enemyLabels[i].Text = $"{prefix}{_entries[i].Name}";
        }
    }

    private void UpdateDetail()
    {
        ClearPanel(_statsPanel);
        ClearPanel(_dropsPanel);

        if (_entries.Count == 0)
        {
            _detailName.Text = "No enemies encountered yet.";
            _defeatedLabel.Text = "";
            return;
        }

        var entry = _entries[_selectedIndex];
        _detailName.Text = entry.Name;
        _defeatedLabel.Text = $"Defeated: {entry.TimesDefeated}";

        var data = _bestiary.GetEnemyData(entry.EnemyId);
        if (data == null) return;

        // Color the preview rectangle based on boss status
        _preview.Red = data.IsBoss ? 200 : 120;
        _preview.Green = data.IsBoss ? 50 : 100;
        _preview.Blue = data.IsBoss ? 50 : 160;

        if (entry.StatsRevealed)
        {
            AddStatLabel($"HP: {data.HP}  MP: {data.MP}");
            AddStatLabel($"STR: {data.STR}  MAG: {data.MAG}");
            AddStatLabel($"DEF: {data.DEF}  RES: {data.RES}");
            AddStatLabel($"SPD: {data.SPD}");
        }
        else
        {
            AddStatLabel("???");
        }

        if (entry.DropsRevealed)
        {
            foreach (var drop in data.DropTable)
            {
                var label = new Label { Text = $"  {drop.ItemId} ({drop.Rate * 100:F0}%)" };
                _dropsPanel.AddChild(label);
            }
        }
        else
        {
            var label = new Label { Text = "???" };
            _dropsPanel.AddChild(label);
        }
    }

    private void AddStatLabel(string text)
    {
        _statsPanel.AddChild(new Label { Text = $"  {text}" });
    }

    private void UpdateRewardsDisplay()
    {
        ClearPanel(_rewardsPanel);

        int total = _bestiary.TotalEnemyCount;
        int encountered = _bestiary.EncounteredCount;
        float pct = total > 0 ? encountered / (float)total * 100 : 0;
        _completionLabel.Text = $"Completion: {pct:F0}% ({encountered}/{total})";

        var allRewards = _bestiary.GetAllRewards();
        for (int i = 0; i < allRewards.Count; i++)
        {
            var reward = allRewards[i];
            bool claimed = _bestiary.ClaimedRewards.Contains(i);
            bool available = encountered >= reward.RequiredEntries;
            string status = claimed ? "[Claimed]" : (available ? "[Available]" : "[Locked]");
            var label = new Label { Text = $"  {status} {reward.Description}" };
            _rewardsPanel.AddChild(label);
        }
    }

    private void TryClaimReward()
    {
        var unclaimed = _bestiary.CheckRewards();
        if (unclaimed.Count > 0)
        {
            var (index, reward) = unclaimed[0];
            if (_bestiary.ClaimReward(index))
            {
                RewardClaimed?.Invoke(index);
                UpdateRewardsDisplay();
            }
        }
    }

    private static void ClearPanel(StackPanel panel)
    {
        var children = panel.Visual.Children;
        for (int i = children.Count - 1; i >= 0; i--)
            children.RemoveAt(i);
    }
}
