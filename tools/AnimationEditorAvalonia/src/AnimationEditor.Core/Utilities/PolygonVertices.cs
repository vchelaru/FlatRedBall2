using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// The editable vertices of a <see cref="PolygonSave"/>. FRB1 closes a polygon by repeating its
/// first point at the end; this view hides that repeated point (vertex indices run 0..Count-1)
/// and keeps it equal to vertex 0 on every edit, so the file round-trips unchanged. A polygon
/// whose last point differs from its first is open, and every point is a vertex.
/// </summary>
public static class PolygonVertices
{
    /// <summary>True when the last point repeats the first (the FRB1 closed-outline convention).</summary>
    public static bool IsClosed(PolygonSave polygon)
    {
        var points = polygon.Points;
        return points.Count >= 2
            && points[0].X == points[^1].X
            && points[0].Y == points[^1].Y;
    }

    public static int Count(PolygonSave polygon) =>
        IsClosed(polygon) ? polygon.Points.Count - 1 : polygon.Points.Count;

    public static (float X, float Y) Get(PolygonSave polygon, int index) =>
        (polygon.Points[index].X, polygon.Points[index].Y);

    public static void Set(PolygonSave polygon, int index, float x, float y)
    {
        bool closed = IsClosed(polygon);
        polygon.Points[index].X = x;
        polygon.Points[index].Y = y;
        if (closed && index == 0)
        {
            polygon.Points[^1].X = x;
            polygon.Points[^1].Y = y;
        }
    }

    /// <summary>Inserts a vertex at <paramref name="index"/> (0..Count); Count appends after the last vertex.</summary>
    public static void Insert(PolygonSave polygon, int index, float x, float y)
    {
        bool closed = IsClosed(polygon);
        polygon.Points.Insert(index, new Vector2Save { X = x, Y = y });
        if (closed && index == 0)
            polygon.Points[^1] = new Vector2Save { X = x, Y = y };
    }

    public static void RemoveAt(PolygonSave polygon, int index)
    {
        bool closed = IsClosed(polygon);
        polygon.Points.RemoveAt(index);
        if (closed && index == 0 && polygon.Points.Count > 0)
        {
            polygon.Points[^1].X = polygon.Points[0].X;
            polygon.Points[^1].Y = polygon.Points[0].Y;
        }
    }

    /// <summary>Deep copy of the stored points (closing point included), for undo snapshots.</summary>
    public static List<Vector2Save> CopyPoints(PolygonSave polygon)
    {
        var copy = new List<Vector2Save>(polygon.Points.Count);
        foreach (var p in polygon.Points) copy.Add(new Vector2Save { X = p.X, Y = p.Y });
        return copy;
    }

    /// <summary>
    /// True when two non-adjacent edges of the outline cross. The runtime's <c>Polygon</c> handles
    /// concave outlines but not self-intersecting ones, so the editor warns on these.
    /// </summary>
    public static bool IsSelfIntersecting(PolygonSave polygon)
    {
        int n = Count(polygon);
        if (n < 4) return false;
        for (int i = 0; i < n; i++)
        {
            var (ax, ay) = Get(polygon, i);
            var (bx, by) = Get(polygon, (i + 1) % n);
            // Skip the edge itself and both neighbours; the first edge's other neighbour is the last edge.
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1) continue;
                var (cx, cy) = Get(polygon, j);
                var (dx, dy) = Get(polygon, (j + 1) % n);
                if (SegmentsCross(ax, ay, bx, by, cx, cy, dx, dy)) return true;
            }
        }
        return false;
    }

    private static bool SegmentsCross(float ax, float ay, float bx, float by,
        float cx, float cy, float dx, float dy)
    {
        float d1 = Cross(cx, cy, dx, dy, ax, ay);
        float d2 = Cross(cx, cy, dx, dy, bx, by);
        float d3 = Cross(ax, ay, bx, by, cx, cy);
        float d4 = Cross(ax, ay, bx, by, dx, dy);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
            && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    // Z of (b - a) x (p - a): the side of line ab that p is on.
    private static float Cross(float ax, float ay, float bx, float by, float px, float py) =>
        (bx - ax) * (py - ay) - (by - ay) * (px - ax);
}
