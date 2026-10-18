using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace Beefball.Entities;

/// <summary>Pooled ring that expands around a player when it hits the puck.</summary>
public class HitRing : Entity
{
    private const float Lifetime = 0.35f;

    private Circle _circle = null!;
    private Entity? _follow;
    private float _age;
    private float _fromRadius;
    private float _toRadius;
    private Color _color;

    public override void CustomInitialize()
    {
        Z = 6f;
        _circle = new Circle { IsFilled = false, IsVisible = true, OutlineThickness = 2f };
        Add(_circle, isDefaultCollision: false);
    }

    protected override void Reset()
    {
        _age = 0f;
        _follow = null;
    }

    public void Launch(Entity follow, Color color, float fromRadius, float toRadius)
    {
        _follow = follow;
        _color = color;
        _fromRadius = fromRadius;
        _toRadius = toRadius;
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
        if (_follow != null)
        {
            X = _follow.X;
            Y = _follow.Y;
        }
        float t = _age / Lifetime;
        float eased = 1f - (1f - t) * (1f - t);
        _circle.Radius = _fromRadius + (_toRadius - _fromRadius) * eased;
        _circle.Color = Visuals.WithAlpha(_color, 0.8f * (1f - t));
    }
}
