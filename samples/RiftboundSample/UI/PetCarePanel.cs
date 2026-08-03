using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Panel for feeding and training a pet. Opened from the overworld with P.
/// </summary>
public class PetCarePanel
{
    private const float BarMaxWidth = 120f;
    private const float BarHeight = 10f;

    private Screen _screen = null!;
    private Panel _root = null!;
    private Label _nameLabel = null!;
    private Label _tierLabel = null!;
    private ColoredRectangleRuntime _satietyBar = null!;
    private ColoredRectangleRuntime _trainingBar = null!;
    private ColoredRectangleRuntime _bondBar = null!;
    private Label _satietyLabel = null!;
    private Label _trainingLabel = null!;
    private Label _bondLabel = null!;

    private PetState? _pet;
    private PetCareSystem? _careSystem;
    private int _selectedButton;
    private readonly string[] _buttons = ["Feed", "Train", "Close"];

    private List<Label> _buttonLabels = [];

    public bool IsVisible => _root?.Visual.Visible ?? false;

    public event Action? Closed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Anchor(Anchor.Center);
        _root.Visual.Visible = false;

        var layout = new StackPanel { Spacing = 8 };

        _nameLabel = new Label { Text = "" };
        layout.AddChild(_nameLabel);

        // Satiety bar row
        _satietyBar = CreateBar(220, 160, 40);
        _satietyLabel = new Label { Text = "Satiety:" };
        var satietyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        satietyRow.AddChild(_satietyLabel);
        satietyRow.Visual.Children.Add(_satietyBar);
        layout.AddChild(satietyRow);

        // Training bar row
        _trainingBar = CreateBar(40, 140, 220);
        _trainingLabel = new Label { Text = "Training:" };
        var trainingRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        trainingRow.AddChild(_trainingLabel);
        trainingRow.Visual.Children.Add(_trainingBar);
        layout.AddChild(trainingRow);

        // Bond bar row
        _bondBar = CreateBar(200, 60, 180);
        _bondLabel = new Label { Text = "Bond:" };
        var bondRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        bondRow.AddChild(_bondLabel);
        bondRow.Visual.Children.Add(_bondBar);
        layout.AddChild(bondRow);

        _tierLabel = new Label { Text = "" };
        layout.AddChild(_tierLabel);

        // Button row
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        foreach (var name in _buttons)
        {
            var label = new Label { Text = name };
            buttonRow.AddChild(label);
            _buttonLabels.Add(label);
        }
        layout.AddChild(buttonRow);

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    public void Show(PetState pet, PetCareSystem careSystem)
    {
        _pet = pet;
        _careSystem = careSystem;
        _selectedButton = 0;
        _root.Visual.Visible = true;
        UpdateDisplay();
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
        _pet = null;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible || _pet == null) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Left))
        {
            _selectedButton = (_selectedButton - 1 + _buttons.Length) % _buttons.Length;
            UpdateButtonHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Right))
        {
            _selectedButton = (_selectedButton + 1) % _buttons.Length;
            UpdateButtonHighlight();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            switch (_buttons[_selectedButton])
            {
                case "Feed":
                    _careSystem?.Feed(_pet, "basic_food");
                    break;
                case "Train":
                    _careSystem?.Train(_pet);
                    break;
                case "Close":
                    Hide();
                    Closed?.Invoke();
                    return;
            }
            UpdateDisplay();
        }
        else if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
        }
    }

    private void UpdateDisplay()
    {
        if (_pet == null) return;

        _nameLabel.Text = $"{_pet.Name} ({_pet.OwnerId}'s Pet)";
        _tierLabel.Text = $"Tier: {_pet.CurrentTier}";

        _satietyBar.Width = _pet.Satiety / 100f * BarMaxWidth;
        _satietyLabel.Text = $"Satiety:  {_pet.Satiety:F0}/100";

        _trainingBar.Width = _pet.Training / 100f * BarMaxWidth;
        _trainingLabel.Text = $"Training: {_pet.Training:F0}/100";

        _bondBar.Width = _pet.Bond / 100f * BarMaxWidth;
        _bondLabel.Text = $"Bond:     {_pet.Bond:F0}/100";

        UpdateButtonHighlight();
    }

    private void UpdateButtonHighlight()
    {
        for (int i = 0; i < _buttonLabels.Count; i++)
        {
            string prefix = i == _selectedButton ? "> " : "  ";
            _buttonLabels[i].Text = $"{prefix}[{_buttons[i]}]";
        }
    }

    private static ColoredRectangleRuntime CreateBar(int r, int g, int b) => new()
    {
        Width = 0,
        Height = BarHeight,
        Red = r,
        Green = g,
        Blue = b,
    };
}
