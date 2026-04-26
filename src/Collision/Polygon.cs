using System;
using System.Collections.Generic;
using System.Linq;
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

/// <summary>
/// An arbitrary 2D polygon, convex or concave (simple, non-self-intersecting). Participates in
/// collision via SAT and is rendered as a filled or outlined shape.
/// </summary>
/// <remarks>
/// Points are stored in local space; world position is <see cref="AbsoluteX"/>/<see cref="AbsoluteY"/>
/// plus its own <see cref="Rotation"/>. Concave inputs are decomposed into convex parts
/// (<see cref="ConvexParts"/>) on construction so SAT collision behaves correctly without manual setup.
/// Construct via <see cref="FromPoints"/> or <see cref="CreateRectangle"/> rather than mutating
/// <see cref="Points"/> after the fact — use <see cref="SetPoints"/> to replace the geometry, which
/// rebuilds the convex decomposition.
/// </remarks>
public class Polygon : IAttachable, IRenderable, ICollidable
{
    private readonly List<Vector2> _points = new();
    private List<IReadOnlyList<Vector2>> _convexParts = new();

    /// <summary>
    /// The polygon's vertices in local (unrotated, unpositioned) space. Read-only — call
    /// <see cref="SetPoints"/> to change the geometry so the convex decomposition is rebuilt.
    /// </summary>
    public IReadOnlyList<Vector2> Points => _points;

    /// <summary>
    /// Bitfield where bit <c>i</c> suppresses edge <c>i</c>'s normal during SAT collision.
    /// Edge <c>i</c> connects <c>Points[i]</c> to <c>Points[(i+1) % Points.Count]</c>.
    /// Used by <see cref="TileShapeCollection"/> to eliminate snagging at seams between
    /// adjacent polygon tiles, analogous to <see cref="AxisAlignedRectangle.RepositionDirections"/>
    /// for rectangle tiles.
    /// </summary>
    internal int SuppressedEdges { get; set; }

    /// <summary>
    /// The convex sub-polygons that tile this polygon's area, in local (unrotated, unpositioned) space.
    /// For convex polygons this contains a single entry equal to <see cref="Points"/>.
    /// For concave polygons this is the Hertel-Mehlhorn convex decomposition produced from ear-clip triangulation.
    /// Collision detection uses these parts so that concave shapes behave correctly.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> ConvexParts => _convexParts;

    /// <summary>
    /// Rotation about the Z axis applied to <see cref="Points"/>. Relative to <see cref="Parent"/>
    /// when attached, world when root.
    /// </summary>
    public Angle Rotation { get; set; }

    /// <summary>
    /// Final world-space rotation after walking the parent chain.
    /// Equal to <see cref="Rotation"/> when this polygon has no parent.
    /// </summary>
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;


    /// <summary>
    /// Creates a rectangular polygon centered on the origin with the given <paramref name="width"/>
    /// and <paramref name="height"/>. Identical in geometry to <see cref="AxisAlignedRectangle"/>
    /// when unrotated; use this when you need a rectangle that can rotate or compose with concave shapes.
    /// </summary>
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
        poly.BuildConvexParts();
        return poly;
    }

    /// <summary>
    /// Creates a polygon from an arbitrary list of points. Both convex and concave (non-convex)
    /// polygons are supported for rendering and collision.
    /// </summary>
    /// <remarks>
    /// Concave polygons are automatically decomposed into convex parts via ear-clip triangulation
    /// followed by Hertel-Mehlhorn merging. Collision detection operates on those convex parts,
    /// so concave shapes respond correctly without any manual decomposition.
    /// </remarks>
    public static Polygon FromPoints(IEnumerable<Vector2> points)
    {
        var poly = new Polygon();
        poly._points.AddRange(points);
        poly.BuildConvexParts();
        return poly;
    }

    /// <summary>
    /// Replaces all points with <paramref name="points"/>. Points are relative to the polygon's position.
    /// </summary>
    public void SetPoints(IEnumerable<Vector2> points)
    {
        _points.Clear();
        _points.AddRange(points);
        BuildConvexParts();
    }

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
    public float BroadPhaseRadius
    {
        get
        {
            float max = 0f;
            foreach (var p in _points)
            {
                float d = MathF.Sqrt(p.X * p.X + p.Y * p.Y);
                if (d > max) max = d;
            }
            return max;
        }
    }

    // IRenderable
    /// <summary>Whether this polygon is drawn. Defaults to <c>false</c> — collision shapes are hidden by default.</summary>
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
    /// <summary>When <c>true</c>, the polygon renders as a filled triangulated mesh; when <c>false</c>, as an outline.</summary>
    public bool IsFilled { get; set; } = true;
    /// <summary>Outline thickness in pixels when <see cref="IsFilled"/> is <c>false</c>.</summary>
    public float OutlineThickness { get; set; } = 2f;

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Batch is not ShapesBatch sb || _points.Count < 2) return;

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

    // ── Convex decomposition ──────────────────────────────────────────────────

    private void BuildConvexParts()
    {
        _convexParts = new List<IReadOnlyList<Vector2>>();
        if (_points.Count < 3)
        {
            if (_points.Count > 0) _convexParts.Add(_points.AsReadOnly());
            return;
        }

        if (IsConvexList(_points))
        {
            _convexParts.Add(_points.AsReadOnly());
            return;
        }

        var triangles = EarClipToLocalTriangles();
        _convexParts.AddRange(HertelMehlhorn(triangles));
    }

    // Returns whether a polygon (given as a list of local-space vertices, CCW winding) is convex.
    private static bool IsConvexList(IReadOnlyList<Vector2> pts)
    {
        if (pts.Count < 3) return true;
        bool hasPos = false, hasNeg = false;
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            var c = pts[(i + 2) % pts.Count];
            float cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
            if (cross > 1e-6f) hasPos = true;
            else if (cross < -1e-6f) hasNeg = true;
            if (hasPos && hasNeg) return false;
        }
        return true;
    }

    // Ear-clip the local-space _points into triangles. Returns triangle vertex triples.
    private List<Vector2[]> EarClipToLocalTriangles()
    {
        var pts = _points.ToArray();
        var result = new List<Vector2[]>();
        if (pts.Length < 3) return result;

        var idx = new List<int>(pts.Length);
        for (int i = 0; i < pts.Length; i++) idx.Add(i);

        // Ensure CCW winding (positive signed area in Y-up space).
        if (SignedArea(pts) < 0) idx.Reverse();

        int guard = idx.Count * idx.Count;
        while (idx.Count > 3 && guard-- > 0)
        {
            bool found = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int n = idx.Count;
                int prev = idx[(i - 1 + n) % n];
                int curr = idx[i];
                int next = idx[(i + 1) % n];
                if (IsEar(pts, idx, prev, curr, next))
                {
                    result.Add(new[] { pts[prev], pts[curr], pts[next] });
                    idx.RemoveAt(i);
                    found = true;
                    break;
                }
            }
            if (!found) break; // degenerate polygon — bail out
        }
        if (idx.Count == 3)
            result.Add(new[] { pts[idx[0]], pts[idx[1]], pts[idx[2]] });

        return result;
    }

    // Hertel-Mehlhorn: greedily merge adjacent convex polygons while the result stays convex.
    // Input: triangles from ear-clip (local space). Output: minimum convex polygon set.
    private static List<IReadOnlyList<Vector2>> HertelMehlhorn(List<Vector2[]> triangles)
    {
        var polys = new List<List<Vector2>>(triangles.Select(t => new List<Vector2>(t)));

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < polys.Count && !changed; i++)
            for (int j = i + 1; j < polys.Count && !changed; j++)
            {
                if (TryMerge(polys[i], polys[j], out var merged))
                {
                    polys.RemoveAt(j);
                    polys.RemoveAt(i);
                    polys.Add(merged!);
                    changed = true;
                }
            }
        }

        return polys.Select(p => (IReadOnlyList<Vector2>)p.AsReadOnly()).ToList();
    }

    // Tries to merge two convex polygons that share exactly one edge.
    // The shared edge is found when a[i] == b[(j+1)%m] and a[(i+1)%n] == b[j].
    // Returns true and sets merged if the resulting polygon is convex.
    private static bool TryMerge(List<Vector2> a, List<Vector2> b, out List<Vector2>? merged)
    {
        merged = null;
        int n = a.Count, m = b.Count;

        for (int i = 0; i < n; i++)
        for (int j = 0; j < m; j++)
        {
            if (!AreClose(a[i], b[(j + 1) % m]) || !AreClose(a[(i + 1) % n], b[j]))
                continue;

            // Build merged polygon: n-1 vertices from a (skip a[i]), m-1 vertices from b (skip b[j]).
            var candidate = new List<Vector2>(n + m - 2);
            for (int k = 1; k < n; k++) candidate.Add(a[(i + k) % n]);
            for (int k = 1; k < m; k++) candidate.Add(b[(j + k) % m]);

            if (IsConvexList(candidate)) { merged = candidate; return true; }
            return false; // shared edge found but merge would be non-convex
        }
        return false;
    }

    private static bool AreClose(Vector2 a, Vector2 b) => (a - b).LengthSquared() < 1e-10f;

    /// <summary>
    /// Detaches this polygon from its parent entity and frees its render registration.
    /// Called recursively by <see cref="Entity.Destroy"/>.
    /// </summary>
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

    /// <inheritdoc/>
    /// <remarks>Concave polygons are handled correctly: the test runs against the original
    /// (possibly concave) outline rather than the convex decomposition.</remarks>
    public bool Contains(Vector2 worldPoint)
    {
        if (_points.Count < 3) return false;

        float angle = AbsoluteRotation.Radians;
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        var worldPts = ComputeWorldPoints(cos, sin);

        // Standard horizontal-ray crossing test (Jordan curve theorem). Boundary points may
        // return either result depending on floating-point noise; that's acceptable for cursor
        // hit-testing and matches the looseness in Circle/AARect's <= boundary check.
        bool inside = false;
        for (int i = 0, j = worldPts.Length - 1; i < worldPts.Length; j = i++)
        {
            var pi = worldPts[i];
            var pj = worldPts[j];
            if (((pi.Y > worldPoint.Y) != (pj.Y > worldPoint.Y)) &&
                (worldPoint.X < (pj.X - pi.X) * (worldPoint.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
            {
                inside = !inside;
            }
        }
        return inside;
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
