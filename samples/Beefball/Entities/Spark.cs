using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using Vector2 = System.Numerics.Vector2;

namespace Beefball.Entities;

/// <summary>Pooled hit spark: a tiny circle that flies outward, slows down and fades.</summary>
public class Spark : Entity
{
    private Circle _circle = null!;
    private float _age;
    private float _lifetime = 0.6f;
    private Color _color;

    public override void CustomInitialize()
    {
        Z = 5f;
        Drag = 3f;
        _circle = new Circle { Radius = 2.4f, IsFilled = true, IsVisible = true, OutlineThickness = 0f };
        Add(_circle, isDefaultCollision: false);
    }

    protected override void Reset() => _age = 0f;

    public void Launch(Vector2 velocity, Color color, float lifetime)
    {
        Velocity = velocity;
        _color = color;
        _lifetime = lifetime;
        _age = 0f;
        Apply();
    }

    public override void CustomActivity(FrameTime time)
    {
        _age += time.DeltaSeconds;
        if (_age >= _lifetime)
        {
            Destroy();
            return;
        }
        Apply();
    }

    private void Apply()
    {
        float t = _age / _lifetime;
        // Fade in over the first few frames so a burst blooms rather than pops.
        float fadeIn = Math.Min(1f, _age / 0.06f);
        _circle.Radius = 2.4f * (1f - 0.5f * t);
        _circle.Color = Visuals.WithAlpha(_color, fadeIn * (1f - t));
    }
}
