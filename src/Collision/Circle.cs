using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Collision;

/// <summary>
/// A circular collision shape with a configurable <see cref="Radius"/>. Attaches to entities,
/// renders as a filled disk or outlined ring, and collides with all other shape types via
/// <see cref="CollisionDispatcher"/>.
/// </summary>
public class Circle : IAttachable, IRenderable, ICollidable
{
    /// <summary>Radius in world units. Defaults to 16. Used for both rendering and collision.</summary>
    public float Radius { get; set; } = 16f;

    // IAttachable
    /// <inheritdoc/>
    public Entity? Parent { get; set; }
    /// <summary>X position. Relative to <see cref="Parent"/> when attached, world when root.</summary>
    public float X { get; set; }
    /// <summary>Y position (Y+ up). Relative to <see cref="Parent"/> when attached, world when root.</summary>
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
    public float BroadPhaseRadius => Radius;

    // IRenderable
    /// <summary>Whether this circle is drawn. Defaults to <c>false</c> — collision shapes are hidden by default.</summary>
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
    /// <summary>When <c>true</c>, the circle renders as a filled disk; when <c>false</c>, as a ring outline.</summary>
    public bool IsFilled { get; set; } = true;
    /// <summary>Outline thickness in pixels when <see cref="IsFilled"/> is <c>false</c>.</summary>
    public float OutlineThickness { get; set; } = 2f;

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Batch is not ShapesBatch sb) return;

        // Convert world-space center to screen pixels (Y-flip handled by WorldToScreen).
        var screenCenter = camera.WorldToScreen(new Vector2(AbsoluteX, AbsoluteY));
        var center = new XnaVec2(screenCenter.X, screenCenter.Y);

        // Scale radius from world units to screen pixels by converting a point one radius
        // along X and measuring the pixel distance.
        var screenEdge = camera.WorldToScreen(new Vector2(AbsoluteX + Radius, AbsoluteY));
        float screenRadius = screenEdge.X - screenCenter.X;

        screenRadius -= 1; // to account for anti-aliasing, so the circle doesn't draw outside its bounds

        if (IsFilled)
            sb.Shapes.FillCircle(center, screenRadius, Color, aaSize:1);
        else
            sb.Shapes.BorderCircle(center, screenRadius, Color, OutlineThickness, aaSize:1);
    }

    /// <summary>
    /// Detaches this circle from its parent entity. Called recursively by <see cref="Entity.Destroy"/>.
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
        float dx = worldPoint.X - AbsoluteX;
        float dy = worldPoint.Y - AbsoluteY;
        return dx * dx + dy * dy <= Radius * Radius;
    }

    /// <inheritdoc/>
    public bool CollidesWith(ICollidable other)
        => CollisionDispatcher.GetSeparationVector(this, other) != Vector2.Zero;

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
