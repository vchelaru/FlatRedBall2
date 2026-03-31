using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SampleProject1.Entities;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Screens;

/// <summary>
/// Demonstrates MoveToLayer: pressing Space toggles the player between the Background and
/// Foreground layers. The purple overlay rectangle is always on Foreground. When the player
/// is on Background it draws behind the overlay; on Foreground it draws in front.
/// </summary>
public class MoveToLayerDemoScreen : Screen
{
    private Layer _behind = null!;
    private Layer _overlayLayer = null!;
    private Layer _front = null!;
    private TopDownPlayer _player = null!;
    private bool _isInFront = false;
    private Label _statusLabel = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new XnaColor(25, 25, 35);

        // Three layers in draw order: Behind → Overlay → Front.
        // The player moves between Behind (index 0) and Front (index 2).
        // The overlay is fixed at index 1, so it is always between them.
        _behind = new Layer("Behind");
        _overlayLayer = new Layer("Overlay");
        _front = new Layer("Front");
        Layers.Add(_behind);
        Layers.Add(_overlayLayer);
        Layers.Add(_front);

        // Semi-transparent purple rectangle permanently on the Overlay layer.
        var overlay = new AxisAlignedRectangle
        {
            Width = 300f,
            Height = 300f,
            Color = new XnaColor(180, 80, 220, 160),
            IsFilled = true,
            IsVisible = true,
            Layer = _overlayLayer,
        };
        Add(overlay);

        // Start the player to the left so it partially overlaps the overlay from the start.
        var playerFactory = new Factory<TopDownPlayer>(this);
        _player = playerFactory.Create();
        _player.X = -80f;
        _player.Y = 0f;
        _player.Layer = _behind;

        SetupHud();
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Space))
        {
            _isInFront = !_isInFront;
            _player.Layer = _isInFront ? _front : _behind;
            UpdateStatusLabel();
        }
    }

    private void UpdateStatusLabel()
    {
        _statusLabel.Text = _isInFront
            ? "Player layer: Front  →  drawn IN FRONT of the purple overlay"
            : "Player layer: Behind  →  drawn BEHIND the purple overlay";
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        var concept = new Label
        {
            Text =
                "MoveToLayer Demo\n" +
                "─────────────────────────────────────────────────────\n" +
                "Three layers in draw order:  Behind  →  Overlay  →  Front.\n" +
                "The purple rectangle is permanently on the Overlay layer.\n" +
                "\n" +
                "Press Space to toggle the player between Behind and Front:\n" +
                "  Behind  →  player is occluded by the purple overlay\n" +
                "  Front   →  player is drawn in front of the overlay",
        };
        concept.Anchor(Anchor.TopLeft);
        concept.X = 10;
        concept.Y = 10;
        panel.AddChild(concept);

        var controls = new Label { Text = "Arrow Keys / WASD: move\nSpace: toggle layer" };
        controls.Anchor(Anchor.TopRight);
        controls.X = -10;
        controls.Y = 10;
        panel.AddChild(controls);

        _statusLabel = new Label();
        _statusLabel.Anchor(Anchor.BottomLeft);
        _statusLabel.X = 10;
        _statusLabel.Y = -10;
        panel.AddChild(_statusLabel);
        UpdateStatusLabel();

        Add(panel);
    }
}
