using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

// Issue #991: a shape's local offset must rotate with the parent's AbsoluteRotation (lever-arm
// orbit), not stay at a fixed world offset. One test per shape type since each implements its
// own AbsoluteX/AbsoluteY rather than sharing Entity's.
public class ShapeAttachmentTests
{
    [Fact]
    public void AbsoluteX_AARectWithRotatedParent_OrbitsAroundParent()
    {
        var parentRotation = Angle.FromDegrees(90f);
        float childX = 10f, childY = 0f;
        float expectedAbsoluteX = 0f;
        float expectedAbsoluteY = 10f;

        var parent = new Entity { Rotation = parentRotation };
        var rect = new AARect { X = childX, Y = childY };
        parent.Add(rect);

        rect.AbsoluteX.ShouldBe(expectedAbsoluteX, tolerance: 0.001f);
        rect.AbsoluteY.ShouldBe(expectedAbsoluteY, tolerance: 0.001f);
    }

    [Fact]
    public void AbsoluteX_CircleWithRotatedParent_OrbitsAroundParent()
    {
        var parentRotation = Angle.FromDegrees(90f);
        float childX = 10f, childY = 0f;
        float expectedAbsoluteX = 0f;
        float expectedAbsoluteY = 10f;

        var parent = new Entity { Rotation = parentRotation };
        var circle = new Circle { X = childX, Y = childY };
        parent.Add(circle);

        circle.AbsoluteX.ShouldBe(expectedAbsoluteX, tolerance: 0.001f);
        circle.AbsoluteY.ShouldBe(expectedAbsoluteY, tolerance: 0.001f);
    }

    [Fact]
    public void AbsoluteX_PolygonWithRotatedParent_OrbitsAroundParent()
    {
        var parentRotation = Angle.FromDegrees(90f);
        float childX = 10f, childY = 0f;
        float expectedAbsoluteX = 0f;
        float expectedAbsoluteY = 10f;

        var parent = new Entity { Rotation = parentRotation };
        var polygon = Polygon.CreateRectangle(4f, 4f);
        polygon.X = childX;
        polygon.Y = childY;
        parent.Add(polygon);

        polygon.AbsoluteX.ShouldBe(expectedAbsoluteX, tolerance: 0.001f);
        polygon.AbsoluteY.ShouldBe(expectedAbsoluteY, tolerance: 0.001f);
    }
}
