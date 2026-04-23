using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using XnaColor = Microsoft.Xna.Framework.Color;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Math;

/// <summary>
/// A 2D path composed of line and arc segments. Supports querying positions and tangents
/// at any distance or ratio along the path, and optionally renders as a polyline when
/// added to a screen via <c>screen.Add(path)</c>.
/// </summary>
/// <remarks>
/// Build paths fluently:
/// <code>
/// var path = new Path()
///     .MoveTo(-200, 0)
///     .LineTo(200, 0)
///     .ArcTo(200, -100, MathF.PI);
/// </code>
/// <para>
/// Arc angles are in radians; positive = CCW, negative = CW (Y+ up world convention).
/// For a CCW arc sweeping left-to-right, the path bows <b>downward</b> because the arc center
/// is placed above the chord and the minor arc curves below it. Negate the angle to bow upward.
/// </para>
/// <para>
/// Rendering and movement are independent — a <see cref="Movement.PathFollower"/> can follow
/// an invisible path, and a path can be rendered without any follower.
/// </para>
/// </remarks>
public class Path : IRenderable
{
    private enum SegmentKind { Move, Line, Arc }

    private sealed class Segment
    {
        public SegmentKind Kind;
        public Vector2 Start;
        public Vector2 End;
        // Arc-only
        public float ArcAngle;      // signed radians
        public Vector2 ArcCenter;
        public float ArcRadius;
        public float ArcStartAngle; // atan2 angle from center to Start
        public float Length;
        public Vector2[] TessPoints = Array.Empty<Vector2>(); // precomputed for rendering
    }

    private readonly List<Segment> _segments = new();
    private float _currentX;
    private float _currentY;
    private float _firstX;
    private float _firstY;
    private bool _hasFirst;
    private float _totalLength; // sum of non-Move segment lengths (excludes closing segment)

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>
    /// Total traversal length in world units.
    /// When <see cref="IsLooped"/> is true, includes the closing segment from the last point back to the first.
    /// </summary>
    public float TotalLength => _isLooped ? _totalLength + ClosingSegmentLength() : _totalLength;

    private bool _isLooped;

    /// <summary>
    /// When true, the path forms a closed loop: the last point connects back to the first.
    /// <see cref="TotalLength"/> and all query methods include the closing segment.
    /// </summary>
    public bool IsLooped
    {
        get => _isLooped;
        set => _isLooped = value;
    }

    // IRenderable
    /// <inheritdoc/>
    public float Z { get; set; }
    /// <inheritdoc/>
    public Layer? Layer { get; set; }
    /// <inheritdoc/>
    public IRenderBatch Batch { get; set; } = ShapesBatch.Instance;
    /// <inheritdoc/>
    public string? Name { get; set; }

    /// <summary>Whether the path polyline is drawn each frame. Default is <c>true</c>.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Color of the rendered polyline.</summary>
    public XnaColor Color { get; set; } = XnaColor.White;

    /// <summary>Thickness of the rendered polyline in screen pixels.</summary>
    public float LineThickness { get; set; } = 2f;

    // ── Builder methods ───────────────────────────────────────────────────

    /// <summary>
    /// Moves the path cursor to an absolute world position without adding a drawn segment.
    /// Use to start the path at a specific location or to create a gap between sub-paths.
    /// </summary>
    public Path MoveTo(float x, float y)
    {
        _segments.Add(new Segment
        {
            Kind = SegmentKind.Move,
            Start = new Vector2(x, y),
            End = new Vector2(x, y),
        });
        _currentX = x;
        _currentY = y;
        if (!_hasFirst) { _firstX = x; _firstY = y; _hasFirst = true; }
        return this;
    }

    /// <summary>Moves the path cursor by a relative offset without adding a drawn segment.</summary>
    public Path MoveBy(float dx, float dy) => MoveTo(_currentX + dx, _currentY + dy);

    /// <summary>
    /// Adds a straight line segment from the current cursor to (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public Path LineTo(float x, float y)
    {
        var start = new Vector2(_currentX, _currentY);
        var end = new Vector2(x, y);
        var len = Vector2.Distance(start, end);
        _segments.Add(new Segment
        {
            Kind = SegmentKind.Line,
            Start = start,
            End = end,
            Length = len,
            TessPoints = new[] { start, end },
        });
        _totalLength += len;
        _currentX = x;
        _currentY = y;
        if (!_hasFirst) { _firstX = x; _firstY = y; _hasFirst = true; }
        return this;
    }

    /// <summary>
    /// Adds a straight line segment offset by (<paramref name="dx"/>, <paramref name="dy"/>) from the current cursor.
    /// </summary>
    public Path LineBy(float dx, float dy) => LineTo(_currentX + dx, _currentY + dy);

    /// <summary>
    /// Adds an arc segment from the current cursor to (<paramref name="endX"/>, <paramref name="endY"/>),
    /// sweeping <paramref name="signedAngleRadians"/> radians around a computed center.
    /// </summary>
    /// <param name="endX">World X of the arc's endpoint.</param>
    /// <param name="endY">World Y of the arc's endpoint.</param>
    /// <param name="signedAngleRadians">
    /// Total arc angle swept from start to end in radians. Positive = CCW; negative = CW (Y+ up convention).
    /// The magnitude controls how much of the circle is traversed: <c>MathF.PI</c> = semicircle.
    /// <para>
    /// A full circle (start == end) cannot be defined by a single <c>ArcTo</c> call because the chord
    /// length is zero. Use two back-to-back <c>ArcTo</c> calls with <c>MathF.PI</c> each instead.
    /// </para>
    /// </param>
    public Path ArcTo(float endX, float endY, float signedAngleRadians)
    {
        var start = new Vector2(_currentX, _currentY);
        var end = new Vector2(endX, endY);
        var seg = BuildArcSegment(start, end, signedAngleRadians);
        _segments.Add(seg);
        _totalLength += seg.Length;
        _currentX = endX;
        _currentY = endY;
        if (!_hasFirst) { _firstX = endX; _firstY = endY; _hasFirst = true; }
        return this;
    }

    /// <summary>
    /// Adds an arc segment to a position offset by (<paramref name="dx"/>, <paramref name="dy"/>) from the current cursor.
    /// </summary>
    public Path ArcBy(float dx, float dy, float signedAngleRadians)
        => ArcTo(_currentX + dx, _currentY + dy, signedAngleRadians);

    /// <summary>Removes all segments and resets the path to its initial empty state.</summary>
    public void Clear()
    {
        _segments.Clear();
        _totalLength = 0f;
        _currentX = 0f;
        _currentY = 0f;
        _firstX = 0f;
        _firstY = 0f;
        _hasFirst = false;
    }

    // ── Queries ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the world-space position at <paramref name="length"/> units along the path.
    /// Clamps to the endpoint for open paths; wraps for <see cref="IsLooped"/> paths.
    /// </summary>
    public Vector2 PointAtLength(float length)
    {
        if (_segments.Count == 0) return Vector2.Zero;
        float total = TotalLength;
        if (total < 1e-6f) return _hasFirst ? new Vector2(_firstX, _firstY) : Vector2.Zero;

        if (_isLooped)
            length = ((length % total) + total) % total;
        else
            length = MathF.Max(0f, MathF.Min(length, total));

        return EvaluateAtLength(length);
    }

    /// <summary>
    /// Returns the world-space position at a normalized <paramref name="ratio"/> along the path.
    /// 0 = path start, 1 = path end (wraps for <see cref="IsLooped"/> paths).
    /// </summary>
    public Vector2 PointAtRatio(float ratio) => PointAtLength(ratio * TotalLength);

    /// <summary>
    /// Returns the unit tangent vector (direction of travel) at <paramref name="length"/> units along the path.
    /// Falls back to <see cref="Vector2.UnitX"/> if the tangent cannot be determined.
    /// </summary>
    /// <param name="length">The distance along the path.</param>
    /// <param name="epsilon">
    /// Look-ahead/look-behind distance for numerical tangent estimation.
    /// Increase for very short paths; decrease for tight curves where detail matters.
    /// </param>
    public Vector2 TangentAtLength(float length, float epsilon = 0.5f)
    {
        float total = TotalLength;
        if (total < 1e-6f) return Vector2.UnitX;

        Vector2 pa, pb;
        if (_isLooped)
        {
            float a = ((length - epsilon) % total + total) % total;
            float b = ((length + epsilon) % total + total) % total;
            pa = PointAtLength(a);
            pb = PointAtLength(b);
        }
        else
        {
            pa = PointAtLength(MathF.Max(0f, length - epsilon));
            pb = PointAtLength(MathF.Min(total, length + epsilon));
        }

        var delta = pb - pa;
        return delta.LengthSquared() > 1e-12f ? Vector2.Normalize(delta) : Vector2.UnitX;
    }

    /// <summary>
    /// Returns the unit tangent vector at a normalized <paramref name="ratio"/> along the path.
    /// </summary>
    public Vector2 TangentAtRatio(float ratio) => TangentAtLength(ratio * TotalLength);

    // ── Rendering ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Batch is not ShapesBatch sb) return;

        Vector2? lastWorldPt = null;

        foreach (var seg in _segments)
        {
            if (seg.Kind == SegmentKind.Move)
            {
                lastWorldPt = null;
                continue;
            }

            // Connect from last endpoint to the start of this segment's tessellation,
            // then draw within the segment.
            for (int i = 0; i < seg.TessPoints.Length; i++)
            {
                var pt = seg.TessPoints[i];
                if (lastWorldPt.HasValue)
                    DrawSegmentLine(sb, camera, lastWorldPt.Value, pt);
                lastWorldPt = pt;
            }
        }

        // Close the loop
        if (_isLooped && _hasFirst && lastWorldPt.HasValue)
            DrawSegmentLine(sb, camera, lastWorldPt.Value, new Vector2(_firstX, _firstY));
    }

    // ── Internal ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the zero-based index of the non-Move segment containing <paramref name="length"/>.
    /// Used by <see cref="Movement.PathFollower"/> for waypoint tracking.
    /// </summary>
    internal int GetSegmentIndexAtLength(float length)
    {
        float walked = 0f;
        int index = 0;
        foreach (var seg in _segments)
        {
            if (seg.Kind == SegmentKind.Move) continue;
            if (length <= walked + seg.Length) return index;
            walked += seg.Length;
            index++;
        }
        return index; // closing segment or past the end
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private void DrawSegmentLine(ShapesBatch sb, Camera camera, Vector2 worldA, Vector2 worldB)
    {
        var sA = camera.WorldToScreen(worldA);
        var sB = camera.WorldToScreen(worldB);
        float remainderRadius = MathF.Max(0.5f, (LineThickness - 1f) / 2f);
        sb.Shapes.FillLine(
            new XnaVec2(sA.X, sA.Y),
            new XnaVec2(sB.X, sB.Y),
            remainderRadius, aaSize: 0.5f, c: Color);
    }

    private static Segment BuildArcSegment(Vector2 start, Vector2 end, float arcAngle)
    {
        float absAngle = MathF.Abs(arcAngle);
        float chordLen = Vector2.Distance(start, end);

        if (chordLen < 1e-6f || absAngle < 1e-6f)
            return new Segment { Kind = SegmentKind.Arc, Start = start, End = end, TessPoints = new[] { start } };

        float halfAngle = absAngle / 2f;
        float radius = chordLen / (2f * MathF.Sin(halfAngle));

        // Center is perpendicular to the chord midpoint.
        // For CCW (θ > 0): center is to the left of Start→End.
        // For CW  (θ < 0): center is to the right.
        var chordDir = (end - start) / chordLen;
        var leftNormal = new Vector2(-chordDir.Y, chordDir.X); // 90° CCW from chord
        float dCenter = radius * MathF.Cos(halfAngle);
        var center = (start + end) * 0.5f + MathF.Sign(arcAngle) * dCenter * leftNormal;

        float startAngle = MathF.Atan2(start.Y - center.Y, start.X - center.X);
        float arcLen = radius * absAngle;

        // ~16 line segments per π radians of arc, minimum 8
        int steps = System.Math.Max(8, (int)MathF.Ceiling(absAngle / MathF.PI * 16f));
        var points = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            float angle = startAngle + (float)i / steps * arcAngle;
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        return new Segment
        {
            Kind = SegmentKind.Arc,
            Start = start,
            End = end,
            ArcAngle = arcAngle,
            ArcCenter = center,
            ArcRadius = radius,
            ArcStartAngle = startAngle,
            Length = arcLen,
            TessPoints = points,
        };
    }

    private float ClosingSegmentLength()
    {
        if (!_hasFirst) return 0f;
        for (int i = _segments.Count - 1; i >= 0; i--)
            if (_segments[i].Kind != SegmentKind.Move)
                return Vector2.Distance(_segments[i].End, new Vector2(_firstX, _firstY));
        return 0f;
    }

    private Vector2 EvaluateAtLength(float length)
    {
        float walked = 0f;
        foreach (var seg in _segments)
        {
            if (seg.Kind == SegmentKind.Move) continue;
            float segEnd = walked + seg.Length;
            if (length <= segEnd)
                return EvaluateSegmentAtLength(seg, length - walked);
            walked = segEnd;
        }

        // Closing segment for looped paths
        if (_isLooped && _hasFirst)
        {
            for (int i = _segments.Count - 1; i >= 0; i--)
            {
                if (_segments[i].Kind == SegmentKind.Move) continue;
                var last = _segments[i].End;
                var first = new Vector2(_firstX, _firstY);
                float closingLen = Vector2.Distance(last, first);
                if (closingLen < 1e-6f) return first;
                float ratio = System.Math.Clamp((length - walked) / closingLen, 0f, 1f);
                return Vector2.Lerp(last, first, ratio);
            }
        }

        // Clamp to the last segment's end
        for (int i = _segments.Count - 1; i >= 0; i--)
            if (_segments[i].Kind != SegmentKind.Move)
                return _segments[i].End;

        return Vector2.Zero;
    }

    private static Vector2 EvaluateSegmentAtLength(Segment seg, float localLength)
    {
        if (seg.Length < 1e-6f) return seg.Start;
        float ratio = System.Math.Clamp(localLength / seg.Length, 0f, 1f);

        if (seg.Kind == SegmentKind.Line)
            return Vector2.Lerp(seg.Start, seg.End, ratio);

        if (seg.Kind == SegmentKind.Arc)
        {
            float angle = seg.ArcStartAngle + ratio * seg.ArcAngle;
            return seg.ArcCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * seg.ArcRadius;
        }

        return seg.Start;
    }
}
