using FlatRedBall2;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;

namespace ShipSample;

public class GameScreen : Screen
{
    private Factory<Ship> _ships = null!;
    private Factory<Shot> _shots = null!;
    private Label _debugLabel = null!;

    public override void CustomInitialize()
    {
        var gameplay = new Layer("Gameplay");
        Layers.Add(gameplay);

        // Shot factory must be registered before Ship so Engine.GetFactory<Shot>() works when shooting
        _shots = new Factory<Shot>(this);
        _ships = new Factory<Ship>(this);

        _ships.Create();

        _debugLabel = new Label();
        _debugLabel.Anchor(Anchor.TopLeft);
        _debugLabel.X = 8;
        _debugLabel.Y = 8;
        Add(_debugLabel);
    }

    public override void CustomActivity(FrameTime time)
    {
        var ship = _ships.Instances.Count > 0 ? _ships.Instances[0] : null;
        if (ship != null)
        {
            Camera.X = ship.X;
            Camera.Y = ship.Y;
            _debugLabel.Text = $"Rotation: {ship.Rotation.Degrees:F1}°";
        }
    }
}
