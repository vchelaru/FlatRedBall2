using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Bottom-screen dialogue box with speaker name, text, and branching choices.
/// </summary>
public class DialogueBox
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private Label _speakerLabel = null!;
    private Label _textLabel = null!;
    private StackPanel _choiceList = null!;
    private Label _advanceHint = null!;

    private DialogueSystem? _dialogue;
    private List<Label> _choiceLabels = [];
    private int _selectedChoiceIndex;

    public bool IsVisible => _root?.Visual.Visible ?? false;

    public event Action? DialogueEnded;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Visual.Visible = false;
        _root.Anchor(Anchor.BottomLeft);
        _root.Visual.WidthUnits = DimensionUnitType.RelativeToParent;
        _root.Visual.Width = -20;
        _root.Visual.Height = 120;
        _root.X = 10;
        _root.Y = -10;

        // Dark background
        var bg = new ColoredRectangleRuntime
        {
            Width = 0,
            Height = 0,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 20,
            Green = 20,
            Blue = 40,
            Alpha = 220,
        };
        _root.Visual.Children.Add(bg);

        var layout = new StackPanel { Spacing = 4 };
        layout.Dock(Dock.Fill);

        _speakerLabel = new Label { Text = "" };
        layout.AddChild(_speakerLabel);

        _textLabel = new Label { Text = "" };
        _textLabel.Visual.WidthUnits = DimensionUnitType.RelativeToParent;
        _textLabel.Visual.Width = -10;
        layout.AddChild(_textLabel);

        _choiceList = new StackPanel { Spacing = 2 };
        _choiceList.Visual.Visible = false;
        layout.AddChild(_choiceList);

        _advanceHint = new Label { Text = "[Enter]" };
        _advanceHint.Anchor(Anchor.BottomRight);
        _advanceHint.X = -8;
        _advanceHint.Y = -4;
        layout.AddChild(_advanceHint);

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    public void Show(DialogueSystem dialogue)
    {
        _dialogue = dialogue;
        _root.Visual.Visible = true;
        DisplayCurrent();
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
        _dialogue = null;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible || _dialogue == null) return;

        var kb = engine.InputManager.Keyboard;
        var current = _dialogue.Current;

        if (current?.Choices is { Count: > 0 })
        {
            // Choice navigation
            if (kb.WasKeyPressed(Keys.Up))
            {
                _selectedChoiceIndex = (_selectedChoiceIndex - 1 + current.Choices.Count) % current.Choices.Count;
                UpdateChoiceHighlight();
            }
            else if (kb.WasKeyPressed(Keys.Down))
            {
                _selectedChoiceIndex = (_selectedChoiceIndex + 1) % current.Choices.Count;
                UpdateChoiceHighlight();
            }
            else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
            {
                bool hasMore = _dialogue.SelectChoice(_selectedChoiceIndex);
                if (hasMore)
                    DisplayCurrent();
                else
                    EndDialogue();
            }
        }
        else
        {
            // Simple advance
            if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
            {
                bool hasMore = _dialogue.Advance();
                if (hasMore)
                    DisplayCurrent();
                else
                    EndDialogue();
            }
        }
    }

    private void DisplayCurrent()
    {
        var node = _dialogue?.Current;
        if (node == null) return;

        _speakerLabel.Text = node.Speaker;
        _textLabel.Text = node.Text;

        // Clear old choices
        foreach (var label in _choiceLabels)
            _choiceList.Visual.Children.Remove(label.Visual);
        _choiceLabels.Clear();

        if (node.Choices is { Count: > 0 })
        {
            _selectedChoiceIndex = 0;
            _choiceList.Visual.Visible = true;
            _advanceHint.Visual.Visible = false;

            for (int i = 0; i < node.Choices.Count; i++)
            {
                string prefix = i == 0 ? "> " : "  ";
                var label = new Label { Text = $"{prefix}{node.Choices[i].Text}" };
                _choiceList.AddChild(label);
                _choiceLabels.Add(label);
            }
        }
        else
        {
            _choiceList.Visual.Visible = false;
            _advanceHint.Visual.Visible = true;
        }
    }

    private void UpdateChoiceHighlight()
    {
        var choices = _dialogue?.Current?.Choices;
        if (choices == null) return;

        for (int i = 0; i < _choiceLabels.Count && i < choices.Count; i++)
        {
            string prefix = i == _selectedChoiceIndex ? "> " : "  ";
            _choiceLabels[i].Text = $"{prefix}{choices[i].Text}";
        }
    }

    private void EndDialogue()
    {
        Hide();
        DialogueEnded?.Invoke();
    }
}
