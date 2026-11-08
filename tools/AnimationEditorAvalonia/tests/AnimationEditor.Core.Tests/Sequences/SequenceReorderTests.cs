using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Dragging sequence rows to a gap in the list: which gap, and the new order for one row or a block.</summary>
public class SequenceReorderTests
{
    private static readonly (double Top, double Height)[] ThreeRows = { (0, 20), (20, 20), (40, 20) };

    [Fact]
    public void SlotAt_SplitsEachRowAtItsMiddle()
    {
        SequenceReorder.SlotAt(ThreeRows, 5).ShouldBe(0);
        SequenceReorder.SlotAt(ThreeRows, 15).ShouldBe(1);
        SequenceReorder.SlotAt(ThreeRows, 25).ShouldBe(1);
        SequenceReorder.SlotAt(ThreeRows, 35).ShouldBe(2);
        SequenceReorder.SlotAt(ThreeRows, 55).ShouldBe(3);
    }

    [Fact]
    public void SlotAt_AboveAndBelowAllRows_ClampsToTheFirstAndLastSlot()
    {
        SequenceReorder.SlotAt(ThreeRows, -40).ShouldBe(0);
        SequenceReorder.SlotAt(ThreeRows, 5000).ShouldBe(3);
    }

    [Fact]
    public void SlotAt_NoRows_IsZero() => SequenceReorder.SlotAt(System.Array.Empty<(double, double)>(), 10).ShouldBe(0);

    [Fact]
    public void Plan_OneRowDown_LandsBeforeTheGapItWasDroppedOn()
    {
        SequenceReorder.Plan(4, new[] { 0 }, 3)!.Order.ShouldBe(new[] { 1, 2, 0, 3 });
    }

    [Fact]
    public void Plan_OneRowUp_LandsAtTheGap()
    {
        SequenceReorder.Plan(4, new[] { 3 }, 1)!.Order.ShouldBe(new[] { 0, 3, 1, 2 });
    }

    [Fact]
    public void Plan_DropAtTheEnds_MovesToFirstAndLast()
    {
        SequenceReorder.Plan(3, new[] { 2 }, 0)!.Order.ShouldBe(new[] { 2, 0, 1 });
        SequenceReorder.Plan(3, new[] { 0 }, 3)!.Order.ShouldBe(new[] { 1, 2, 0 });
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Plan_DropOnTheGapAboveOrBelowItself_ChangesNothing(int slot)
    {
        SequenceReorder.Plan(4, new[] { 1 }, slot).ShouldBeNull();
    }

    [Fact]
    public void Plan_BlockMovesTogether_KeepingRelativeOrder_EvenWhenNotAdjacent()
    {
        SequenceReorder.Plan(5, new[] { 1, 3 }, 5)!.Order.ShouldBe(new[] { 0, 2, 4, 1, 3 });
        SequenceReorder.Plan(5, new[] { 1, 3 }, 0)!.Order.ShouldBe(new[] { 1, 3, 0, 2, 4 });
    }

    [Fact]
    public void Plan_MovedIndicesInSelectionOrder_StillKeepDocumentOrder()
    {
        SequenceReorder.Plan(5, new[] { 3, 1 }, 5)!.Order.ShouldBe(new[] { 0, 2, 4, 1, 3 });
    }

    [Fact]
    public void Plan_BlockDroppedInsideItsOwnSpan_ChangesNothing()
    {
        SequenceReorder.Plan(5, new[] { 1, 2, 3 }, 2).ShouldBeNull();
        SequenceReorder.Plan(5, new[] { 1, 2, 3 }, 1).ShouldBeNull();
        SequenceReorder.Plan(5, new[] { 1, 2, 3 }, 4).ShouldBeNull();
    }

    [Fact]
    public void Plan_SeparatedRowsDroppedBetweenThem_GatherAtTheGap()
    {
        SequenceReorder.Plan(5, new[] { 1, 3 }, 2)!.Order.ShouldBe(new[] { 0, 1, 3, 2, 4 });
    }

    [Fact]
    public void Plan_NothingMovedOrOutOfRange_ChangesNothing()
    {
        SequenceReorder.Plan(3, System.Array.Empty<int>(), 1).ShouldBeNull();
        SequenceReorder.Plan(3, new[] { 7 }, 0).ShouldBeNull();
        SequenceReorder.Plan(1, new[] { 0 }, 1).ShouldBeNull();
    }

    [Fact]
    public void Plan_DuplicateIndices_AreOneRow()
    {
        SequenceReorder.Plan(3, new[] { 0, 0 }, 3)!.Order.ShouldBe(new[] { 1, 2, 0 });
    }

    [Fact]
    public void Plan_SlotOutsideTheList_ClampsToTheEnds()
    {
        SequenceReorder.Plan(3, new[] { 0 }, 99)!.Order.ShouldBe(new[] { 1, 2, 0 });
        SequenceReorder.Plan(3, new[] { 2 }, -5)!.Order.ShouldBe(new[] { 2, 0, 1 });
    }

    [Fact]
    public void AutoScrollDelta_InTheMiddle_IsZero() => SequenceReorder.AutoScrollDelta(100, 200, 24, 20).ShouldBe(0);

    [Fact]
    public void AutoScrollDelta_NearTheTop_ScrollsUp_FasterTheCloserToTheEdge()
    {
        double near = SequenceReorder.AutoScrollDelta(20, 200, 24, 20);
        double edge = SequenceReorder.AutoScrollDelta(0, 200, 24, 20);
        near.ShouldBeLessThan(0);
        edge.ShouldBe(-20);
        edge.ShouldBeLessThan(near);
    }

    [Fact]
    public void AutoScrollDelta_NearTheBottom_ScrollsDown_AndPastTheEdgeStaysAtTheMaximum()
    {
        SequenceReorder.AutoScrollDelta(190, 200, 24, 20).ShouldBeGreaterThan(0);
        SequenceReorder.AutoScrollDelta(260, 200, 24, 20).ShouldBe(20);
        SequenceReorder.AutoScrollDelta(-60, 200, 24, 20).ShouldBe(-20);
    }

    [Fact]
    public void AutoScrollDelta_ShortViewport_NeverOverlapsTheTwoEdgeZones()
    {
        // 30 tall with a 24 edge: the zones would overlap, so the middle must not scroll both ways at once.
        SequenceReorder.AutoScrollDelta(15, 30, 24, 20).ShouldBe(0);
    }
}
