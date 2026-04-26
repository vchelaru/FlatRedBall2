using System;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Collision;

/// <summary>
/// A non-rotating rectangle aligned to the X and Y axes — the most common collision primitive
/// for tile-based geometry, hitboxes, and trigger zones. Cheaper to test than <see cref="Polygon"/>
/// and supports per-side selective response via <see cref="RepositionDirections"/>.
/// </summary>
/// <remarks>
/// Position (<see cref="X"/>/<see cref="Y"/>) is the rectangle's <i>center</i>, not a corner.
/// Use <see cref="Polygon.CreateRectangle"/> if you need a rectangle that can rotate.
/// </remarks>
public class AxisAlignedRectangle : IAttachable, IRenderable, ICollidable
{
    /// <summary>Width in world units. Defaults to 32. Centered on <see cref="X"/>.</summary>
    public float Width { get; set; } = 32f;
    /// <summary>Height in world units. Defaults to 32. Centered on <see cref="Y"/>.</summary>
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
    /// <inheritdoc/>
    public Entity? Parent { get; set; }
    /// <summary>Center X. Relative to <see cref="Parent"/> when attached, world when root.</summary>
    public float X { get; set; }
    /// <summary>Center Y (Y+ up). Relative to <see cref="Parent"/> when attached, world when root.</summary>
    public float Y { get; set; }
    /// <summary>Z value. See <see cref="Entity.Z"/> for draw-order semantics.</summary>
    public float Z { get; set; }
    /// <inheritdoc/>
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    /// <inheritdoc/>
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    /// <summary>Final Z after walking the parent chain.</summary>
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;
    /// <inheritdoc/>
    public float BroadPhaseRadius => MathF.Max(Width, Height) / 2f;

    // IRenderable
    /// <summary>Whether this rectangle is drawn. Defaults to <c>false</c> — collision shapes are hidden by default.</summary>
    public bool IsVisible { get; set; } = false;
    /// <inheritdoc/>
    public Layer? Layer { get; set; }
    /// <inheritdoc/>
    public IRenderBatch Batch { get; set; } = ShapesBatch.Instance;
    /// <summary>Optional logical name for diagnostics.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Fill or outline color. Defaults to semi-transparent white so overlapping shapes are obvious
    /// when made visible for debugging.
    /// </summary>
    public XnaColor Color { get; set; } = new XnaColor(255, 255, 255, 128);
    /// <summary>When <c>true</c>, the rectangle renders filled; when <c>false</c>, as an outline.</summary>
    public bool IsFilled { get; set; } = true;
    /// <summary>Outline thickness in pixels when <see cref="IsFilled"/> is <c>false</c>.</summary>
    public float OutlineThickness { get; set; } = 2f;

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Batch is not ShapesBatch sb) return;

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

    /// <summary>
    /// Detaches this rectangle from its parent entity. Called recursively by <see cref="Entity.Destroy"/>.
    /// </summary>
    public void Destroy()
    {
        if (Parent is Entity entity)
            entity.Remove(this);
        else
            Parent = null;
    }

    /// <inheritdoc/>
    public bool Contains(Vector2 worldPoint)
    {
        float hw = Width / 2f, hh = Height / 2f;
        return worldPoint.X >= AbsoluteX - hw && worldPoint.X <= AbsoluteX + hw
            && worldPoint.Y >= AbsoluteY - hh && worldPoint.Y <= AbsoluteY + hh;
    }

    /// <inheritdoc/>
    public bool CollidesWith(ICollidable other)
        => CollisionDispatcher.CollidesWith(this, other);

    /// <inheritdoc/>
    public Vector2 GetSeparationVector(ICollidable other)
        => CollisionDispatcher.GetSeparationVector(this, other);

    /// <inheritdoc/>
    public void SeparateFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f)
    {
        var offset = CollisionDispatcher.ComputeSeparationOffset(GetSeparationVector(other), thisMass, otherMass);
        X += offset.X;
        Y += offset.Y;
    }

    /// <inheritdoc/>
    public void ApplySeparationOffset(Vector2 offset) { X += offset.X; Y += offset.Y; }

    /// <summary>
    /// No-op on shapes — only <see cref="Entity"/> carries velocity. Velocity bounce is handled by
    /// <see cref="Entity.AdjustVelocityFrom"/> on the owning entity.
    /// </summary>
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
    /// <summary>No-op on shapes — see <see cref="AdjustVelocityFrom"/>.</summary>
    public void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
}
