using System;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Collision;

/// <summary>
/// A line segment defined by two world-space endpoints, <see cref="AbsolutePoint1"/> and <see cref="AbsolutePoint2"/>.
/// </summary>
/// <remarks>
/// <see cref="IAttachable.X"/>/<see cref="IAttachable.Y"/> is the parent-relative offset to the first endpoint
/// (or the world position when unparented). <see cref="EndPoint"/> is added to that to give the second endpoint.
/// Use <see cref="AbsolutePoint1"/> and <see cref="AbsolutePoint2"/> to read world-space positions,
/// and <see cref="SetAbsoluteEndpoint"/> to set the second endpoint directly in world space.
/// </remarks>
public class Line : IAttachable, IRenderable, ICollidable
{
    /// <summary>
    /// Offset from the first endpoint to the second, in world units.
    /// Added to <see cref="AbsolutePoint1"/> to produce <see cref="AbsolutePoint2"/>.
    /// </summary>
    public Vector2 EndPoint { get; set; } = new Vector2(32f, 0f);

    // IAttachable
    /// <inheritdoc/>
    public Entity? Parent { get; set; }
    /// <summary>X of the first endpoint. Relative to <see cref="Parent"/> when attached, world when root.</summary>
    public float X { get; set; }
    /// <summary>Y of the first endpoint (Y+ up). Relative to <see cref="Parent"/> when attached, world when root.</summary>
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
    public float BroadPhaseRadius => EndPoint.Length();

    // IRenderable
    /// <summary>Whether this line is drawn. Defaults to <c>false</c> — collision shapes are hidden by default.</summary>
    public bool IsVisible { get; set; } = false;
    /// <inheritdoc/>
    public Layer? Layer { get; set; }
    /// <inheritdoc/>
    public IRenderBatch Batch { get; set; } = ShapesBatch.Instance;
    /// <summary>Optional logical name for diagnostics.</summary>
    public string? Name { get; set; }

    /// <summary>Line color. Defaults to opaque white.</summary>
    public XnaColor Color { get; set; } = new XnaColor(255, 255, 255, 255);
    /// <summary>Line thickness in pixels. Defaults to 1.</summary>
    public float LineThickness { get; set; } = 1f;

    /// <summary>World-space position of the first endpoint — same as (<see cref="AbsoluteX"/>, <see cref="AbsoluteY"/>).</summary>
    public Vector2 AbsolutePoint1 => new Vector2(AbsoluteX, AbsoluteY);

    /// <summary>World-space position of the second endpoint: <c>(AbsoluteX + EndPoint.X, AbsoluteY + EndPoint.Y)</c>.</summary>
    public Vector2 AbsolutePoint2 => new Vector2(AbsoluteX + EndPoint.X, AbsoluteY + EndPoint.Y);

    /// <summary>
    /// Sets <see cref="EndPoint"/> so that <see cref="AbsolutePoint2"/> lands at <paramref name="absolutePoint"/>.
    /// </summary>
    public void SetAbsoluteEndpoint(Vector2 absolutePoint) => EndPoint = absolutePoint - AbsolutePoint1;

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Batch is not ShapesBatch sb) return;

        var screen1 = camera.WorldToScreen(AbsolutePoint1);
        var screen2 = camera.WorldToScreen(AbsolutePoint2);

        var radius = LineThickness / 2f;

        // thickness will always take "1" pixel on the edge, so we need to subtract 1 and then split the rest by 2
        var remainderRadius = (LineThickness - 1) / 2f;
        remainderRadius = System.Math.Max(0.5f, remainderRadius);
        
        var aa = 0.5f;

        sb.Shapes.FillLine(
            new XnaVec2(screen1.X, screen1.Y),
            new XnaVec2(screen2.X, screen2.Y),
            // line radius:
            remainderRadius,
            aaSize:aa,
            c:Color);
    }

    /// <summary>
    /// Detaches this line from its parent entity. Called recursively by <see cref="Entity.Destroy"/>.
    /// </summary>
    public void Destroy()
    {
        if (Parent is Entity entity)
            entity.Remove(this);
        else
            Parent = null;
    }

    // ── ICollidable ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if this segment intersects <paramref name="other"/>.
    /// Handles Line, Circle, and AxisAlignedRectangle. Returns false for unrecognized types.
    /// </summary>
    public bool CollidesWith(ICollidable other) => other switch
    {
        Line otherLine                 => CollideAgainst(otherLine),
        Circle circle                  => CollideAgainst(circle),
        AxisAlignedRectangle rect      => CollideAgainst(rect),
        Polygon polygon                => CollideAgainst(polygon),
        _                              => false
    };

    /// <summary>Returns true if this segment intersects any edge of <paramref name="other"/>.</summary>
    public bool CollideAgainst(Polygon other)
        => other.Raycast(AbsolutePoint1, AbsolutePoint2, out _, out _);

    /// <summary>
    /// Returns the minimum displacement needed to separate this segment from <paramref name="other"/>.
    /// Only meaningful for <see cref="AxisAlignedRectangle"/> targets — other shape pairings return
    /// <see cref="Vector2.Zero"/> because there is no well-defined separation vector for a degenerate
    /// (zero-width) segment.
    /// </summary>
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

    /// <summary>No-op on shapes — only <see cref="Entity"/> carries velocity.</summary>
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
    /// <summary>No-op on shapes — see <see cref="AdjustVelocityFrom"/>.</summary>
    public void AdjustVelocityFromSeparation(Vector2 sep, ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }

    // ── Typed collision tests ─────────────────────────────────────────────

    /// <summary>Returns true if this segment intersects <paramref name="other"/>.</summary>
    public bool CollideAgainst(Line other)
        => SegmentsIntersect(AbsolutePoint1, AbsolutePoint2, other.AbsolutePoint1, other.AbsolutePoint2);

    /// <summary>
    /// Returns true if this segment intersects <paramref name="other"/>.
    /// Computes the shortest distance from the circle center to the segment and
    /// compares against the circle radius.
    /// </summary>
    public bool CollideAgainst(Circle other)
    {
        var p = new Vector2(other.AbsoluteX, other.AbsoluteY);
        float dist = DistanceSquaredToPoint(AbsolutePoint1, AbsolutePoint2, p);
        return dist <= other.Radius * other.Radius;
    }

    /// <summary>
    /// Returns true if this segment intersects <paramref name="other"/>.
    /// Tests the segment against all four edges of the AABB, and also checks
    /// whether either endpoint lies inside the rectangle.
    /// </summary>
    public bool CollideAgainst(AxisAlignedRectangle other)
    {
        float left   = other.AbsoluteX - other.Width  / 2f;
        float right  = other.AbsoluteX + other.Width  / 2f;
        float bottom = other.AbsoluteY - other.Height / 2f;
        float top    = other.AbsoluteY + other.Height / 2f;

        var p1 = AbsolutePoint1;
        var p2 = AbsolutePoint2;

        // Either endpoint inside the rectangle counts as a collision.
        if (PointInAabb(p1, left, right, bottom, top)) return true;
        if (PointInAabb(p2, left, right, bottom, top)) return true;

        // Test the segment against each of the four AABB edges.
        var tl = new Vector2(left,  top);
        var tr = new Vector2(right, top);
        var br = new Vector2(right, bottom);
        var bl = new Vector2(left,  bottom);

        return SegmentsIntersect(p1, p2, tl, tr)
            || SegmentsIntersect(p1, p2, tr, br)
            || SegmentsIntersect(p1, p2, br, bl)
            || SegmentsIntersect(p1, p2, bl, tl);
    }

    // ── Geometry helpers ──────────────────────────────────────────────────

    // Standard parametric segment-vs-segment test using cross products.
    // Returns true when the two segments share an interior point or share an endpoint.
    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var ab = b - a;
        var cd = d - c;
        float denom = Cross(ab, cd);

        // Parallel (or degenerate) segments — check collinear overlap.
        if (MathF.Abs(denom) < 1e-8f)
            return CollinearOverlap(a, b, c, d);

        var ac = c - a;
        float t = Cross(ac, cd) / denom;
        float u = Cross(ac, ab) / denom;

        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    // 2D cross product (perp-dot product).
    private static float Cross(Vector2 v, Vector2 w) => v.X * w.Y - v.Y * w.X;

    // Checks whether two collinear segments share any point.
    // Callers must only invoke this when the segments are already known to be parallel.
    private static bool CollinearOverlap(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var axis = b - a;
        float lenSq = axis.LengthSquared();
        if (lenSq < 1e-12f) return false; // degenerate first segment

        // Parallel but not collinear — no intersection.
        // Cross product of axis with (c−a) is non-zero when they are offset perpendicular to the axis.
        float cross = Cross(axis, c - a);
        if (MathF.Abs(cross) > 1e-6f * lenSq) return false;

        // Project c and d onto the axis and check for 1D overlap with [0, 1].
        float t2 = Vector2.Dot(c - a, axis) / lenSq;
        float t3 = Vector2.Dot(d - a, axis) / lenSq;

        float minCD = MathF.Min(t2, t3);
        float maxCD = MathF.Max(t2, t3);

        return maxCD >= 0f && minCD <= 1f;
    }

    // Returns the squared distance from point p to segment (a, b).
    private static float DistanceSquaredToPoint(Vector2 a, Vector2 b, Vector2 p)
    {
        var ab = b - a;
        float lenSq = ab.LengthSquared();
        if (lenSq < 1e-12f) return (p - a).LengthSquared(); // degenerate

        float t = System.Math.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
        var closest = a + ab * t;
        return (p - closest).LengthSquared();
    }

    private static bool PointInAabb(Vector2 p, float left, float right, float bottom, float top)
        => p.X >= left && p.X <= right && p.Y >= bottom && p.Y <= top;
}
