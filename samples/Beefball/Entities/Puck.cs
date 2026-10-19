using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using Vector2 = System.Numerics.Vector2;

namespace Beefball.Entities;

public class Puck : Entity
{
    private const float TrailSpacing = 3.5f;
    private const float MinTrailSpeed = 30f;
    private const float MaxTrailJump = 60f;

    public BeefballConfig Config { get; set; } = null!;
    public Circle Body { get; private set; } = null!;

    private Vector2 _lastTrailPosition;
    private bool _hasTrailPosition;

    public override void CustomInitialize()
    {
        var p = Config.Puck;
        var palette = Config.Palette;
        Drag = p.Drag;

        Visuals.AddRadialGlow(this, Config.Glow.Puck, Config.Glow.Strength, palette.Puck, p.Radius);

        Body = new Circle
        {
            Radius = p.Radius, IsFilled = true, IsVisible = true,
            Color = palette.Puck, OutlineThickness = 0f,
        };
        Add(Body);

        Add(new Circle
        {
            Radius = p.Radius * 0.45f, IsFilled = true, IsVisible = true,
            Color = palette.PuckCore, Z = 0.1f,
        }, isDefaultCollision: false);
    }

    public override void CustomActivity(FrameTime time)
    {
        var position = new Vector2(X, Y);
        if (Velocity.Length() < MinTrailSpeed)
        {
            _hasTrailPosition = false;
            return;
        }

        if (!_hasTrailPosition)
        {
            _lastTrailPosition = position;
            _hasTrailPosition = true;
            return;
        }

        // Drop dots at even spacing along the path so a fast puck leaves a continuous streak.
        var step = position - _lastTrailPosition;
        float distance = step.Length();
        if (distance > MaxTrailJump)
        {
            // Teleported (round reset or staging): do not smear a streak across the table.
            _lastTrailPosition = position;
            return;
        }
        if (distance < TrailSpacing) return;

        var direction = step / distance;
        var factory = Engine.GetFactory<TrailDot>();
        while (distance >= TrailSpacing)
        {
            _lastTrailPosition += direction * TrailSpacing;
            distance -= TrailSpacing;
            var dot = factory.Create();
            dot.X = _lastTrailPosition.X;
            dot.Y = _lastTrailPosition.Y;
            dot.Launch(Config.Puck.Radius, Config.Palette.Puck, Config.Palette.TrailEnd);
        }
    }
}
