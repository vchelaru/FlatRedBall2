using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure, SkiaSharp-free hit-tester for collision shapes in the preview panel.
/// All coordinates are in screen space (already transformed from world space).
/// </summary>
public static class PreviewShapeHitTester
{
    /// <summary>
    /// Returns <c>true</c> if the click point is inside or within
    /// <paramref name="tolerance"/> pixels of a circle's edge.
    /// </summary>
    public static bool HitsCircle(
        float ptX, float ptY,
        float centerX, float centerY,
        float screenRadius,
        float tolerance = 5f)
    {
        float dx = ptX - centerX;
        float dy = ptY - centerY;
        float limit = screenRadius + tolerance;
        return dx * dx + dy * dy <= limit * limit;
    }

    /// <summary>
    /// Returns <c>true</c> if the click point is inside or within
    /// <paramref name="tolerance"/> pixels of an axis-aligned rectangle's edge.
    /// </summary>
    public static bool HitsRect(
        float ptX, float ptY,
        float centerX, float centerY,
        float halfW, float halfH,
        float tolerance = 5f)
    {
        return MathF.Abs(ptX - centerX) <= halfW + tolerance
            && MathF.Abs(ptY - centerY) <= halfH + tolerance;
    }

    /// <summary>
    /// Returns <c>true</c> if the click point is inside the polygon (even-odd rule, so concave
    /// outlines work) or within <paramref name="tolerance"/> pixels of one of its edges. The
    /// outline is treated as closed for the inside test even when the polygon is open.
    /// </summary>
    public static bool HitsPolygon(
        float ptX, float ptY,
        IReadOnlyList<(float X, float Y)> vertices,
        float tolerance = 5f)
    {
        int n = vertices.Count;
        if (n == 0) return false;
        bool inside = false;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            var (xi, yi) = vertices[i];
            var (xj, yj) = vertices[j];
            if ((yi > ptY) != (yj > ptY) && ptX < (xj - xi) * (ptY - yi) / (yj - yi) + xi)
                inside = !inside;
            if (DistanceToSegment(ptX, ptY, xj, yj, xi, yi) <= tolerance)
                return true;
        }
        return inside;
    }

    /// <summary>Index of the vertex within <paramref name="radius"/> pixels of the point (the last one wins), or -1.</summary>
    public static int HitVertex(float ptX, float ptY, IReadOnlyList<(float X, float Y)> vertices, float radius)
    {
        for (int i = vertices.Count - 1; i >= 0; i--)
        {
            float dx = ptX - vertices[i].X, dy = ptY - vertices[i].Y;
            if (dx * dx + dy * dy <= radius * radius) return i;
        }
        return -1;
    }

    /// <summary>
    /// Index of the edge whose midpoint is within <paramref name="radius"/> pixels of the point, or
    /// -1. Edge <c>i</c> runs from vertex <c>i</c> to vertex <c>i + 1</c>; a closed outline also has
    /// the edge from the last vertex back to the first.
    /// </summary>
    public static int HitEdgeMidpoint(float ptX, float ptY, IReadOnlyList<(float X, float Y)> vertices,
        bool closed, float radius)
    {
        int edges = closed ? vertices.Count : vertices.Count - 1;
        for (int i = edges - 1; i >= 0; i--)
        {
            var (ax, ay) = vertices[i];
            var (bx, by) = vertices[(i + 1) % vertices.Count];
            float dx = ptX - (ax + bx) / 2f, dy = ptY - (ay + by) / 2f;
            if (dx * dx + dy * dy <= radius * radius) return i;
        }
        return -1;
    }

    private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        float abx = bx - ax, aby = by - ay;
        float lengthSquared = abx * abx + aby * aby;
        float t = lengthSquared == 0f ? 0f : Math.Clamp(((px - ax) * abx + (py - ay) * aby) / lengthSquared, 0f, 1f);
        float dx = px - (ax + t * abx), dy = py - (ay + t * aby);
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
