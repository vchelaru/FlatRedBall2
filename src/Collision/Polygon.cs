using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;
// Disambiguate — Polygon points are System.Numerics.Vector2; XNA Vector2 is aliased above.
using Vec2 = System.Numerics.Vector2;

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

    /// <summary>
    /// Creates a polygon from an arbitrary list of points.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Collision detection uses the Separating Axis Theorem (SAT), which only works correctly
    /// for <b>convex</b> polygons. Passing concave (non-convex) points will produce incorrect
    /// collision responses — shapes may pass through concave regions or report false collisions.
    /// </para>
    /// <para>
    /// Rendering via <see cref="Draw"/> supports concave polygons (ear-clip triangulation).
    /// If you need both rendering and collision for a concave shape, decompose it into
    /// convex pieces manually and add each as a separate <see cref="Polygon"/>.
    /// </para>
    /// </remarks>
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
    // Swap IsFilled to false for an outline-only view.
    public XnaColor Color { get; set; } = new XnaColor(255, 255, 255, 128);
    public bool IsFilled { get; set; } = true;
    public float OutlineThickness { get; set; } = 2f;

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!Visible || Batch is not ShapesBatch sb || _points.Count < 2) return;

        float angle = AbsoluteRotation.Radians;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        if (IsFilled && _points.Count >= 3)
        {
            var worldPts = ComputeWorldPoints(cos, sin);
            foreach (var (i0, i1, i2) in EarClipTriangulate(worldPts))
            {
                var a = WorldToXna(worldPts[i0], camera);
                var b = WorldToXna(worldPts[i1], camera);
                var c = WorldToXna(worldPts[i2], camera);
                sb.Shapes.FillTriangle(a, b, c, Color);
            }
        }
        else
        {
            for (int i = 0; i < _points.Count; i++)
            {
                var a = ScreenPoint(_points[i], cos, sin, camera);
                var b = ScreenPoint(_points[(i + 1) % _points.Count], cos, sin, camera);
                sb.Shapes.FillLine(a, b, OutlineThickness, Color);
            }
        }
    }

    private Vec2[] ComputeWorldPoints(float cos, float sin)
    {
        var result = new Vec2[_points.Count];
        for (int i = 0; i < _points.Count; i++)
        {
            float lx = _points[i].X, ly = _points[i].Y;
            result[i] = new Vec2(AbsoluteX + lx * cos - ly * sin,
                                 AbsoluteY + lx * sin + ly * cos);
        }
        return result;
    }

    private static XnaVec2 WorldToXna(Vec2 worldPt, Camera camera)
    {
        var s = camera.WorldToScreen(worldPt);
        return new XnaVec2(s.X, s.Y);
    }

    private XnaVec2 ScreenPoint(Vec2 local, float cos, float sin, Camera camera)
    {
        // Rotate in Y-up world space, translate to world position, then convert to screen pixels.
        float rx = local.X * cos - local.Y * sin;
        float ry = local.X * sin + local.Y * cos;
        var screen = camera.WorldToScreen(new Vec2(AbsoluteX + rx, AbsoluteY + ry));
        return new XnaVec2(screen.X, screen.Y);
    }

    // Ear-clipping triangulation. Works for both convex and concave (simple) polygons.
    // Yields triangles as index triples into the original pts array.
    private static IEnumerable<(int, int, int)> EarClipTriangulate(Vec2[] pts)
    {
        if (pts.Length < 3) yield break;
        if (pts.Length == 3) { yield return (0, 1, 2); yield break; }

        var idx = new List<int>(pts.Length);
        for (int i = 0; i < pts.Length; i++) idx.Add(i);

        // Ear clipping requires CCW winding in Y-up space (positive signed area).
        if (SignedArea(pts) < 0) idx.Reverse();

        int guard = idx.Count * idx.Count;
        while (idx.Count > 3 && guard-- > 0)
        {
            for (int i = 0; i < idx.Count; i++)
            {
                int n = idx.Count;
                int prev = idx[(i - 1 + n) % n];
                int curr = idx[i];
                int next = idx[(i + 1) % n];
                if (IsEar(pts, idx, prev, curr, next))
                {
                    yield return (prev, curr, next);
                    idx.RemoveAt(i);
                    break;
                }
            }
        }
        if (idx.Count == 3) yield return (idx[0], idx[1], idx[2]);
    }

    // Shoelace formula. Positive = CCW in Y-up space.
    private static float SignedArea(Vec2[] pts)
    {
        float area = 0;
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % pts.Length];
            area += a.X * b.Y - b.X * a.Y;
        }
        return area;
    }

    private static bool IsEar(Vec2[] pts, List<int> idx, int prev, int curr, int next)
    {
        var a = pts[prev]; var b = pts[curr]; var c = pts[next];
        // Convex vertex in CCW polygon: left turn (positive cross product).
        if (Cross(b - a, c - a) <= 0) return false;
        // No other polygon vertex may lie strictly inside this triangle.
        foreach (var id in idx)
        {
            if (id == prev || id == curr || id == next) continue;
            if (PointInTriangle(pts[id], a, b, c)) return false;
        }
        return true;
    }

    private static float Cross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

    private static bool PointInTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
        return !((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0));
    }

    public void Destroy()
    {
        if (Parent is Entity entity)
            entity.Remove(this);
        else
            Parent = null;
    }

    /// <summary>
    /// Tests a line segment against all edges of this polygon and returns the closest intersection.
    /// </summary>
    /// <param name="start">World-space start of the segment (typically the ray origin).</param>
    /// <param name="end">World-space end of the segment.</param>
    /// <param name="hitPoint">World-space intersection point closest to <paramref name="start"/>, or <see cref="Vector2.Zero"/> when no hit.</param>
    /// <param name="hitNormal">Surface normal at the hit point, pointing toward <paramref name="start"/>, or <see cref="Vector2.Zero"/> when no hit.</param>
    /// <returns><c>true</c> if the segment intersects any edge of this polygon.</returns>
    public bool Raycast(Vec2 start, Vec2 end, out Vec2 hitPoint, out Vec2 hitNormal)
    {
        hitPoint = Vec2.Zero;
        hitNormal = Vec2.Zero;

        float angle = AbsoluteRotation.Radians;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);

        // Build world-space points
        var worldPts = new Vec2[_points.Count];
        for (int i = 0; i < _points.Count; i++)
        {
            float lx = _points[i].X, ly = _points[i].Y;
            worldPts[i] = new Vec2(AbsoluteX + lx * cos - ly * sin,
                                   AbsoluteY + lx * sin + ly * cos);
        }

        var rayDir = end - start;
        float minT = float.MaxValue;
        int hitEdge = -1;

        for (int i = 0; i < worldPts.Length; i++)
        {
            float t = SegmentT(start, rayDir, worldPts[i], worldPts[(i + 1) % worldPts.Length]);
            if (t >= 0f && t < minT) { minT = t; hitEdge = i; }
        }

        if (hitEdge < 0) return false;

        hitPoint = start + rayDir * minT;
        var edgeDir = worldPts[(hitEdge + 1) % worldPts.Length] - worldPts[hitEdge];
        hitNormal = Vec2.Normalize(new Vec2(-edgeDir.Y, edgeDir.X));
        if (Vec2.Dot(hitNormal, start - hitPoint) < 0) hitNormal = -hitNormal;
        return true;
    }

    // Returns t in [0,1] along rayDir for the intersection with edge [a,b], or -1 if none.
    private static float SegmentT(Vec2 start, Vec2 rayDir, Vec2 a, Vec2 b)
    {
        var edgeDir = b - a;
        float denom = RayCross(rayDir, edgeDir);
        if (MathF.Abs(denom) < 1e-8f) return -1f;
        var ac = a - start;
        float t = RayCross(ac, edgeDir) / denom;
        float s = RayCross(ac, rayDir) / denom;
        return t >= 0f && t <= 1f && s >= 0f && s <= 1f ? t : -1f;
    }

    private static float RayCross(Vec2 a, Vec2 b) => a.X * b.Y - a.Y * b.X;

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

    // Shapes don't carry velocity — only Entity does. AdjustVelocityFrom is intentionally a no-op here.
    // Velocity bounce is handled by Entity.AdjustVelocityFrom, which is called on the owning entity.
    public void AdjustVelocityFrom(ICollidable other, float thisMass = 1f, float otherMass = 1f, float elasticity = 1f) { }
}
