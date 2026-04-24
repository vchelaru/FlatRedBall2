using System;
using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2.Math;

namespace FlatRedBall2.Collision;

/// <summary>
/// Central dispatch table for shape-pair collision. Resolves any
/// <see cref="ICollidable"/> pair to the correct narrow-phase routine
/// (AABB vs AABB, circle vs polygon, line vs AABB, etc.) and returns either an overlap test
/// or a minimum translation vector (MTV) suitable for separation.
/// <para>
/// Internal — game code should use the methods on <see cref="Entity"/>, the shape classes
/// themselves, or a configured <see cref="CollisionRelationship{A,B}"/> rather than calling
/// the dispatcher directly.
/// </para>
/// </summary>
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

    /// <summary>
    /// Returns true if the two shapes overlap or are touching.
    /// </summary>
    public static bool CollidesWith(ICollidable a, ICollidable b)
    {
        // Line vs AARect now has separation support — use the standard path.
        // For other Line pairs (Line vs Line, Line vs Circle, Line vs Polygon),
        // delegate to Line's own intersection test since no separation exists.
        if (a is Line la && b is not AxisAlignedRectangle) return la.CollidesWith(b);
        if (b is Line lb && a is not AxisAlignedRectangle) return lb.CollidesWith(a);
        // TileShapeCollection.CollidesWith uses a direct cell-occupancy scan that is
        // correct even when the caller is fully surrounded (net separation == zero).
        if (a is TileShapeCollection tsca) return tsca.CollidesWith(b);
        if (b is TileShapeCollection tscb) return tscb.CollidesWith(a);
        return GetSeparationVector(a, b) != Vector2.Zero || PointsOverlap(a, b);
    }

    /// <summary>
    /// Computes the separation vector required to push object 'a' out of 'b'.
    /// </summary>
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
            (Line la, AxisAlignedRectangle rb)                 => LineVsAabb(la, rb),
            (AxisAlignedRectangle ra, Line lb)                 => -LineVsAabb(lb, ra),
            (_, TileShapeCollection tsc)                       => tsc.GetSeparationFor(a),
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

        float bCenterX = (bLeft + bRight) / 2f;
        float bCenterY = (bBottom + bTop) / 2f;

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
                    // Only push down if the circle center is below the tile's center.
                    // This prevents corner-clips from above triggering a downward push.
                    if (cy >= bCenterY) return Vector2.Zero;
                    // Exit through the bottom face. Circle center lands at rect.bottom − √(r²−dx²).
                    // If dx ≥ r the circle can't reach the bottom face — no separation possible.
                    if (dx >= r) return Vector2.Zero;
                    float targetDown = bBottom - MathF.Sqrt(r * r - dx * dx);
                    float deltaDown  = targetDown - cy;
                    return deltaDown < 0f ? new Vector2(0f, deltaDown) : Vector2.Zero;

                case RepositionDirections.Up:
                    if (cy <= bCenterY) return Vector2.Zero;
                    if (dx >= r) return Vector2.Zero;
                    float targetUp  = bTop + MathF.Sqrt(r * r - dx * dx);
                    float deltaUp   = targetUp - cy;
                    return deltaUp > 0f ? new Vector2(0f, deltaUp) : Vector2.Zero;

                case RepositionDirections.Left:
                    if (cx >= bCenterX) return Vector2.Zero;
                    // Exit through the left face. Circle center lands at rect.left − √(r²−dy²).
                    if (dy >= r) return Vector2.Zero;
                    float targetLeft  = bLeft - MathF.Sqrt(r * r - dy * dy);
                    float deltaLeft   = targetLeft - cx;
                    return deltaLeft < 0f ? new Vector2(deltaLeft, 0f) : Vector2.Zero;

                case RepositionDirections.Right:
                    if (cx <= bCenterX) return Vector2.Zero;
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
            float aCenterX = (aMinX + aMaxX) / 2f;
            float aCenterY = (aMinY + aMaxY) / 2f;
            // Only push in a direction if the moving object's center is on the outward side of the
            // tile's center. This prevents corner-clips from triggering spurious repositions — e.g.,
            // a player whose bottom-right corner barely overlaps the top-left of a Right-only tile
            // should not be pushed rightward.
            switch (dir)
            {
                case RepositionDirections.Down:
                    if (aCenterY >= bCenterY) return Vector2.Zero;
                    float sepD = bBottom - aMaxY;
                    return sepD < 0f ? new Vector2(0f, sepD) : Vector2.Zero;
                case RepositionDirections.Up:
                    if (aCenterY <= bCenterY) return Vector2.Zero;
                    float sepU = bTop - aMinY;
                    return sepU > 0f ? new Vector2(0f, sepU) : Vector2.Zero;
                case RepositionDirections.Left:
                    if (aCenterX >= bCenterX) return Vector2.Zero;
                    float sepL = bLeft - aMaxX;
                    return sepL < 0f ? new Vector2(sepL, 0f) : Vector2.Zero;
                case RepositionDirections.Right:
                    if (aCenterX <= bCenterX) return Vector2.Zero;
                    float sepR = bRight - aMinX;
                    return sepR > 0f ? new Vector2(sepR, 0f) : Vector2.Zero;
            }
        }
        return Vector2.Zero;
    }

    // Returns the axis-aligned bounding box of any supported ICollidable.
    // Internal so TileShapeCollection can use it for spatial partitioning.
    internal static (float minX, float maxX, float minY, float maxY) GetBounds(ICollidable c)
    {
        switch (c)
        {
            case AxisAlignedRectangle r:
                return (r.AbsoluteX - r.Width / 2f,  r.AbsoluteX + r.Width / 2f,
                        r.AbsoluteY - r.Height / 2f, r.AbsoluteY + r.Height / 2f);
            case Circle ci:
                return (ci.AbsoluteX - ci.Radius, ci.AbsoluteX + ci.Radius,
                        ci.AbsoluteY - ci.Radius, ci.AbsoluteY + ci.Radius);
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
            case Line line:
            {
                var p1 = line.AbsolutePoint1;
                var p2 = line.AbsolutePoint2;
                return (MathF.Min(p1.X, p2.X), MathF.Max(p1.X, p2.X),
                        MathF.Min(p1.Y, p2.Y), MathF.Max(p1.Y, p2.Y));
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

    // Line segment vs AABB — treats the segment as a degenerate AABB (its bounding box).
    // Exact for axis-aligned lines; bounding-box approximation for diagonal lines.
    private static Vector2 LineVsAabb(Line line, AxisAlignedRectangle rect)
    {
        // First verify actual intersection (the segment might miss the rect even if bounding boxes overlap).
        if (!line.CollideAgainst(rect)) return Vector2.Zero;

        var p1 = line.AbsolutePoint1;
        var p2 = line.AbsolutePoint2;
        float lineMinX = MathF.Min(p1.X, p2.X), lineMaxX = MathF.Max(p1.X, p2.X);
        float lineMinY = MathF.Min(p1.Y, p2.Y), lineMaxY = MathF.Max(p1.Y, p2.Y);
        float lineCenterX = (lineMinX + lineMaxX) / 2f;
        float lineCenterY = (lineMinY + lineMaxY) / 2f;
        float lineHalfW = (lineMaxX - lineMinX) / 2f;
        float lineHalfH = (lineMaxY - lineMinY) / 2f;

        float rectHW = rect.Width / 2f, rectHH = rect.Height / 2f;

        float overlapX = (lineHalfW + rectHW) - MathF.Abs(lineCenterX - rect.AbsoluteX);
        float overlapY = (lineHalfH + rectHH) - MathF.Abs(lineCenterY - rect.AbsoluteY);

        if (overlapX <= 0 || overlapY <= 0) return Vector2.Zero;

        if (overlapX < overlapY)
            return new Vector2(lineCenterX < rect.AbsoluteX ? -overlapX : overlapX, 0f);
        else
            return new Vector2(0f, lineCenterY < rect.AbsoluteY ? -overlapY : overlapY);
    }

    // Polygon vs Polygon — iterate convex parts of each, return minimum-magnitude MTV.
    private static Vector2 PolygonVsPolygon(Polygon a, Polygon b)
    {
        Vector2 bestMtv = Vector2.Zero;
        float bestMagSq = float.MaxValue;

        foreach (var partA in GetConvexPartsWorldPoints(a))
        foreach (var partB in GetConvexPartsWorldPoints(b))
        {
            var mtv = ConvexVsConvex(partA, partB);
            if (mtv == Vector2.Zero) continue;
            float magSq = mtv.LengthSquared();
            if (magSq < bestMagSq) { bestMagSq = magSq; bestMtv = mtv; }
        }
        return bestMtv;
    }

    // Polygon vs AABB — iterate convex parts, AABB is always convex.
    private static Vector2 PolygonVsAabb(Polygon poly, AxisAlignedRectangle rect)
    {
        var rectPoints = GetAabbPoints(rect);
        Vector2[] axesRect = { new Vector2(1, 0), new Vector2(0, 1) };

        Vector2 bestMtv = Vector2.Zero;
        float bestMagSq = float.MaxValue;

        foreach (var part in GetConvexPartsWorldPoints(poly))
        {
            var mtv = ConvexVsAabbPoints(part, rectPoints, axesRect);
            if (mtv == Vector2.Zero) continue;
            float magSq = mtv.LengthSquared();
            if (magSq < bestMagSq) { bestMagSq = magSq; bestMtv = mtv; }
        }
        return bestMtv;
    }

    // Polygon vs Circle — iterate convex parts, circle is always convex.
    private static Vector2 PolygonVsCircle(Polygon poly, Circle circle)
    {
        var circleCenter = new Vector2(circle.AbsoluteX, circle.AbsoluteY);

        Vector2 bestMtv = Vector2.Zero;
        float bestMagSq = float.MaxValue;

        foreach (var part in GetConvexPartsWorldPoints(poly))
        {
            var mtv = ConvexPartVsCircle(part, circle, circleCenter);
            if (mtv == Vector2.Zero) continue;
            float magSq = mtv.LengthSquared();
            if (magSq < bestMagSq) { bestMagSq = magSq; bestMtv = mtv; }
        }
        return bestMtv;
    }

    // Transforms each convex part of a polygon from local to world space.
    private static IEnumerable<Vector2[]> GetConvexPartsWorldPoints(Polygon poly)
    {
        float cos = MathF.Cos(poly.AbsoluteRotation.Radians);
        float sin = MathF.Sin(poly.AbsoluteRotation.Radians);
        float ax = poly.AbsoluteX, ay = poly.AbsoluteY;

        foreach (var part in poly.ConvexParts)
        {
            var worldPts = new Vector2[part.Count];
            for (int i = 0; i < part.Count; i++)
            {
                float lx = part[i].X, ly = part[i].Y;
                worldPts[i] = new Vector2(ax + lx * cos - ly * sin, ay + lx * sin + ly * cos);
            }
            yield return worldPts;
        }
    }

    // SAT for two convex polygons given as world-space point arrays.
    private static Vector2 ConvexVsConvex(Vector2[] a, Vector2[] b)
    {
        Vector2 minMtv = Vector2.Zero;
        float minOverlap = float.MaxValue;

        foreach (var axis in GetAxesFromPoints(a))
        {
            if (!SatOverlap(a, b, axis, out float overlap, out bool flip)) return Vector2.Zero;
            if (overlap < minOverlap) { minOverlap = overlap; minMtv = flip ? axis * overlap : -axis * overlap; }
        }
        foreach (var axis in GetAxesFromPoints(b))
        {
            if (!SatOverlap(a, b, axis, out float overlap, out bool flip)) return Vector2.Zero;
            if (overlap < minOverlap) { minOverlap = overlap; minMtv = flip ? axis * overlap : -axis * overlap; }
        }
        return minMtv;
    }

    // SAT for a convex polygon part vs a set of AABB points with known axes.
    private static Vector2 ConvexVsAabbPoints(Vector2[] part, Vector2[] rectPoints, Vector2[] axesRect)
    {
        Vector2 minMtv = Vector2.Zero;
        float minOverlap = float.MaxValue;

        foreach (var axis in GetAxesFromPoints(part))
        {
            if (!SatOverlap(part, rectPoints, axis, out float overlap, out bool flip)) return Vector2.Zero;
            if (overlap < minOverlap) { minOverlap = overlap; minMtv = flip ? axis * overlap : -axis * overlap; }
        }
        foreach (var axis in axesRect)
        {
            if (!SatOverlap(part, rectPoints, axis, out float overlap, out bool flip)) return Vector2.Zero;
            if (overlap < minOverlap) { minOverlap = overlap; minMtv = flip ? axis * overlap : -axis * overlap; }
        }
        return minMtv;
    }

    // SAT for a convex polygon part vs a circle (SAT + closest-point axis).
    private static Vector2 ConvexPartVsCircle(Vector2[] partPoints, Circle circle, Vector2 circleCenter)
    {
        var axes = new List<Vector2>(GetAxesFromPoints(partPoints));

        // Add axis from the closest point on the part to the circle center.
        var closest = ClosestPointOnPoly(partPoints, circleCenter);
        var toCircle = circleCenter - closest;
        float len = toCircle.Length();
        if (len > 1e-6f) axes.Add(toCircle / len);

        Vector2 minMtv = Vector2.Zero;
        float minOverlap = float.MaxValue;

        foreach (var axis in axes)
        {
            ProjectPoly(partPoints, axis, out float polyMin, out float polyMax);
            float circC = Vector2.Dot(circleCenter, axis);
            float circMin = circC - circle.Radius;
            float circMax = circC + circle.Radius;

            float overlap = MathF.Min(polyMax, circMax) - MathF.Max(polyMin, circMin);
            if (overlap <= 0) return Vector2.Zero;

            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                float polyCenter = (polyMin + polyMax) / 2f;
                bool flip = polyCenter < circC;
                minMtv = flip ? -axis * overlap : axis * overlap;
            }
        }
        return minMtv;
    }

    // Returns outward-facing edge normals for a convex polygon (world-space points).
    private static IEnumerable<Vector2> GetAxesFromPoints(Vector2[] pts)
    {
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
