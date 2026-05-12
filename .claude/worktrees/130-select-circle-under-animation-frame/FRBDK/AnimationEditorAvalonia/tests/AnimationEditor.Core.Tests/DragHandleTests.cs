using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class DragHandleHitTesterTests
{
    // rect: left=10, top=10, right=110, bottom=110, cx=60, cy=60

    [Fact]
    public void GetHandleAt_TopLeft_WhenOnTopLeftCorner()
    {
        var result = DragHandleHitTester.GetHandleAt(10f, 10f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.TopLeft, result);
    }

    [Fact]
    public void GetHandleAt_TopCenter_WhenOnTopEdgeMidpoint()
    {
        var result = DragHandleHitTester.GetHandleAt(60f, 10f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.TopCenter, result);
    }

    [Fact]
    public void GetHandleAt_TopRight_WhenOnTopRightCorner()
    {
        var result = DragHandleHitTester.GetHandleAt(110f, 10f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.TopRight, result);
    }

    [Fact]
    public void GetHandleAt_MidLeft_WhenOnLeftEdgeMidpoint()
    {
        var result = DragHandleHitTester.GetHandleAt(10f, 60f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.MidLeft, result);
    }

    [Fact]
    public void GetHandleAt_MidRight_WhenOnRightEdgeMidpoint()
    {
        var result = DragHandleHitTester.GetHandleAt(110f, 60f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.MidRight, result);
    }

    [Fact]
    public void GetHandleAt_BotLeft_WhenOnBottomLeftCorner()
    {
        var result = DragHandleHitTester.GetHandleAt(10f, 110f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.BotLeft, result);
    }

    [Fact]
    public void GetHandleAt_BotCenter_WhenOnBottomEdgeMidpoint()
    {
        var result = DragHandleHitTester.GetHandleAt(60f, 110f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.BotCenter, result);
    }

    [Fact]
    public void GetHandleAt_BotRight_WhenOnBottomRightCorner()
    {
        var result = DragHandleHitTester.GetHandleAt(110f, 110f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.BotRight, result);
    }

    [Fact]
    public void GetHandleAt_Move_WhenInsideRectBody()
    {
        var result = DragHandleHitTester.GetHandleAt(60f, 60f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.Move, result);
    }

    [Fact]
    public void GetHandleAt_None_WhenOutsideRect()
    {
        var result = DragHandleHitTester.GetHandleAt(200f, 200f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.None, result);
    }

    [Fact]
    public void GetHandleAt_HitsHandle_WhenWithinHitRadius()
    {
        // Point is 5 pixels away from TopLeft corner — within default hitRadius of 7
        var result = DragHandleHitTester.GetHandleAt(15f, 14f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.TopLeft, result);
    }

    [Fact]
    public void GetHandleAt_MissesHandle_WhenBeyondHitRadius()
    {
        // Point is 20 pixels away from TopLeft corner — beyond default hitRadius of 7
        var result = DragHandleHitTester.GetHandleAt(30f, 30f, 10f, 10f, 110f, 110f);
        Assert.Equal(HandleKind.Move, result); // inside the rect but not near a handle
    }

    [Fact]
    public void GetHandleAt_None_WhenRectIsZeroSize()
    {
        var result = DragHandleHitTester.GetHandleAt(50f, 50f, 50f, 50f, 50f, 50f);
        // all handles coincide at (50,50); TopLeft is checked first
        Assert.NotEqual(HandleKind.None, result);
    }

    // ── Handle offset (issue #114: handles sit outside the frame) ─────────────

    [Fact]
    public void GetHandleAt_WithHandleOffset_TopLeftHandleIsOutsideFrameBounds()
    {
        // With handleOffset=5, TopLeft moves from (10,10) to (5,5).
        // Clicking at (3,3) is outside the frame AND within hitRadius(7) of (5,5).
        var result = DragHandleHitTester.GetHandleAt(3f, 3f, 10f, 10f, 110f, 110f, handleOffset: 5f);
        Assert.Equal(HandleKind.TopLeft, result);
    }

    [Fact]
    public void GetHandleAt_WithHandleOffset_BotRightHandleIsOutsideFrameBounds()
    {
        // With handleOffset=5, BotRight moves from (110,110) to (115,115).
        var result = DragHandleHitTester.GetHandleAt(115f, 115f, 10f, 10f, 110f, 110f, handleOffset: 5f);
        Assert.Equal(HandleKind.BotRight, result);
    }

    [Fact]
    public void GetHandleAt_WithHandleOffset_TopCenterHandleIsAboveFrame()
    {
        // With handleOffset=5, TopCenter moves from (60,10) to (60,5).
        var result = DragHandleHitTester.GetHandleAt(60f, 3f, 10f, 10f, 110f, 110f, handleOffset: 5f);
        Assert.Equal(HandleKind.TopCenter, result);
    }

    [Fact]
    public void GetHandleAt_WithHandleOffset_MidLeftHandleIsLeftOfFrame()
    {
        // With handleOffset=5, MidLeft moves from (10,60) to (5,60).
        var result = DragHandleHitTester.GetHandleAt(3f, 60f, 10f, 10f, 110f, 110f, handleOffset: 5f);
        Assert.Equal(HandleKind.MidLeft, result);
    }

    [Fact]
    public void GetHandleAt_WithHandleOffset_FrameInteriorIsStillMove()
    {
        // With handleOffset=5, clicking inside the frame body still gives Move.
        var result = DragHandleHitTester.GetHandleAt(60f, 60f, 10f, 10f, 110f, 110f, handleOffset: 5f);
        Assert.Equal(HandleKind.Move, result);
    }
}

[Collection("SequentialSingletons")]
public class DragHandleApplierTests
{
    private static BoundsRect Rect20x20 => new(20f, 20f, 80f, 80f); // 60×60

    // ── Apply – edge deltas ───────────────────────────────────────────────────

    [Fact]
    public void Apply_Move_ShiftsAllFourEdges()
    {
        var result = DragHandleApplier.Apply(HandleKind.Move, 10f, 5f, Rect20x20);
        Assert.Equal(30f, result.Left);
        Assert.Equal(25f, result.Top);
        Assert.Equal(90f, result.Right);
        Assert.Equal(85f, result.Bottom);
    }

    [Fact]
    public void Apply_TopLeft_MovesTopAndLeftEdges()
    {
        var result = DragHandleApplier.Apply(HandleKind.TopLeft, 5f, 5f, Rect20x20);
        Assert.Equal(25f, result.Left);
        Assert.Equal(25f, result.Top);
        Assert.Equal(80f, result.Right);  // unchanged
        Assert.Equal(80f, result.Bottom); // unchanged
    }

    [Fact]
    public void Apply_TopCenter_MovesTopEdgeOnly()
    {
        var result = DragHandleApplier.Apply(HandleKind.TopCenter, 99f, 5f, Rect20x20);
        Assert.Equal(20f, result.Left);   // unchanged
        Assert.Equal(25f, result.Top);
        Assert.Equal(80f, result.Right);  // unchanged
        Assert.Equal(80f, result.Bottom); // unchanged
    }

    [Fact]
    public void Apply_MidRight_MovesRightEdgeOnly()
    {
        var result = DragHandleApplier.Apply(HandleKind.MidRight, 10f, 99f, Rect20x20);
        Assert.Equal(20f, result.Left);
        Assert.Equal(20f, result.Top);
        Assert.Equal(90f, result.Right);
        Assert.Equal(80f, result.Bottom);
    }

    [Fact]
    public void Apply_BotCenter_MovesBottomEdgeOnly()
    {
        var result = DragHandleApplier.Apply(HandleKind.BotCenter, 99f, 10f, Rect20x20);
        Assert.Equal(90f, result.Bottom); // 80 + dy(10) = 90
        Assert.Equal(20f, result.Left);
        Assert.Equal(80f, result.Right);
        Assert.Equal(20f, result.Top);
    }

    // ── Outside-PNG-bounds dragging (issue #107) ──────────────────────────────

    [Fact]
    public void Apply_Move_AllowsNegativeLeft()
    {
        // Frame (20,20)→(80,80) dragged 30px left → Left should be -10, not clamped to 0.
        var result = DragHandleApplier.Apply(HandleKind.Move, -30f, 0f, Rect20x20);
        Assert.Equal(-10f, result.Left);
        Assert.Equal(50f,  result.Right);  // width preserved
    }

    [Fact]
    public void Apply_Move_AllowsFrameBeyondRightBoundary()
    {
        // Frame (20,20)→(80,80) dragged 40px right → Right=120, not clamped to 100.
        var result = DragHandleApplier.Apply(HandleKind.Move, 40f, 0f, Rect20x20);
        Assert.Equal(60f,  result.Left);
        Assert.Equal(120f, result.Right);
    }

    [Fact]
    public void Apply_Move_AllowsNegativeTop()
    {
        // Frame (20,20)→(80,80) dragged 30px up → Top=-10, not clamped to 0.
        var result = DragHandleApplier.Apply(HandleKind.Move, 0f, -30f, Rect20x20);
        Assert.Equal(-10f, result.Top);
        Assert.Equal(50f,  result.Bottom);
    }

    [Fact]
    public void Apply_MidLeft_AllowsNegativeLeft()
    {
        // Drag left edge 30px left from (20,20,80,80) → Left=-10.
        var result = DragHandleApplier.Apply(HandleKind.MidLeft, -30f, 0f, Rect20x20);
        Assert.Equal(-10f, result.Left);
        Assert.Equal(80f,  result.Right);  // unchanged
    }

    [Fact]
    public void Apply_MidRight_AllowsRightBeyondBitmapWidth()
    {
        // Drag right edge 30px right from (20,20,80,80) → Right=110.
        var result = DragHandleApplier.Apply(HandleKind.MidRight, 30f, 0f, Rect20x20);
        Assert.Equal(110f, result.Right);
        Assert.Equal(20f,  result.Left);   // unchanged
    }

    [Fact]
    public void Apply_TopCenter_AllowsNegativeTop()
    {
        // Drag top edge 30px up from (20,20,80,80) → Top=-10.
        var result = DragHandleApplier.Apply(HandleKind.TopCenter, 0f, -30f, Rect20x20);
        Assert.Equal(-10f, result.Top);
        Assert.Equal(80f,  result.Bottom); // unchanged
    }

    [Fact]
    public void Apply_BotCenter_AllowsBottomBeyondBitmapHeight()
    {
        // Drag bottom edge 30px down from (20,20,80,80) → Bottom=110.
        var result = DragHandleApplier.Apply(HandleKind.BotCenter, 0f, 30f, Rect20x20);
        Assert.Equal(110f, result.Bottom);
        Assert.Equal(20f,  result.Top);    // unchanged
    }

    // ── Minimum-size enforcement ──────────────────────────────────────────────

    [Fact]
    public void Apply_EnforcesMinimumWidthOf1()
    {
        // Drag right edge far left to collapse width
        var result = DragHandleApplier.Apply(HandleKind.MidRight, -200f, 0f, Rect20x20);
        Assert.True(result.Right - result.Left >= 1f);
    }

    [Fact]
    public void Apply_EnforcesMinimumHeightOf1()
    {
        // Drag bottom edge far up to collapse height
        var result = DragHandleApplier.Apply(HandleKind.BotCenter, 0f, -200f, Rect20x20);
        Assert.True(result.Bottom - result.Top >= 1f);
    }

    // ── UV conversion ─────────────────────────────────────────────────────────

    [Fact]
    public void ToUvCoords_DividesByBitmapDimensions()
    {
        var (l, t, r, b) = DragHandleApplier.ToUvCoords(new BoundsRect(10f, 20f, 50f, 80f), 100f, 100f);
        Assert.Equal(0.1f, l);
        Assert.Equal(0.2f, t);
        Assert.Equal(0.5f, r);
        Assert.Equal(0.8f, b);
    }

    [Fact]
    public void ToUvCoords_FullBitmapBounds_ReturnsOneByOne()
    {
        var (l, t, r, b) = DragHandleApplier.ToUvCoords(new BoundsRect(0f, 0f, 100f, 100f), 100f, 100f);
        Assert.Equal(0f, l);
        Assert.Equal(0f, t);
        Assert.Equal(1f, r);
        Assert.Equal(1f, b);
    }

    [Fact]
    public void Apply_None_LeavesRectUnchanged()
    {
        var result = DragHandleApplier.Apply(HandleKind.None, 50f, 50f, Rect20x20);
        Assert.Equal(Rect20x20, result);
    }

    // ── SnapEdges – pixel snap (snapSize = 1) ────────────────────────────────

    [Fact]
    public void SnapEdges_Move_SnapsTopLeftToNearestPixelAndPreservesSize()
    {
        // Left=20.7, Top=30.3, width=60, height=70
        // → snapL=21, snapT=30, R=21+60=81, B=30+70=100
        var input = new BoundsRect(20.7f, 30.3f, 80.7f, 100.3f);
        var result = DragHandleApplier.SnapEdges(input, HandleKind.Move, 1);
        Assert.Equal(21f,  result.Left);
        Assert.Equal(30f,  result.Top);
        Assert.Equal(81f,  result.Right);
        Assert.Equal(100f, result.Bottom);
    }

    [Fact]
    public void SnapEdges_MidRight_SnapsRightEdgeOnly()
    {
        var input = new BoundsRect(20f, 30f, 80.7f, 100f);
        var result = DragHandleApplier.SnapEdges(input, HandleKind.MidRight, 1);
        Assert.Equal(20f,  result.Left);    // unchanged
        Assert.Equal(30f,  result.Top);     // unchanged
        Assert.Equal(81f,  result.Right);   // round(80.7) = 81
        Assert.Equal(100f, result.Bottom);  // unchanged
    }

    [Fact]
    public void SnapEdges_TopCenter_SnapsTopEdgeOnly()
    {
        var input = new BoundsRect(20f, 30.6f, 80f, 100f);
        var result = DragHandleApplier.SnapEdges(input, HandleKind.TopCenter, 1);
        Assert.Equal(20f,  result.Left);
        Assert.Equal(31f,  result.Top);    // round(30.6) = 31
        Assert.Equal(80f,  result.Right);
        Assert.Equal(100f, result.Bottom);
    }

    [Fact]
    public void SnapEdges_GridSize16_SnapsToGridMultiple()
    {
        // TopLeft handle: only Left and Top get snapped
        // round(20.7/16)*16 = round(1.29)*16 = 16
        // round(30.3/16)*16 = round(1.89)*16 = 32
        var input = new BoundsRect(20.7f, 30.3f, 80.7f, 100.3f);
        var result = DragHandleApplier.SnapEdges(input, HandleKind.TopLeft, 16);
        Assert.Equal(16f,    result.Left);
        Assert.Equal(32f,    result.Top);
        Assert.Equal(80.7f,  result.Right);   // unchanged
        Assert.Equal(100.3f, result.Bottom);  // unchanged
    }

    [Fact]
    public void SnapEdges_SnapSizeZeroOrNegative_ReturnsUnchanged()
    {
        var input = new BoundsRect(20.7f, 30.3f, 80.7f, 100.3f);
        Assert.Equal(input, DragHandleApplier.SnapEdges(input, HandleKind.Move, 0));
        Assert.Equal(input, DragHandleApplier.SnapEdges(input, HandleKind.Move, -1));
    }

    // ── Move handle boundary clamping bug ─────────────────────────────────────
    //
    // BUG: when a Move drag carries the entire frame past the right or left
    // edge of the bitmap, the current clamping logic produces an inverted rect
    // where Left > Right.  The clamp for 'l' doesn't bound against bitmapWidth,
    // and the clamp for 'r' can produce a negative value — both should preserve
    // the frame's width against the bitmap boundary.
    //
    // These tests assert the CORRECT behaviour (Left ≤ Right).  They FAIL with
    // the current code, exposing the bug.

    [Fact]
    public void Apply_Move_PastRightBoundary_ShouldNotProduceInvertedRect_BUG()
    {
        // Frame (10,10)→(20,20) on a 32×32 bitmap, moved 30 px right.
        // Unclamped: Left=40, Right=50.
        // Bug: l = max(0, min(40,49)) = 40 — not clamped to bitmapWidth(32)!
        //      r = min(32, max(50,41)) = 32
        // Result: Left=40 > Right=32 → inverted rect.
        var result = DragHandleApplier.Apply(HandleKind.Move, 30f, 0f,
            new BoundsRect(10f, 10f, 20f, 20f));

        Assert.True(result.Left <= result.Right,
            $"BUG: Rect is inverted after Move past right boundary. Left={result.Left}, Right={result.Right}");
    }

    [Fact]
    public void Apply_Move_PastLeftBoundary_ShouldNotProduceInvertedRect_BUG()
    {
        // Frame (10,10)→(20,20) on a 32×32 bitmap, moved 30 px left.
        // Unclamped: Left=-20, Right=-10.
        // Bug: l = max(0, min(-20,-11)) = 0
        //      r = min(32, max(-10,-19)) = min(32,-10) = -10
        // Result: Left=0 > Right=-10 → inverted rect.
        var result = DragHandleApplier.Apply(HandleKind.Move, -30f, 0f,
            new BoundsRect(10f, 10f, 20f, 20f));

        Assert.True(result.Left <= result.Right,
            $"BUG: Rect is inverted after Move past left boundary. Left={result.Left}, Right={result.Right}");
    }
}
