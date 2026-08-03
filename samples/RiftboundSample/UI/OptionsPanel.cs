using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;

namespace RiftboundSample.UI;

/// <summary>
/// Options panel for game settings: battle speed, text speed, show enemy ATB, auto-battle default.
/// </summary>
public class OptionsPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private readonly List<Label> _optionLabels = [];
    private int _selectedIndex;
    private GameSettings _settings = new();

    private static readonly string[] OptionNames = ["Battle Speed", "Text Speed", "Show Enemy ATB", "Auto Battle Default", "Back"];

    public bool IsVisible => _root?.Visual.Visible ?? false;
    public event Action? Closed;

    public void Initialize(Screen screen)
    {
        _screen = screen;
        _settings = GameSettings.Load();

        _root = new Panel();
        _root.Dock(Dock.Fill);
        _root.Visual.Visible = false;

        var bg = new ColoredRectangleRuntime
        {
            Width = 0,
            Height = 0,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 10,
            Green = 10,
            Blue = 30,
            Alpha = 240,
        };
        _root.Visual.Children.Add(bg);

        var layout = new StackPanel { Spacing = 8 };
        layout.Anchor(Anchor.Center);

        layout.AddChild(new Label { Text = "OPTIONS" });
        layout.AddChild(new Label { Text = "Up/Down: Select  Left/Right: Change  Esc: Back" });

        foreach (var name in OptionNames)
        {
            var label = new Label { Text = name };
            layout.AddChild(label);
            _optionLabels.Add(label);
        }

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    public void Show()
    {
        _settings = GameSettings.Load();
        _selectedIndex = 0;
        _root.Visual.Visible = true;
        RefreshLabels();
    }

    public void Hide()
    {
        _settings.Save();
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

        if (kb.WasKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + OptionNames.Length) % OptionNames.Length;
            RefreshLabels();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % OptionNames.Length;
            RefreshLabels();
        }
        else if (kb.WasKeyPressed(Keys.Left) || kb.WasKeyPressed(Keys.Right))
        {
            int dir = kb.WasKeyPressed(Keys.Right) ? 1 : -1;
            AdjustSetting(_selectedIndex, dir);
            RefreshLabels();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            if (OptionNames[_selectedIndex] == "Back")
            {
                Hide();
                Closed?.Invoke();
            }
        }
    }

    private void AdjustSetting(int index, int direction)
    {
        switch (OptionNames[index])
        {
            case "Battle Speed":
                float[] speeds = [1f, 2f, 4f];
                int si = Array.IndexOf(speeds, _settings.DefaultBattleSpeed);
                if (si < 0) si = 0;
                si = Math.Clamp(si + direction, 0, speeds.Length - 1);
                _settings.DefaultBattleSpeed = speeds[si];
                break;

            case "Text Speed":
                float[] textSpeeds = [0.5f, 1f, 2f];
                int ti = Array.IndexOf(textSpeeds, _settings.TextSpeed);
                if (ti < 0) ti = 1;
                ti = Math.Clamp(ti + direction, 0, textSpeeds.Length - 1);
                _settings.TextSpeed = textSpeeds[ti];
                break;

            case "Show Enemy ATB":
                _settings.ShowEnemyATB = !_settings.ShowEnemyATB;
                break;

            case "Auto Battle Default":
                _settings.AutoBattleDefault = !_settings.AutoBattleDefault;
                break;
        }
    }

    private void RefreshLabels()
    {
        for (int i = 0; i < _optionLabels.Count; i++)
        {
            string prefix = i == _selectedIndex ? "> " : "  ";
            string value = OptionNames[i] switch
            {
                "Battle Speed" => $": {_settings.DefaultBattleSpeed}x",
                "Text Speed" => $": {_settings.TextSpeed}x",
                "Show Enemy ATB" => $": {(_settings.ShowEnemyATB ? "On" : "Off")}",
                "Auto Battle Default" => $": {(_settings.AutoBattleDefault ? "On" : "Off")}",
                _ => "",
            };
            _optionLabels[i].Text = $"{prefix}{OptionNames[i]}{value}";
        }
    }
}
