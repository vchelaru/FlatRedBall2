using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Movement;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Path = FlatRedBall2.Math.Path;

namespace SampleProject1.Screens;

/// <summary>
/// Demonstrates Path and PathFollower:
/// - A racetrack-shaped path (straights connected by semicircular ends)
/// - An entity following the path with direction-facing enabled
/// - V toggles path visibility
/// </summary>
public class PathDemoScreen : Screen
{
    private Path _path = null!;
    private PathFollower _follower = null!;
    private Entity _dot = null!;
    private int _waypointCount;
    private Label _statusLabel = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(15, 20, 30);

        BuildPath();
        BuildDot();

        _follower = new PathFollower(_path)
        {
            Speed = 250f,
            Loops = true,
            FaceDirection = true,
        };

        _follower.WaypointReached += _ => _waypointCount++;

        SetupHud();
    }

    private void BuildPath()
    {
        // Racetrack: two horizontal straights connected by semicircular ends.
        // CW arcs (negative angle) bow outward to the right and left respectively.
        _path = new Path()
            .MoveTo(-300, 80)
            .LineTo(300, 80)                       // top straight
            .ArcTo(300, -80, -MathF.PI)            // right semicircle (bows right through x=400)
            .LineTo(-300, -80)                     // bottom straight
            .ArcTo(-300, 80, -MathF.PI);           // left semicircle (bows left through x=-400)

        _path.IsLooped = true;
        _path.Color = new Color(80, 160, 220);
        _path.LineThickness = 3f;

        Add(_path);
    }

    private void BuildDot()
    {
        _dot = new Entity();

        var body = new Circle
        {
            Radius = 12f,
            IsVisible = true,
            IsFilled = true,
            Color = new Color(255, 200, 50),
        };
        _dot.Add(body);

        // Small forward indicator offset in local +Y (entity faces +Y at rotation 0)
        var nose = new Circle
        {
            Radius = 5f,
            Y = 16f,
            IsVisible = true,
            IsFilled = true,
            Color = new Color(255, 100, 50),
        };
        _dot.Add(nose);

        Register(_dot);

        // Start the dot at the path origin
        var start = _path.PointAtRatio(0f);
        _dot.X = start.X;
        _dot.Y = start.Y;
    }

    public override void CustomActivity(FrameTime time)
    {
        _follower.Activity(_dot, time.DeltaSeconds);

        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.V))
            _path.IsVisible = !_path.IsVisible;

        _statusLabel.Text =
            $"V = toggle path  |  Waypoints: {_waypointCount}  |  " +
            $"Distance: {_follower.DistanceTraveled:F0} / {_path.TotalLength:F0}";
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        _statusLabel = new Label { Text = "" };
        _statusLabel.Anchor(Anchor.TopLeft);
        _statusLabel.X = 10;
        _statusLabel.Y = 10;
        panel.AddChild(_statusLabel);

        Add(panel);
    }
}
