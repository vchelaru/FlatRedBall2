using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RiftboundSample.Models;
using RiftboundSample.Systems;
using RiftboundSample.UI;

namespace RiftboundSample.Screens;

public class TitleScreen : Screen
{
    private static readonly string[] MenuOptions = ["New Game", "Continue", "Options", "Quit"];
    private readonly List<Label> _menuLabels = [];
    private int _selectedIndex;

    // Sub-panels
    private SaveLoadPanel _saveLoadPanel = new();
    private OptionsPanel? _optionsPanel;
    private bool _showingSaveLoad;
    private bool _showingOptions;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(10, 10, 30);

        var layout = new StackPanel { Spacing = 16 };
        layout.Anchor(Anchor.Center);

        var titleLabel = new Label { Text = "RIFTBOUND" };
        layout.AddChild(titleLabel);

        var subtitleLabel = new Label { Text = "A tale of worlds torn apart" };
        layout.AddChild(subtitleLabel);

        // Spacer
        layout.AddChild(new Label { Text = "" });

        for (int i = 0; i < MenuOptions.Length; i++)
        {
            string prefix = i == 0 ? "> " : "  ";
            var label = new Label { Text = $"{prefix}{MenuOptions[i]}" };
            layout.AddChild(label);
            _menuLabels.Add(label);
        }

        Add(layout);

        // Save/Load panel for Continue
        _saveLoadPanel.Initialize(this);
        _saveLoadPanel.Closed += () => _showingSaveLoad = false;
        _saveLoadPanel.LoadRequested += OnLoadRequested;

        // Options panel
        _optionsPanel = new OptionsPanel();
        _optionsPanel.Initialize(this);
        _optionsPanel.Closed += () => _showingOptions = false;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_showingSaveLoad)
        {
            _saveLoadPanel.Update(Engine);
            return;
        }

        if (_showingOptions)
        {
            _optionsPanel!.Update(Engine);
            return;
        }

        var kb = Engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + MenuOptions.Length) % MenuOptions.Length;
            UpdateHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % MenuOptions.Length;
            UpdateHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            switch (MenuOptions[_selectedIndex])
            {
                case "New Game":
                    MoveToScreen<OverworldScreen>();
                    break;
                case "Continue":
                    _showingSaveLoad = true;
                    _saveLoadPanel.Show(saveMode: false);
                    break;
                case "Options":
                    _showingOptions = true;
                    _optionsPanel!.Show();
                    break;
                case "Quit":
                    Engine.Game.Exit();
                    break;
            }
        }
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < _menuLabels.Count; i++)
        {
            string prefix = i == _selectedIndex ? "> " : "  ";
            _menuLabels[i].Text = $"{prefix}{MenuOptions[i]}";
        }
    }

    private void OnLoadRequested(SaveData data)
    {
        _showingSaveLoad = false;
        MoveToScreen<OverworldScreen>(s =>
        {
            s.InitialMapId = data.CurrentMap;
            s.RestorePlayerX = data.PlayerX;
            s.RestorePlayerY = data.PlayerY;
        });
    }
}
