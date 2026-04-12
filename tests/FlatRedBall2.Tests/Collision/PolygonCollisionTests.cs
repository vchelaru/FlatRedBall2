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

    // ── Concave polygon collision ───────────────────────────────────────────────
    //
    // L-shape (local coords, Y+ up, CCW winding):
    //   (-50,50)───(50,50)
    //      │           │
    //   (-50, 0)  (0,0)──(50,0)
    //      │       │
    //   (-50,-50)─(0,-50)
    //
    // Concave pocket = region x=[0,50], y=[-50,0] (the "missing" bottom-right quadrant).

    private static Polygon LShape(float cx, float cy)
    {
        var poly = Polygon.FromPoints(new[]
        {
            new Vector2(-50f, -50f),
            new Vector2(  0f, -50f),
            new Vector2(  0f,   0f),
            new Vector2( 50f,   0f),
            new Vector2( 50f,  50f),
            new Vector2(-50f,  50f),
        });
        poly.X = cx;
        poly.Y = cy;
        return poly;
    }

    [Fact]
    public void ConcavePolygon_HasTwoConvexParts()
    {
        var poly = LShape(0f, 0f);
        poly.ConvexParts.Count.ShouldBe(2, "L-shape decomposes into exactly 2 convex parts");
    }

    [Fact]
    public void ConcavePolygon_CircleInConcavePocket_NoCollision()
    {
        var poly = LShape(0f, 0f);
        // Circle entirely within the concave "missing" pocket at (25, -25).
        var circle = new Circle { X = 25f, Y = -25f, Radius = 10f };

        circle.GetSeparationVector(poly).ShouldBe(Vector2.Zero,
            "circle in concave pocket should not collide with L-shape");
    }

    [Fact]
    public void ConcavePolygon_CircleOverlapsArm_Collides()
    {
        var poly = LShape(0f, 0f);
        // Circle centered below the left arm, overlapping its bottom edge by 5 units.
        var circle = new Circle { X = -25f, Y = -60f, Radius = 15f };

        var sep = circle.GetSeparationVector(poly);
        sep.ShouldNotBe(Vector2.Zero, "circle overlapping L-arm should collide");
        sep.Y.ShouldBeLessThan(0f, "circle should be pushed down, away from the arm's bottom edge");
    }

    [Fact]
    public void ConcavePolygon_CircleSeparationResolvesCollision()
    {
        var poly = LShape(0f, 0f);
        var circle = new Circle { X = -25f, Y = -60f, Radius = 15f };

        var sep = circle.GetSeparationVector(poly);
        var moved = new Circle { X = circle.X + sep.X, Y = circle.Y + sep.Y, Radius = circle.Radius };

        moved.GetSeparationVector(poly).ShouldBe(Vector2.Zero,
            "applying separation should resolve the concave polygon collision");
    }

    [Fact]
    public void ConcavePolygon_RectInConcavePocket_NoCollision()
    {
        var poly = LShape(0f, 0f);
        // Rect entirely within the concave pocket.
        var rect = new AxisAlignedRectangle { X = 25f, Y = -25f, Width = 10f, Height = 10f };

        rect.GetSeparationVector(poly).ShouldBe(Vector2.Zero,
            "rect in concave pocket should not collide with L-shape");
    }

    [Fact]
    public void ConcavePolygon_ConvexPolygonInConcavePocket_NoCollision()
    {
        var lPoly = LShape(0f, 0f);
        // Small 10×10 convex polygon entirely within the concave pocket (x=[0,50], y=[-50,0]).
        var pocket = Polygon.FromPoints(new[]
        {
            new Vector2(-5f, -5f),
            new Vector2( 5f, -5f),
            new Vector2( 5f,  5f),
            new Vector2(-5f,  5f),
        });
        pocket.X = 25f;
        pocket.Y = -25f;

        pocket.GetSeparationVector(lPoly).ShouldBe(Vector2.Zero,
            "convex polygon in concave pocket should not collide with L-shape");
    }

}
