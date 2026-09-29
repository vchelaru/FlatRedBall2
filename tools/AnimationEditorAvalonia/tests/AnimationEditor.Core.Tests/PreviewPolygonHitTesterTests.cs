using AnimationEditor.Core.Rendering;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

// Screen-space polygon hit-testing for the preview panel: body, vertex handles, edge-midpoint handles.
public class PreviewPolygonHitTesterTests
{
    // Concave L in screen space; the notch is the top-right quarter (x > 110, y < 110).
    private static readonly (float X, float Y)[] L =
    {
        (100f, 100f), (110f, 100f), (110f, 110f), (120f, 110f), (120f, 120f), (100f, 120f),
    };

    [Fact]
    public void HitsPolygon_InsideConcaveArm_ReturnsTrue()
    {
        PreviewShapeHitTester.HitsPolygon(115f, 115f, L, tolerance: 0f).ShouldBeTrue();
    }

    [Fact]
    public void HitsPolygon_InTheNotch_ReturnsFalse()
    {
        PreviewShapeHitTester.HitsPolygon(117f, 103f, L, tolerance: 0f).ShouldBeFalse();
    }

    [Fact]
    public void HitsPolygon_JustOutsideAnEdge_ReturnsTrueWithinTolerance()
    {
        PreviewShapeHitTester.HitsPolygon(97f, 105f, L, tolerance: 5f).ShouldBeTrue();
    }

    [Fact]
    public void HitVertex_NearThirdVertex_ReturnsItsIndex()
    {
        PreviewShapeHitTester.HitVertex(111f, 109f, L, radius: 4f).ShouldBe(2);
    }

    [Fact]
    public void HitVertex_AwayFromEveryVertex_ReturnsMinusOne()
    {
        PreviewShapeHitTester.HitVertex(105f, 115f, L, radius: 4f).ShouldBe(-1);
    }

    [Fact]
    public void HitEdgeMidpoint_ClosingEdgeOfClosedOutline_ReturnsLastEdge()
    {
        // The closing edge runs from the last vertex (100,120) back to the first (100,100).
        PreviewShapeHitTester.HitEdgeMidpoint(100f, 110f, L, closed: true, radius: 4f).ShouldBe(5);
    }

    [Fact]
    public void HitEdgeMidpoint_OpenOutline_HasNoClosingEdge()
    {
        PreviewShapeHitTester.HitEdgeMidpoint(100f, 110f, L, closed: false, radius: 4f).ShouldBe(-1);
    }
}
