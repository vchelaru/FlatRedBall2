using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace Beefball.Entities;

/// <summary>Pooled puck-trail dot: shrinks, fades and shifts from orange to red, then returns to the pool.</summary>
public class TrailDot : Entity
{
    private const float Lifetime = 0.45f;
    private const float StartAlpha = 0.55f;

    private Circle _circle = null!;
    private float _age;
    private float _startRadius;
    private Color _from;
    private Color _to;

    public override void CustomInitialize()
    {
        Z = -1f;
        _circle = new Circle { IsFilled = true, IsVisible = true, OutlineThickness = 0f };
        Add(_circle, isDefaultCollision: false);
    }

    protected override void Reset() => _age = 0f;

    public void Launch(float startRadius, Color from, Color to)
    {
        _startRadius = startRadius;
        _from = from;
        _to = to;
        _age = 0f;
        Apply();
    }

    public override void CustomActivity(FrameTime time)
    {
        _age += time.DeltaSeconds;
        if (_age >= Lifetime)
        {
            Destroy();
            return;
        }
        Apply();
    }

    private void Apply()
    {
        float t = _age / Lifetime;
        _circle.Radius = _startRadius * (1f - 0.75f * t);
        float fade = (1f - t) * (1f - t);
        _circle.Color = Visuals.WithAlpha(Color.Lerp(_from, _to, t), StartAlpha * fade);
    }
}
