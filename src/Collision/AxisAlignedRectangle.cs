using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Collision;

public class AxisAlignedRectangle : IAttachable, IRenderable, ICollidable
{
    public float Width { get; set; } = 32f;
    public float Height { get; set; } = 32f;

    /// <summary>
    /// Controls which directions this rectangle may reposition overlapping objects.
    /// When a collision occurs, the engine computes the minimum displacement along each allowed
    /// axis and applies the smallest one — so even a left-edge hit against a Down-only rect
    /// will push the object downward rather than suppressing the collision.
    /// Defaults to <see cref="RepositionDirections.All"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this to prevent "snagging" at the seams of adjacent rectangles. For a horizontal
    /// strip of floor tiles, clear <see cref="RepositionDirections.Left"/> and
    /// <see cref="RepositionDirections.Right"/> from interior tiles so objects glide across
    /// the top surface without catching on shared vertical edges.
    /// </para>
    /// <para>
    /// For circles the displacement is geometrically exact: the circle is moved until its arc
    /// grazes the target face (or corner), never inflated to its bounding box.
    /// </para>
    /// </remarks>
    public RepositionDirections RepositionDirections { get; set; } = RepositionDirections.All;

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

        // Convert world-space corners to screen pixels (Y-flip handled by WorldToScreen).
        // World top-left  = (X - W/2, Y + H/2); world bottom-right = (X + W/2, Y - H/2).
        var screenTL = camera.WorldToScreen(new Vector2(AbsoluteX - Width / 2f, AbsoluteY + Height / 2f));
        var screenBR = camera.WorldToScreen(new Vector2(AbsoluteX + Width / 2f, AbsoluteY - Height / 2f));
        var xy   = new XnaVec2(screenTL.X, screenTL.Y);
        var size = new XnaVec2(screenBR.X - screenTL.X, screenBR.Y - screenTL.Y);

        if (IsFilled)
            sb.Shapes.FillRectangle(xy, size, Color, aaSize:0f);
        else
            sb.Shapes.BorderRectangle(xy, size, Color, OutlineThickness, aaSize: 0f);
    }

    public void Destroy()
    {
        if (Parent is Entity entity)
            entity.Remove(this);
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
        var offset = CollisionDispatcher.ComputeSeparationOffset(GetSeparationVector(other), thisMass, otherMass);
        X += offset.X;
        Y += offset.Y;
    }

    public void ApplySeparationOffset(Vector2 offset) { X += offset.X; Y += offset.Y; }

    // Shapes don't carry velocity — only Entity does. AdjustVelocityFrom is intentionally a no-op here.
    // Velocity bounce is handled by Entity.AdjustVelocityFrom, which is called on the owning entity.
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
    public void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
}
