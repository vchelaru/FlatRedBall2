using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

/// <summary>
/// Tests for SAT-based polygon collision (Circle vs Polygon, Polygon vs Polygon,
/// Polygon vs AxisAlignedRectangle). Each test verifies that GetSeparationVector
/// pushes the first argument AWAY from the second argument.
/// </summary>
public class PolygonCollisionTests
{
    // A square polygon centered at origin, side = 100.
    private static Polygon Square(float cx, float cy)
    {
        var poly = Polygon.FromPoints(new[]
        {
            new Vector2(-50f, -50f),
            new Vector2( 50f, -50f),
            new Vector2( 50f,  50f),
            new Vector2(-50f,  50f),
        });
        poly.X = cx;
        poly.Y = cy;
        return poly;
    }

    // ── Circle vs Polygon ──────────────────────────────────────────────────────

    [Fact]
    public void CircleVsPolygon_CircleToRight_PushesCircleRight()
    {
        var poly   = Square(0f, 0f);
        var circle = new Circle { X = 60f, Y = 0f, Radius = 20f }; // overlaps by 10

        var sep = circle.GetSeparationVector(poly);

        sep.X.ShouldBeGreaterThan(0f, "circle should be pushed right (away from polygon)");
        sep.Y.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void CircleVsPolygon_CircleToLeft_PushesCircleLeft()
    {
        var poly   = Square(0f, 0f);
        var circle = new Circle { X = -60f, Y = 0f, Radius = 20f };

        var sep = circle.GetSeparationVector(poly);

        sep.X.ShouldBeLessThan(0f, "circle should be pushed left (away from polygon)");
        sep.Y.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void CircleVsPolygon_CircleAbove_PushesCircleUp()
    {
        var poly   = Square(0f, 0f);
        var circle = new Circle { X = 0f, Y = 60f, Radius = 20f };

        var sep = circle.GetSeparationVector(poly);

        sep.Y.ShouldBeGreaterThan(0f, "circle should be pushed up (away from polygon)");
        sep.X.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void CircleVsPolygon_CircleBelow_PushesCircleDown()
    {
        var poly   = Square(0f, 0f);
        var circle = new Circle { X = 0f, Y = -60f, Radius = 20f };

        var sep = circle.GetSeparationVector(poly);

        sep.Y.ShouldBeLessThan(0f, "circle should be pushed down (away from polygon)");
        sep.X.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void CircleVsPolygon_NoOverlap_ReturnsZero()
    {
        var poly   = Square(0f, 0f);
        var circle = new Circle { X = 200f, Y = 0f, Radius = 20f };

        circle.GetSeparationVector(poly).ShouldBe(Vector2.Zero);
    }

    [Fact]
    public void CircleVsPolygon_SeparationMovesPastCollision()
    {
        var poly   = Square(0f, 0f);
        var circle = new Circle { X = 60f, Y = 0f, Radius = 20f };

        var sep = circle.GetSeparationVector(poly);
        var moved = new Circle { X = circle.X + sep.X, Y = circle.Y + sep.Y, Radius = circle.Radius };

        moved.GetSeparationVector(poly).ShouldBe(Vector2.Zero, "applying separation should resolve the collision");
    }

    // ── Polygon vs Polygon ─────────────────────────────────────────────────────

    [Fact]
    public void PolygonVsPolygon_AToRight_PushesARight()
    {
        var a = Square( 60f, 0f); // overlaps b by 40 on x-axis
        var b = Square(  0f, 0f);

        var sep = a.GetSeparationVector(b);

        sep.X.ShouldBeGreaterThan(0f, "polygon A should be pushed right (away from B)");
        sep.Y.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void PolygonVsPolygon_AToLeft_PushesALeft()
    {
        var a = Square(-60f, 0f);
        var b = Square(  0f, 0f);

        var sep = a.GetSeparationVector(b);

        sep.X.ShouldBeLessThan(0f, "polygon A should be pushed left (away from B)");
        sep.Y.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void PolygonVsPolygon_SeparationMovesPastCollision()
    {
        var a = Square(60f, 0f);
        var b = Square( 0f, 0f);

        var sep = a.GetSeparationVector(b);
        var moved = Square(a.X + sep.X, a.Y + sep.Y);

        moved.GetSeparationVector(b).ShouldBe(Vector2.Zero);
    }

    // ── Polygon vs AxisAlignedRectangle ────────────────────────────────────────

    [Fact]
    public void PolygonVsRect_PolyToRight_PushesPolyRight()
    {
        var poly = Square(60f, 0f);
        var rect = new AxisAlignedRectangle { Width = 100f, Height = 100f, X = 0f, Y = 0f };

        var sep = poly.GetSeparationVector(rect);

        sep.X.ShouldBeGreaterThan(0f, "polygon should be pushed right (away from rect)");
        sep.Y.ShouldBe(0f, tolerance: 0.01f);
    }

    [Fact]
    public void PolygonVsRect_RectToRight_PushesRectRight()
    {
        var poly = Square(0f, 0f);
        var rect = new AxisAlignedRectangle { Width = 100f, Height = 100f, X = 60f, Y = 0f };

        var sep = rect.GetSeparationVector(poly);

        sep.X.ShouldBeGreaterThan(0f, "rect should be pushed right (away from polygon)");
    }
}
