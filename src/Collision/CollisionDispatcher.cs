using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Math;

namespace FlatRedBall2.Collision;

internal static class CollisionDispatcher
{
    public static bool CollidesWith(ICollidable a, ICollidable b)
        => GetSeparationVector(a, b) != Vector2.Zero || PointsOverlap(a, b);

    // Returns MTV (minimum translation vector) to move 'a' out of 'b'.
    // Returns Vector2.Zero if no collision.
    public static Vector2 GetSeparationVector(ICollidable a, ICollidable b)
    {
        return (a, b) switch
        {
            (AxisAlignedRectangle ra, AxisAlignedRectangle rb) => AabbVsAabb(ra, rb),
            (Circle ca, Circle cb)                             => CircleVsCircle(ca, cb),
            (AxisAlignedRectangle ra, Circle cb)               => -AabbVsCircle(ra, cb),
            (Circle ca, AxisAlignedRectangle rb)               => AabbVsCircle(rb, ca),
            (Polygon pa, Polygon pb)                           => PolygonVsPolygon(pa, pb),
            (Polygon pa, AxisAlignedRectangle rb)              => PolygonVsAabb(pa, rb),
            (AxisAlignedRectangle ra, Polygon pb)              => -PolygonVsAabb(pb, ra),
            (Polygon pa, Circle cb)                            => PolygonVsCircle(pa, cb),
            (Circle ca, Polygon pb)                            => -PolygonVsCircle(pb, ca),
            _                                                  => Vector2.Zero
        };
    }

    private static bool PointsOverlap(ICollidable a, ICollidable b)
    {
        // Handled entirely by GetSeparationVector returning non-zero
        return false;
    }

    // AABB vs AABB
    private static Vector2 AabbVsAabb(AxisAlignedRectangle a, AxisAlignedRectangle b)
    {
        float ax = a.AbsoluteX, ay = a.AbsoluteY;
        float bx = b.AbsoluteX, by = b.AbsoluteY;
        float hw_a = a.Width / 2f, hh_a = a.Height / 2f;
        float hw_b = b.Width / 2f, hh_b = b.Height / 2f;

        float overlapX = (hw_a + hw_b) - MathF.Abs(ax - bx);
        float overlapY = (hh_a + hh_b) - MathF.Abs(ay - by);

        if (overlapX <= 0 || overlapY <= 0) return Vector2.Zero;

        if (overlapX < overlapY)
            return new Vector2(ax < bx ? -overlapX : overlapX, 0f);
        else
            return new Vector2(0f, ay < by ? -overlapY : overlapY);
    }

    // Circle vs Circle
    private static Vector2 CircleVsCircle(Circle a, Circle b)
    {
        float dx = b.AbsoluteX - a.AbsoluteX;
        float dy = b.AbsoluteY - a.AbsoluteY;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        float radii = a.Radius + b.Radius;

        if (dist >= radii) return Vector2.Zero;

        if (dist < 1e-6f) return new Vector2(-radii, 0f);

        float overlap = radii - dist;
        return new Vector2(-dx / dist * overlap, -dy / dist * overlap);
    }

    // AABB vs Circle
    private static Vector2 AabbVsCircle(AxisAlignedRectangle rect, Circle circle)
    {
        float rx = rect.AbsoluteX, ry = rect.AbsoluteY;
        float cx = circle.AbsoluteX, cy = circle.AbsoluteY;
        float hw = rect.Width / 2f, hh = rect.Height / 2f;

        float clampedX = System.Math.Clamp(cx, rx - hw, rx + hw);
        float clampedY = System.Math.Clamp(cy, ry - hh, ry + hh);

        float dx = cx - clampedX;
        float dy = cy - clampedY;
        float distSq = dx * dx + dy * dy;

        if (distSq >= circle.Radius * circle.Radius) return Vector2.Zero;

        float dist = MathF.Sqrt(distSq);
        if (dist < 1e-6f)
        {
            // Circle center is inside rect — push out shortest axis
            float overlapX = hw + circle.Radius - MathF.Abs(cx - rx);
            float overlapY = hh + circle.Radius - MathF.Abs(cy - ry);
            if (overlapX < overlapY)
                return new Vector2(cx < rx ? -overlapX : overlapX, 0f);
            else
                return new Vector2(0f, cy < ry ? -overlapY : overlapY);
        }

        float overlap = circle.Radius - dist;
        return new Vector2(dx / dist * overlap, dy / dist * overlap);
    }

    // Polygon vs Polygon (SAT)
    private static Vector2 PolygonVsPolygon(Polygon a, Polygon b)
    {
        var axesA = GetAxes(a);
        var axesB = GetAxes(b);
        var worldPointsA = GetWorldPoints(a);
        var worldPointsB = GetWorldPoints(b);

        Vector2 minMtv = Vector2.Zero;
        float minOverlap = float.MaxValue;

        foreach (var axis in axesA)
        {
            if (!SatOverlap(worldPointsA, worldPointsB, axis, out float overlap, out bool flip))
                return Vector2.Zero;
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                minMtv = flip ? -axis * overlap : axis * overlap;
            }
        }
        foreach (var axis in axesB)
        {
            if (!SatOverlap(worldPointsA, worldPointsB, axis, out float overlap, out bool flip))
                return Vector2.Zero;
            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                minMtv = flip ? -axis * overlap : axis * overlap;
            }
        }
        return minMtv;
    }

    // Polygon vs AABB — convert AABB to polygon axes and use SAT
    private static Vector2 PolygonVsAabb(Polygon poly, AxisAlignedRectangle rect)
    {
        var polyPoints = GetWorldPoints(poly);
        var rectPoints = GetAabbPoints(rect);
        var axesPoly = GetAxes(poly);
        Vector2[] axesRect = { new Vector2(1, 0), new Vector2(0, 1) };

        Vector2 minMtv = Vector2.Zero;
        float minOverlap = float.MaxValue;

        foreach (var axis in axesPoly)
        {
            if (!SatOverlap(polyPoints, rectPoints, axis, out float overlap, out bool flip))
                return Vector2.Zero;
            if (overlap < minOverlap) { minOverlap = overlap; minMtv = flip ? -axis * overlap : axis * overlap; }
        }
        foreach (var axis in axesRect)
        {
            if (!SatOverlap(polyPoints, rectPoints, axis, out float overlap, out bool flip))
                return Vector2.Zero;
            if (overlap < minOverlap) { minOverlap = overlap; minMtv = flip ? -axis * overlap : axis * overlap; }
        }
        return minMtv;
    }

    // Polygon vs Circle (SAT + circle-center axis)
    private static Vector2 PolygonVsCircle(Polygon poly, Circle circle)
    {
        var polyPoints = GetWorldPoints(poly);
        var axes = new List<Vector2>(GetAxes(poly));

        // Add axis from poly to circle center
        var closest = ClosestPointOnPoly(polyPoints, new Vector2(circle.AbsoluteX, circle.AbsoluteY));
        var toCircle = new Vector2(circle.AbsoluteX - closest.X, circle.AbsoluteY - closest.Y);
        float len = toCircle.Length();
        if (len > 1e-6f) axes.Add(toCircle / len);

        Vector2 minMtv = Vector2.Zero;
        float minOverlap = float.MaxValue;

        foreach (var axis in axes)
        {
            ProjectPoly(polyPoints, axis, out float polyMin, out float polyMax);
            float circleCenter = Vector2.Dot(new Vector2(circle.AbsoluteX, circle.AbsoluteY), axis);
            float circMin = circleCenter - circle.Radius;
            float circMax = circleCenter + circle.Radius;

            float overlap = MathF.Min(polyMax, circMax) - MathF.Max(polyMin, circMin);
            if (overlap <= 0) return Vector2.Zero;

            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                // Determine direction: push poly away from circle
                float polyCenter = (polyMin + polyMax) / 2f;
                bool flip = polyCenter < circleCenter;
                minMtv = flip ? axis * overlap : -axis * overlap;
            }
        }
        return minMtv;
    }

    private static IEnumerable<Vector2> GetAxes(Polygon poly)
    {
        var pts = GetWorldPoints(poly);
        for (int i = 0; i < pts.Length; i++)
        {
            var edge = pts[(i + 1) % pts.Length] - pts[i];
            var normal = new Vector2(-edge.Y, edge.X);
            float len = normal.Length();
            if (len > 1e-6f) yield return normal / len;
        }
    }

    private static Vector2[] GetWorldPoints(Polygon poly)
    {
        var cos = MathF.Cos(poly.AbsoluteRotation.Radians);
        var sin = MathF.Sin(poly.AbsoluteRotation.Radians);
        var pts = poly.Points;
        var result = new Vector2[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            float lx = pts[i].X, ly = pts[i].Y;
            result[i] = new Vector2(
                poly.AbsoluteX + lx * cos - ly * sin,
                poly.AbsoluteY + lx * sin + ly * cos);
        }
        return result;
    }

    private static Vector2[] GetAabbPoints(AxisAlignedRectangle rect)
    {
        float hw = rect.Width / 2f, hh = rect.Height / 2f;
        float cx = rect.AbsoluteX, cy = rect.AbsoluteY;
        return new[]
        {
            new Vector2(cx - hw, cy - hh),
            new Vector2(cx + hw, cy - hh),
            new Vector2(cx + hw, cy + hh),
            new Vector2(cx - hw, cy + hh)
        };
    }

    private static bool SatOverlap(Vector2[] a, Vector2[] b, Vector2 axis, out float overlap, out bool flip)
    {
        ProjectPoly(a, axis, out float aMin, out float aMax);
        ProjectPoly(b, axis, out float bMin, out float bMax);
        overlap = MathF.Min(aMax, bMax) - MathF.Max(aMin, bMin);
        flip = (aMin + aMax) / 2f > (bMin + bMax) / 2f;
        return overlap > 0;
    }

    private static void ProjectPoly(Vector2[] pts, Vector2 axis, out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;
        foreach (var p in pts)
        {
            float proj = Vector2.Dot(p, axis);
            if (proj < min) min = proj;
            if (proj > max) max = proj;
        }
    }

    private static Vector2 ClosestPointOnPoly(Vector2[] pts, Vector2 point)
    {
        Vector2 closest = pts[0];
        float minDist = float.MaxValue;
        for (int i = 0; i < pts.Length; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Length];
            var ab = b - a;
            float t = MathF.Max(0, MathF.Min(1, Vector2.Dot(point - a, ab) / Vector2.Dot(ab, ab)));
            var proj = a + ab * t;
            float dist = (point - proj).LengthSquared();
            if (dist < minDist) { minDist = dist; closest = proj; }
        }
        return closest;
    }
}
