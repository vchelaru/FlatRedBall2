using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class LineTests
{
    // ── AbsolutePoint helpers ─────────────────────────────────────────────

    [Fact]
    public void AbsolutePoint1_EqualsPosition()
    {
        var line = new Line { X = 10f, Y = 5f };

        line.AbsolutePoint1.ShouldBe(new Vector2(10f, 5f));
    }

    [Fact]
    public void AbsolutePoint2_IsPositionPlusEndPoint()
    {
        var line = new Line { X = 10f, Y = 5f, EndPoint = new Vector2(4f, -2f) };

        line.AbsolutePoint2.ShouldBe(new Vector2(14f, 3f));
    }

    // ── CollidesWith (ICollidable — pit of success) ───────────────────────

    [Fact]
    public void CollidesWith_LineVsLine_Crossing_ReturnsTrue()
    {
        // Horizontal: (−10,0)→(10,0); vertical: (0,−10)→(0,10).
        var h = new Line { X = -10f, Y = 0f,  EndPoint = new Vector2(20f, 0f) };
        var v = new Line { X = 0f,  Y = -10f, EndPoint = new Vector2(0f, 20f) };

        ((ICollidable)h).CollidesWith(v).ShouldBeTrue();
    }

    [Fact]
    public void CollidesWith_LineVsCircle_Intersecting_ReturnsTrue()
    {
        var line   = new Line   { X = -20f, Y = 0f, EndPoint = new Vector2(40f, 0f) };
        var circle = new Circle { X = 0f, Y = 5f, Radius = 10f };

        ((ICollidable)line).CollidesWith(circle).ShouldBeTrue();
    }

    [Fact]
    public void CollidesWith_LineVsAARect_Intersecting_ReturnsTrue()
    {
        var line = new Line                  { X = 0f, Y = 0f,   EndPoint = new Vector2(100f, 100f) };
        var rect = new AxisAlignedRectangle  { X = 0f, Y = 0f,   Width = 10f, Height = 10f };

        ((ICollidable)line).CollidesWith(rect).ShouldBeTrue();
    }

    // ── CollideAgainst(Line) ──────────────────────────────────────────────

    [Fact]
    public void CollideAgainst_LineVsLine_Crossing_ReturnsTrue()
    {
        var h = new Line { X = -10f, Y = 0f,  EndPoint = new Vector2(20f, 0f) };
        var v = new Line { X = 0f,  Y = -10f, EndPoint = new Vector2(0f, 20f) };

        h.CollideAgainst(v).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsLine_Parallel_ReturnsFalse()
    {
        var a = new Line { X = -10f, Y =  5f, EndPoint = new Vector2(20f, 0f) };
        var b = new Line { X = -10f, Y = -5f, EndPoint = new Vector2(20f, 0f) };

        a.CollideAgainst(b).ShouldBeFalse();
    }

    [Fact]
    public void CollideAgainst_LineVsLine_SharingEndpoint_ReturnsTrue()
    {
        // a ends at (10,0); b starts at (10,0).
        var a = new Line { X = 0f,  Y = 0f, EndPoint = new Vector2(10f, 0f) };
        var b = new Line { X = 10f, Y = 0f, EndPoint = new Vector2(0f, 10f) };

        a.CollideAgainst(b).ShouldBeTrue();
    }

    // ── CollideAgainst(Circle) ────────────────────────────────────────────

    [Fact]
    public void CollideAgainst_LineVsCircle_CircleCrossesSegment_ReturnsTrue()
    {
        // Horizontal segment at Y=0 from X=−20 to X=20; circle at (0,5) radius 10.
        var line   = new Line   { X = -20f, Y = 0f, EndPoint = new Vector2(40f, 0f) };
        var circle = new Circle { X = 0f, Y = 5f, Radius = 10f };

        line.CollideAgainst(circle).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsCircle_CircleFarAway_ReturnsFalse()
    {
        var line   = new Line   { X = -10f, Y = 0f, EndPoint = new Vector2(20f, 0f) };
        var circle = new Circle { X = 0f, Y = 50f, Radius = 5f };

        line.CollideAgainst(circle).ShouldBeFalse();
    }

    [Fact]
    public void CollideAgainst_LineVsCircle_CircleNearEndpointOnly_ReturnsTrue()
    {
        // Segment from (0,0) to (10,0); circle at (11,0) radius 2 — closest point is (10,0), dist = 1.
        var line   = new Line   { X = 0f, Y = 0f, EndPoint = new Vector2(10f, 0f) };
        var circle = new Circle { X = 11f, Y = 0f, Radius = 2f };

        line.CollideAgainst(circle).ShouldBeTrue();
    }

    // ── CollideAgainst(AxisAlignedRectangle) ─────────────────────────────

    [Fact]
    public void CollideAgainst_LineVsAARect_EndpointInsideRect_ReturnsTrue()
    {
        // First endpoint (0,0) is inside the 10×10 rect centered at origin.
        var line = new Line                 { X = 0f, Y = 0f, EndPoint = new Vector2(100f, 100f) };
        var rect = new AxisAlignedRectangle { X = 0f, Y = 0f, Width = 10f, Height = 10f };

        line.CollideAgainst(rect).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsAARect_SegmentCrossesRect_ReturnsTrue()
    {
        // Diagonal from (−20,−20) to (20,20) crosses a 10×10 rect at origin.
        var line = new Line                 { X = -20f, Y = -20f, EndPoint = new Vector2(40f, 40f) };
        var rect = new AxisAlignedRectangle { X = 0f,   Y = 0f,   Width = 10f, Height = 10f };

        line.CollideAgainst(rect).ShouldBeTrue();
    }

    [Fact]
    public void CollideAgainst_LineVsAARect_SegmentMissesRect_ReturnsFalse()
    {
        var line = new Line                 { X = -50f, Y = 0f, EndPoint = new Vector2(30f, 0f) };
        var rect = new AxisAlignedRectangle { X = 0f,   Y = 0f, Width = 10f, Height = 10f };

        line.CollideAgainst(rect).ShouldBeFalse();
    }
}
