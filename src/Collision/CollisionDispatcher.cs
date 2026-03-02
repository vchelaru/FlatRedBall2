using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Math;

namespace FlatRedBall2.Collision;

internal static class CollisionDispatcher
{
    /// <summary>
    /// Computes how far to move an object with <paramref name="thisMass"/> out of a collision,
    /// given the full separation vector and the mass of the other party.
    /// The result is the fraction of the separation each object absorbs, weighted by mass:
    /// a wall (otherMass = 0) absorbs none, so the moving object absorbs all of it.
    /// Returns Vector2.Zero if there is no collision or total mass is zero.
    /// </summary>
    internal static Vector2 ComputeSeparationOffset(Vector2 sep, float thisMass, float otherMass)
    {
        if (sep == Vector2.Zero) return Vector2.Zero;
        float total = thisMass + otherMass;
        if (total == 0) return Vector2.Zero;
        return sep * (otherMass / total);
    }

    public static bool CollidesWith(ICollidable a, ICollidable b)
        => GetSeparationVector(a, b) != Vector2.Zero || PointsOverlap(a, b);

    // Returns the displacement needed to move 'a' out of 'b', respecting b's RepositionDirections.
    // Returns Vector2.Zero if no collision.
    // When b has RepositionDirections != All, computes the minimum displacement restricted to
    // each allowed axis and returns the smallest one — so a left-edge hit against a Down-only
    // rect pushes the object downward rather than being suppressed. For circles, the exact
    // circle-arc geometry is used; the object is never treated as a bounding box.
    public static Vector2 GetSeparationVector(ICollidable a, ICollidable b)
    {
        var mtv = (a, b) switch
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

        if (mtv == Vector2.Zero) return Vector2.Zero;

        if (b is AxisAlignedRectangle rectB && rectB.RepositionDirections != RepositionDirections.All)
            mtv = ComputeDirectionalSeparation(a, rectB);

        return mtv;
    }

    // Computes the minimum displacement for 'a' restricted to b's allowed axes.
    // Tries every allowed direction and returns the one with the smallest magnitude.
    private static Vector2 ComputeDirectionalSeparation(ICollidable a, AxisAlignedRectangle b)
    {
        var dirs = b.RepositionDirections;
        if (dirs == RepositionDirections.None) return Vector2.Zero;

        Vector2 best = Vector2.Zero;
        float bestMag = float.MaxValue;

        if (dirs.HasFlag(RepositionDirections.Down))  TryAxis(ComputeAxisSeparation(a, b, RepositionDirections.Down),  ref best, ref bestMag);
        if (dirs.HasFlag(RepositionDirections.Up))    TryAxis(ComputeAxisSeparation(a, b, RepositionDirections.Up),    ref best, ref bestMag);
        if (dirs.HasFlag(RepositionDirections.Left))  TryAxis(ComputeAxisSeparation(a, b, RepositionDirections.Left),  ref best, ref bestMag);
        if (dirs.HasFlag(RepositionDirections.Right)) TryAxis(ComputeAxisSeparation(a, b, RepositionDirections.Right), ref best, ref bestMag);

        return best;

        static void TryAxis(Vector2 sep, ref Vector2 best, ref float bestMag)
        {
            if (sep == Vector2.Zero) return;
            float mag = MathF.Abs(sep.X) + MathF.Abs(sep.Y); // one component is always zero
            if (mag < bestMag) { best = sep; bestMag = mag; }
        }
    }

    // Returns the minimum displacement to push 'a' out of 'b' along one specific axis direction.
    // For circles the exact arc formula is used: targetCenter = face ± √(r²−d²) where d is the
    // perpendicular distance from the circle center to the face being exited.
    // For other shape types the AABB bounding-box approximation is used (exact for rects).
    private static Vector2 ComputeAxisSeparation(ICollidable a, AxisAlignedRectangle b, RepositionDirections dir)
    {
        float bLeft   = b.AbsoluteX - b.Width  / 2f;
        float bRight  = b.AbsoluteX + b.Width  / 2f;
        float bBottom = b.AbsoluteY - b.Height / 2f;
        float bTop    = b.AbsoluteY + b.Height / 2f;

        if (a is Circle circle)
        {
            float cx = circle.AbsoluteX, cy = circle.AbsoluteY, r = circle.Radius;
            // Perpendicular distances from circle center to the rect's nearest edges on each axis.
            float px = System.Math.Clamp(cx, bLeft, bRight);
            float py = System.Math.Clamp(cy, bBottom, bTop);
            float dx = MathF.Abs(cx - px); // 0 when center is inside rect's X span
            float dy = MathF.Abs(cy - py); // 0 when center is inside rect's Y span

            switch (dir)
            {
                case RepositionDirections.Down:
                    // Exit through the bottom face. Circle center lands at rect.bottom − √(r²−dx²).
                    // If dx ≥ r the circle can't reach the bottom face — no separation possible.
                    if (dx >= r) return Vector2.Zero;
                    float targetDown = bBottom - MathF.Sqrt(r * r - dx * dx);
                    float deltaDown  = targetDown - cy;
                    return deltaDown < 0f ? new Vector2(0f, deltaDown) : Vector2.Zero;

                case RepositionDirections.Up:
                    if (dx >= r) return Vector2.Zero;
                    float targetUp  = bTop + MathF.Sqrt(r * r - dx * dx);
                    float deltaUp   = targetUp - cy;
                    return deltaUp > 0f ? new Vector2(0f, deltaUp) : Vector2.Zero;

                case RepositionDirections.Left:
                    // Exit through the left face. Circle center lands at rect.left − √(r²−dy²).
                    if (dy >= r) return Vector2.Zero;
                    float targetLeft  = bLeft - MathF.Sqrt(r * r - dy * dy);
                    float deltaLeft   = targetLeft - cx;
                    return deltaLeft < 0f ? new Vector2(deltaLeft, 0f) : Vector2.Zero;

                case RepositionDirections.Right:
                    if (dy >= r) return Vector2.Zero;
                    float targetRight = bRight + MathF.Sqrt(r * r - dy * dy);
                    float deltaRight  = targetRight - cx;
                    return deltaRight > 0f ? new Vector2(deltaRight, 0f) : Vector2.Zero;
            }
        }
        else
        {
            // AABB bounding-box approach — exact for AxisAlignedRectangle, approximate for Polygon.
            var (aMinX, aMaxX, aMinY, aMaxY) = GetBounds(a);
            switch (dir)
            {
                case RepositionDirections.Down:
                    float sepD = bBottom - aMaxY;
                    return sepD < 0f ? new Vector2(0f, sepD) : Vector2.Zero;
                case RepositionDirections.Up:
                    float sepU = bTop - aMinY;
                    return sepU > 0f ? new Vector2(0f, sepU) : Vector2.Zero;
                case RepositionDirections.Left:
                    float sepL = bLeft - aMaxX;
                    return sepL < 0f ? new Vector2(sepL, 0f) : Vector2.Zero;
                case RepositionDirections.Right:
                    float sepR = bRight - aMinX;
                    return sepR > 0f ? new Vector2(sepR, 0f) : Vector2.Zero;
            }
        }
        return Vector2.Zero;
    }

    // Returns the axis-aligned bounding box of any supported ICollidable.
    private static (float minX, float maxX, float minY, float maxY) GetBounds(ICollidable c)
    {
        switch (c)
        {
            case AxisAlignedRectangle r:
                return (r.AbsoluteX - r.Width / 2f,  r.AbsoluteX + r.Width / 2f,
                        r.AbsoluteY - r.Height / 2f, r.AbsoluteY + r.Height / 2f);
            case Polygon poly:
            {
                var pts = GetWorldPoints(poly);
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var pt in pts)
                {
                    if (pt.X < minX) minX = pt.X;
                    if (pt.X > maxX) maxX = pt.X;
                    if (pt.Y < minY) minY = pt.Y;
                    if (pt.Y > maxY) maxY = pt.Y;
                }
                return (minX, maxX, minY, maxY);
            }
            default:
                return (0f, 0f, 0f, 0f);
        }
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
