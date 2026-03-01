using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Collision;

public class Polygon : IAttachable, IRenderable, ICollidable
{
    private readonly List<Vector2> _points = new();

    public IReadOnlyList<Vector2> Points => _points;

    // Own rotation (not inherited from IAttachable rotation — per the architecture, Polygon has its own)
    public Angle Rotation { get; set; }
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;

    public static Polygon CreateRectangle(float width, float height)
    {
        float hw = width / 2f, hh = height / 2f;
        var poly = new Polygon();
        poly._points.AddRange(new[]
        {
            new Vector2(-hw, -hh),
            new Vector2( hw, -hh),
            new Vector2( hw,  hh),
            new Vector2(-hw,  hh)
        });
        return poly;
    }

    public static Polygon FromPoints(IEnumerable<Vector2> points)
    {
        var poly = new Polygon();
        poly._points.AddRange(points);
        return poly;
    }

    // IAttachable
    public Entity? Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    // IRenderable
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
    public string? Name { get; set; }

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        // TODO: Draw debug polygon outline via DebugRenderer
    }

    public void Destroy()
    {
        if (Parent is Entity entity)
            entity.RemoveChild(this);
        else
            Parent = null;
    }

    public bool CollidesWith(ICollidable other)
        => CollisionDispatcher.GetSeparationVector(this, other) != Vector2.Zero;

    public Vector2 GetSeparationVector(ICollidable other)
        => CollisionDispatcher.GetSeparationVector(this, other);

    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f)
    {
        var sep = GetSeparationVector(other);
        if (sep == Vector2.Zero) return;
        float total = thisMass + otherMass;
        if (total == 0) return;
        if (thisMass != 0)
        {
            float r = otherMass == 0 ? 1f : otherMass / total;
            X += sep.X * r;
            Y += sep.Y * r;
        }
    }

    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
}
