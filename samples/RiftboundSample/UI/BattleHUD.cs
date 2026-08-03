using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;

namespace RiftboundSample.UI;

/// <summary>
/// Manages all battle HUD elements: party status bars, enemy ATB gauges, and speed indicator.
/// Uses a compact layout designed for 1280x720 resolution.
/// </summary>
public class BattleHUD
{
    private const float BarWidth = 140f;
    private const float BarHeight = 10f;
    private const float SmallBarHeight = 6f;
    private const float RowSpacing = 14f;

    private Screen _screen = null!;
    private Label _speedLabel = null!;

    private readonly List<PartyBarSet> _partyBars = [];
    private readonly List<EnemyBarSet> _enemyBars = [];

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _speedLabel = new Label { Text = "1x" };
        _speedLabel.Anchor(Anchor.BottomRight);
        _speedLabel.X = -20;
        _speedLabel.Y = -20;
        _screen.Add(_speedLabel);
    }

    public void BuildPartyBars(List<CombatantState> party)
    {
        _partyBars.Clear();

        // Each member gets: Name row, HP bar, MP bar, ATB bar = ~4 rows * RowSpacing
        float memberHeight = 4 * RowSpacing + 8;
        float totalHeight = party.Count * memberHeight + 10;

        var root = new ContainerRuntime();
        root.X = 10;
        root.Y = 720 - totalHeight - 10;
        root.WidthUnits = DimensionUnitType.Absolute;
        root.Width = 350;
        root.HeightUnits = DimensionUnitType.Absolute;
        root.Height = totalHeight;

        for (int i = 0; i < party.Count; i++)
        {
            var member = party[i];
            float baseY = i * memberHeight;
            var bars = new PartyBarSet();

            // Name
            bars.NameLabel = MakeText(member.Name, 0, baseY);
            root.Children.Add(bars.NameLabel);

            // HP bar row
            float hpY = baseY + RowSpacing;
            bars.HPLabel = MakeText("HP", 0, hpY);
            root.Children.Add(bars.HPLabel);
            bars.HPBarBg = MakeBar(30, BarWidth, BarHeight, 40, 40, 40);
            bars.HPBarBg.Y = hpY + 2;
            root.Children.Add(bars.HPBarBg);
            bars.HPBar = MakeBar(30, 0, BarHeight, 80, 220, 80);
            bars.HPBar.Y = hpY + 2;
            root.Children.Add(bars.HPBar);
            bars.HPText = MakeText($"{member.CurrentHP}/{member.MaxHP}", 30 + BarWidth + 4, hpY);
            root.Children.Add(bars.HPText);

            // MP bar row
            float mpY = hpY + RowSpacing;
            bars.MPLabel = MakeText("MP", 0, mpY);
            root.Children.Add(bars.MPLabel);
            bars.MPBarBg = MakeBar(30, BarWidth, BarHeight, 40, 40, 40);
            bars.MPBarBg.Y = mpY + 2;
            root.Children.Add(bars.MPBarBg);
            bars.MPBar = MakeBar(30, 0, BarHeight, 80, 120, 220);
            bars.MPBar.Y = mpY + 2;
            root.Children.Add(bars.MPBar);
            bars.MPText = MakeText($"{member.CurrentMP}/{member.MaxMP}", 30 + BarWidth + 4, mpY);
            root.Children.Add(bars.MPText);

            // ATB + Pet + Limit on one row (thin bars side by side)
            float gaugeY = mpY + RowSpacing;
            bars.ATBBarBg = MakeBar(0, 80, SmallBarHeight, 40, 40, 40);
            bars.ATBBarBg.Y = gaugeY + 2;
            root.Children.Add(bars.ATBBarBg);
            bars.ATBBar = MakeBar(0, 0, SmallBarHeight, 220, 180, 40);
            bars.ATBBar.Y = gaugeY + 2;
            root.Children.Add(bars.ATBBar);

            bars.PetBarBg = MakeBar(90, 40, SmallBarHeight, 40, 40, 40);
            bars.PetBarBg.Y = gaugeY + 2;
            root.Children.Add(bars.PetBarBg);
            bars.PetBar = MakeBar(90, 0, SmallBarHeight, 180, 100, 220);
            bars.PetBar.Y = gaugeY + 2;
            root.Children.Add(bars.PetBar);

            bars.LimitBarBg = MakeBar(140, 40, SmallBarHeight, 40, 40, 40);
            bars.LimitBarBg.Y = gaugeY + 2;
            root.Children.Add(bars.LimitBarBg);
            bars.LimitBar = MakeBar(140, 0, SmallBarHeight, 220, 200, 40);
            bars.LimitBar.Y = gaugeY + 2;
            root.Children.Add(bars.LimitBar);

            _partyBars.Add(bars);
        }

        _screen.Add(root);
    }

    public void BuildEnemyBars(List<CombatantState> enemies)
    {
        _enemyBars.Clear();

        var root = new ContainerRuntime();
        root.X = 1280 - 220 - 10;
        root.Y = 10;
        root.WidthUnits = DimensionUnitType.Absolute;
        root.Width = 220;
        root.HeightUnits = DimensionUnitType.Absolute;
        root.Height = enemies.Count * 24 + 10;

        for (int i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            float baseY = i * 24f;
            var bars = new EnemyBarSet();

            bars.NameLabel = MakeText(enemy.Name, 0, baseY);
            root.Children.Add(bars.NameLabel);

            bars.ATBBarBg = MakeBar(110, 80, SmallBarHeight, 40, 40, 40);
            bars.ATBBarBg.Y = baseY + 4;
            root.Children.Add(bars.ATBBarBg);
            bars.ATBBar = MakeBar(110, 0, SmallBarHeight, 200, 60, 60);
            bars.ATBBar.Y = baseY + 4;
            root.Children.Add(bars.ATBBar);

            _enemyBars.Add(bars);
        }

        _screen.Add(root);
    }

    private float _limitPulseTimer;

    public void UpdateBars(List<CombatantState> party, float deltaSeconds = 0f)
    {
        _limitPulseTimer += deltaSeconds;

        for (int i = 0; i < party.Count && i < _partyBars.Count; i++)
        {
            var state = party[i];
            var bars = _partyBars[i];

            float hpPct = state.MaxHP > 0 ? (float)state.CurrentHP / state.MaxHP : 0f;
            float mpPct = state.MaxMP > 0 ? (float)state.CurrentMP / state.MaxMP : 0f;

            bars.HPBar.Width = hpPct * BarWidth;
            bars.MPBar.Width = mpPct * BarWidth;
            bars.ATBBar.Width = state.ATB * 80f;
            bars.PetBar.Width = state.PetGauge * 40f;

            bars.HPText.Text = $"{state.CurrentHP}/{state.MaxHP}";
            bars.MPText.Text = $"{state.CurrentMP}/{state.MaxMP}";

            bars.LimitBar.Width = state.LimitGauge * 40f;
            if (state.LimitGauge >= 1f)
            {
                bool visible = ((int)(_limitPulseTimer / 0.3f)) % 2 == 0;
                bars.LimitBar.Visible = visible;
            }
            else
            {
                bars.LimitBar.Visible = true;
            }
        }
    }

    public void ShowEnemyATB(List<CombatantState> enemies)
    {
        for (int i = 0; i < enemies.Count && i < _enemyBars.Count; i++)
        {
            var state = enemies[i];
            var bars = _enemyBars[i];
            bars.ATBBar.Width = state.ATB * 80f;
            bars.NameLabel.Text = state.IsAlive ? state.Name : $"{state.Name} (dead)";
        }
    }

    public void ShowSpeedIndicator(float multiplier)
    {
        _speedLabel.Text = multiplier switch
        {
            >= 4f => "4x",
            >= 2f => "2x",
            _ => "1x",
        };
    }

    private static ColoredRectangleRuntime MakeBar(float x, float width, float height, int r, int g, int b)
    {
        return new ColoredRectangleRuntime
        {
            X = x,
            Width = width,
            Height = height,
            Red = r,
            Green = g,
            Blue = b,
        };
    }

    private static TextRuntime MakeText(string text, float x, float y)
    {
        return new TextRuntime
        {
            Text = text,
            X = x,
            Y = y,
            Width = 200,
            Height = 16,
            Red = 255,
            Green = 255,
            Blue = 255,
            FontSize = 12,
        };
    }

    private class PartyBarSet
    {
        public TextRuntime NameLabel { get; set; } = null!;
        public ColoredRectangleRuntime HPBarBg { get; set; } = null!;
        public ColoredRectangleRuntime HPBar { get; set; } = null!;
        public TextRuntime HPLabel { get; set; } = null!;
        public TextRuntime HPText { get; set; } = null!;
        public ColoredRectangleRuntime MPBarBg { get; set; } = null!;
        public ColoredRectangleRuntime MPBar { get; set; } = null!;
        public TextRuntime MPLabel { get; set; } = null!;
        public TextRuntime MPText { get; set; } = null!;
        public ColoredRectangleRuntime ATBBarBg { get; set; } = null!;
        public ColoredRectangleRuntime ATBBar { get; set; } = null!;
        public ColoredRectangleRuntime PetBarBg { get; set; } = null!;
        public ColoredRectangleRuntime PetBar { get; set; } = null!;
        public ColoredRectangleRuntime LimitBarBg { get; set; } = null!;
        public ColoredRectangleRuntime LimitBar { get; set; } = null!;
    }

    private class EnemyBarSet
    {
        public TextRuntime NameLabel { get; set; } = null!;
        public ColoredRectangleRuntime ATBBarBg { get; set; } = null!;
        public ColoredRectangleRuntime ATBBar { get; set; } = null!;
    }
}
