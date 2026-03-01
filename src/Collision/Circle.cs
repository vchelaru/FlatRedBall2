using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Collision;

public class Circle : IAttachable, IRenderable, ICollidable
{
    public float Radius { get; set; } = 16f;

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
    // Swap IsFilled to false for an outline-only view.
    public XnaColor Color { get; set; } = new XnaColor(255, 255, 255, 128);
    public bool IsFilled { get; set; } = true;
    public float OutlineThickness { get; set; } = 2f;

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!Visible || Batch is not ShapesBatch sb) return;

        var center = new XnaVec2(AbsoluteX, AbsoluteY);

        if (IsFilled)
            sb.Shapes.FillCircle(center, Radius, Color);
        else
            sb.Shapes.BorderCircle(center, Radius, Color, OutlineThickness);
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
