using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;

namespace RiftboundSample.UI;

/// <summary>
/// Shows pet evolution scene: name change and new abilities.
/// Press Enter to confirm.
/// </summary>
public class PetEvolutionPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;
    private Label _messageLabel = null!;
    private Label _nameLabel = null!;
    private StackPanel _abilityList = null!;

    public bool IsVisible => _root?.Visual.Visible ?? false;
    public event Action? Confirmed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Anchor(Anchor.Center);
        _root.Visual.Visible = false;

        var bg = new ColoredRectangleRuntime
        {
            Width = 260, Height = 180,
            Red = 20, Green = 10, Blue = 40, Alpha = 240,
        };
        _root.Visual.Children.Add(bg);

        var layout = new StackPanel { Spacing = 8 };
        layout.X = 10;
        layout.Y = 10;

        _messageLabel = new Label { Text = "" };
        layout.AddChild(_messageLabel);

        _nameLabel = new Label { Text = "" };
        layout.AddChild(_nameLabel);

        layout.AddChild(new Label { Text = "New Abilities:" });

        _abilityList = new StackPanel { Spacing = 2 };
        layout.AddChild(_abilityList);

        layout.AddChild(new Label { Text = "Press Enter to confirm" });

        _root.AddChild(layout);
        _screen.Add(_root);
    }

    public void Show(PetState pet, PetEvolution evolution)
    {
        _messageLabel.Text = $"{pet.Name} is evolving!";
        _nameLabel.Text = $"-> {evolution.EvolvedName}";

        ClearAbilities();
        _abilityList.AddChild(new Label { Text = $"  Basic: {evolution.EvolvedAbilityBasic}" });
        _abilityList.AddChild(new Label { Text = $"  Advanced: {evolution.EvolvedAbilityAdvanced}" });
        _abilityList.AddChild(new Label { Text = $"  Ultimate: {evolution.EvolvedAbilityUltimate}" });

        _root.Visual.Visible = true;
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible) return;

        if (engine.InputManager.Keyboard.WasKeyPressed(Keys.Enter))
        {
            Hide();
            Confirmed?.Invoke();
        }
    }

    private void ClearAbilities()
    {
        var children = _abilityList.Visual.Children;
        for (int i = children.Count - 1; i >= 0; i--)
            children.RemoveAt(i);
    }
}
