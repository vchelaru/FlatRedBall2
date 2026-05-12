using System.Numerics;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class ShapeContainsTests
{
    [Fact]
    public void Contains_AARectAtOrigin_PointInside_ReturnsTrue()
    {
        var rect = new AARect { Width = 20f, Height = 10f };
        rect.Contains(new Vector2(5f, -3f)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_AARectAtOrigin_PointOnBoundary_ReturnsTrue()
    {
        var rect = new AARect { Width = 20f, Height = 10f };
        rect.Contains(new Vector2(10f, 5f)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_AARectAtOrigin_PointOutside_ReturnsFalse()
    {
        var rect = new AARect { Width = 20f, Height = 10f };
        rect.Contains(new Vector2(11f, 0f)).ShouldBeFalse();
    }

    [Fact]
    public void Contains_AARectOffset_PointInside_ReturnsTrue()
    {
        // Rect centered at (100, 200), 20x10. World point (105, 198) is inside.
        var rect = new AARect { X = 100f, Y = 200f, Width = 20f, Height = 10f };
        rect.Contains(new Vector2(105f, 198f)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_AARectOffset_PointOutside_ReturnsFalse()
    {
        var rect = new AARect { X = 100f, Y = 200f, Width = 20f, Height = 10f };
        rect.Contains(new Vector2(50f, 200f)).ShouldBeFalse();
    }

    [Fact]
    public void Contains_CircleAtOrigin_PointInside_ReturnsTrue()
    {
        var circle = new Circle { Radius = 10f };
        circle.Contains(new Vector2(3f, 4f)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_CircleAtOrigin_PointOnBoundary_ReturnsTrue()
    {
        var circle = new Circle { Radius = 5f };
        // 3-4-5 triangle: distance is exactly 5.
        circle.Contains(new Vector2(3f, 4f)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_CircleAtOrigin_PointOutside_ReturnsFalse()
    {
        var circle = new Circle { Radius = 5f };
        circle.Contains(new Vector2(10f, 0f)).ShouldBeFalse();
    }

    [Fact]
    public void Contains_CircleOffset_PointInside_ReturnsTrue()
    {
        var circle = new Circle { X = 100f, Y = 200f, Radius = 10f };
        circle.Contains(new Vector2(105f, 200f)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_PolygonConvexAtOrigin_PointInside_ReturnsTrue()
    {
        var poly = Polygon.CreateRectangle(20f, 10f);
        poly.Contains(new Vector2(5f, -3f)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_PolygonConvexAtOrigin_PointOutside_ReturnsFalse()
    {
        var poly = Polygon.CreateRectangle(20f, 10f);
        poly.Contains(new Vector2(20f, 0f)).ShouldBeFalse();
    }

    [Fact]
    public void Contains_PolygonOffset_PointInside_ReturnsTrue()
    {
        var poly = Polygon.CreateRectangle(20f, 10f);
        poly.X = 100f;
        poly.Y = 200f;
        poly.Contains(new Vector2(105f, 200f)).ShouldBeTrue();
    }

    [Fact]
    public void Contains_PolygonConcave_PointInBoundingBoxButOutsidePolygon_ReturnsFalse()
    {
        // U-shape (concave) with a notch cut from the top center. Bounding box is
        // (-10..10, -10..10) but the notch (-2..2, 0..10) is outside the shape.
        // Vertex order: CCW around outer perimeter, dipping into the notch at the top.
        var poly = Polygon.FromPoints(new[]
        {
            new Vector2(-10f, -10f),
            new Vector2( 10f, -10f),
            new Vector2( 10f,  10f),
            new Vector2(  2f,  10f),
            new Vector2(  2f,   0f),
            new Vector2( -2f,   0f),
            new Vector2( -2f,  10f),
            new Vector2(-10f,  10f),
        });

        // Inside the notch — within bounding box but outside the polygon's interior.
        poly.Contains(new Vector2(0f, 5f)).ShouldBeFalse();
        // In the solid base — should be inside.
        poly.Contains(new Vector2(0f, -5f)).ShouldBeTrue();
    }
}
