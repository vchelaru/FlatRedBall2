using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;

namespace RiftboundSample.UI;

/// <summary>
/// Overworld pause menu with Resume, Save, Load, Quit options.
/// </summary>
public class PauseMenu
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private StackPanel _optionList = null!;
    private List<Label> _optionLabels = [];
    private int _selectedIndex;

    private static readonly string[] Options = ["Resume", "Save", "Load", "Text Log", "Cutscenes", "Quit"];

    public bool IsVisible => _root?.Visual.Visible ?? false;

    public event Action? ResumeSelected;
    public event Action? SaveSelected;
    public event Action? LoadSelected;
    public event Action? TextLogSelected;
    public event Action? CutscenesSelected;
    public event Action? QuitSelected;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Dock(Dock.Fill);
        _root.Visual.Visible = false;

        var bg = new ColoredRectangleRuntime
        {
            Width = 0,
            Height = 0,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 0,
            Green = 0,
            Blue = 0,
            Alpha = 180,
        };
        _root.Visual.Children.Add(bg);

        _optionList = new StackPanel { Spacing = 8 };
        _optionList.Anchor(Anchor.Center);

        var titleLabel = new Label { Text = "PAUSED" };
        _optionList.AddChild(titleLabel);

        for (int i = 0; i < Options.Length; i++)
        {
            string prefix = i == 0 ? "> " : "  ";
            var label = new Label { Text = $"{prefix}{Options[i]}" };
            _optionList.AddChild(label);
            _optionLabels.Add(label);
        }

        _root.AddChild(_optionList);
        _screen.Add(_root);
    }

    public void Show()
    {
        _selectedIndex = 0;
        _root.Visual.Visible = true;
        UpdateHighlight();
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
            ResumeSelected?.Invoke();
            return;
        }

        if (kb.WasKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + Options.Length) % Options.Length;
            UpdateHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % Options.Length;
            UpdateHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            Hide();
            switch (Options[_selectedIndex])
            {
                case "Resume": ResumeSelected?.Invoke(); break;
                case "Save": SaveSelected?.Invoke(); break;
                case "Load": LoadSelected?.Invoke(); break;
                case "Text Log": TextLogSelected?.Invoke(); break;
                case "Cutscenes": CutscenesSelected?.Invoke(); break;
                case "Quit": QuitSelected?.Invoke(); break;
            }
        }
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < _optionLabels.Count; i++)
        {
            string prefix = i == _selectedIndex ? "> " : "  ";
            _optionLabels[i].Text = $"{prefix}{Options[i]}";
        }
    }
}
