using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Collision;

public class AxisAlignedRectangle : IAttachable, IRenderable, ICollidable
{
    public float Width { get; set; } = 32f;
    public float Height { get; set; } = 32f;

    // IAttachable
    public Entity? Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    // IRenderable (for debug drawing)
    public bool Visible { get; set; } = false;
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
    public string? Name { get; set; }

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!Visible) return;
        // TODO: Draw debug rectangle outline (requires primitive renderer, e.g. Apos.Shapes)
    }

    public void Destroy()
    {
        if (Parent is Entity entity)
            entity.RemoveChild(this);
        else
            Parent = null;
    }

    // ICollidable — delegates to CollisionDispatcher
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
