using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

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
    public bool Visible { get; set; } = false;
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = ShapesBatch.Instance;
    public string? Name { get; set; }

    // Visual — semi-transparent white so overlapping shapes are obvious.
    // IsFilled is accepted but Polygon always draws as an outline (no fill triangulation yet).
    // Use IsFilled = true (default) for a thicker outline that reads as "solid" at a glance.
    public XnaColor Color { get; set; } = new XnaColor(255, 255, 255, 128);
    public bool IsFilled { get; set; } = true;
    public float OutlineThickness { get; set; } = 2f;

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!Visible || Batch is not ShapesBatch sb || _points.Count < 2) return;

        float thickness = IsFilled ? OutlineThickness * 2f : OutlineThickness;
        float angle = AbsoluteRotation.Radians;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        for (int i = 0; i < _points.Count; i++)
        {
            var a = ScreenPoint(_points[i], cos, sin, camera);
            var b = ScreenPoint(_points[(i + 1) % _points.Count], cos, sin, camera);
            sb.Shapes.FillLine(a, b, thickness, Color);
        }
    }

    private XnaVec2 ScreenPoint(Vector2 local, float cos, float sin, Camera camera)
    {
        // Rotate in Y-up world space, translate to world position, then convert to screen pixels.
        float rx = local.X * cos - local.Y * sin;
        float ry = local.X * sin + local.Y * cos;
        var screen = camera.WorldToScreen(new Vector2(AbsoluteX + rx, AbsoluteY + ry));
        return new XnaVec2(screen.X, screen.Y);
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
        float r = otherMass / total;
        X += sep.X * r;
        Y += sep.Y * r;
    }

    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
}
