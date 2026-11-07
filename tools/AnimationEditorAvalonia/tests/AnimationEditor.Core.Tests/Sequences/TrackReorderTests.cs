using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>Dragging a track header to a gap between lanes: which gap, and the new order with its Z renumbering.</summary>
public class TrackReorderTests
{
    private static TimelineViewport Viewport(params double[] heights)
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(heights);
        return viewport;
    }

    [Fact]
    public void SlotAt_VariableLaneHeights_SplitsEachLaneAtItsOwnMiddle()
    {
        // Lane 0 is 60 tall (event row), lanes 1 and 2 are 48. Under the 22 px ruler they span 22-82, 82-130, 130-178.
        var viewport = Viewport(60, 48, 48);

        TrackReorder.SlotAt(viewport, 3, 40).ShouldBe(0);   // upper half of lane 0 (middle is 52)
        TrackReorder.SlotAt(viewport, 3, 60).ShouldBe(1);   // lower half of lane 0, inside its event row
        TrackReorder.SlotAt(viewport, 3, 100).ShouldBe(1);  // upper half of lane 1 (middle is 106)
        TrackReorder.SlotAt(viewport, 3, 110).ShouldBe(2);
        TrackReorder.SlotAt(viewport, 3, 170).ShouldBe(3);  // lower half of the last lane
    }

    [Fact]
    public void SlotAt_AboveAndBelowAllLanes_ClampsToFirstAndLastSlot()
    {
        var viewport = Viewport(48, 48);

        TrackReorder.SlotAt(viewport, 2, -50).ShouldBe(0);
        TrackReorder.SlotAt(viewport, 2, 5000).ShouldBe(2);
    }

    [Fact]
    public void SlotAt_ScrolledLanes_UsesTheScrolledPositions()
    {
        var viewport = Viewport(48, 48, 48, 48);
        viewport.ScrollVertically(48, 4, 100);

        // Lane 1 now starts right under the ruler (y 22), so y 30 is its upper half.
        TrackReorder.SlotAt(viewport, 4, 30).ShouldBe(1);
    }

    [Fact]
    public void SlotAt_NoTracks_IsZero() => TrackReorder.SlotAt(Viewport(), 0, 100).ShouldBe(0);

    [Fact]
    public void SlotY_IsTheTopOfTheLaneBelowTheGap_AndTheBottomOfTheLastLaneForTheLastSlot()
    {
        var viewport = Viewport(60, 48);

        TrackReorder.SlotY(viewport, 1).ShouldBe(82);
        TrackReorder.SlotY(viewport, 2).ShouldBe(130);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    public void Plan_DroppingOnTheGapsNextToItself_ChangesNothing(int from, int slot) =>
        TrackReorder.Plan(new[] { 0, 1, 2 }, from, slot).ShouldBeNull();

    [Fact]
    public void Plan_SingleTrack_ChangesNothing()
    {
        TrackReorder.Plan(new[] { 4 }, 0, 0).ShouldBeNull();
        TrackReorder.Plan(new[] { 4 }, 0, 1).ShouldBeNull();
    }

    [Fact]
    public void Plan_FirstTrackToTheEnd_RaisesItsZToTheLastNeighbours()
    {
        var plan = TrackReorder.Plan(new[] { 0, 1, 2 }, from: 0, slot: 3)!;

        plan.Order.ShouldBe(new[] { 1, 2, 0 });
        plan.Z.ShouldBe(new[] { 1, 2, 2 });
        plan.To.ShouldBe(2);
    }

    [Fact]
    public void Plan_LastTrackToTheStart_LowersItsZToTheFirstNeighbours()
    {
        var plan = TrackReorder.Plan(new[] { 0, 1, 2 }, from: 2, slot: 0)!;

        plan.Order.ShouldBe(new[] { 2, 0, 1 });
        plan.Z.ShouldBe(new[] { 0, 0, 1 });
        plan.To.ShouldBe(0);
    }

    [Fact]
    public void Plan_MoveDownOne_KeepsEveryZWhenTheyAlreadyFit()
    {
        // The moved track's Z 5 sits between its new neighbours' 5 and 9.
        var plan = TrackReorder.Plan(new[] { 2, 5, 5, 9 }, from: 1, slot: 3)!;

        plan.Order.ShouldBe(new[] { 0, 2, 1, 3 });
        plan.Z.ShouldBe(new[] { 2, 5, 5, 9 });
        plan.To.ShouldBe(2);
    }

    [Fact]
    public void Plan_AllEqualZs_ReordersWithoutChangingAnyZ()
    {
        var plan = TrackReorder.Plan(new[] { 0, 0, 0, 0 }, from: 3, slot: 1)!;

        plan.Order.ShouldBe(new[] { 0, 3, 1, 2 });
        plan.Z.ShouldBe(new[] { 0, 0, 0, 0 });
        plan.To.ShouldBe(1);
    }

    [Fact]
    public void Plan_MovedZOutsideItsNewNeighboursRange_TakesTheNearestNeighbourZ()
    {
        // Z 4 moves below two Z 10s, so it can only stay in front of them by becoming 10.
        var plan = TrackReorder.Plan(new[] { 0, 4, 10, 10 }, from: 1, slot: 3)!;

        plan.Order.ShouldBe(new[] { 0, 2, 1, 3 });
        plan.Z.ShouldBe(new[] { 0, 10, 10, 10 });
    }

    [Fact]
    public void Plan_ZsThatDidNotAscendBefore_RenumberEveryTrackToItsLane()
    {
        // The middle lane's Z 0 sits below its upper neighbour's 5: lane order was not draw order to begin with.
        var plan = TrackReorder.Plan(new[] { 5, 0, 9 }, from: 2, slot: 0)!;

        plan.Order.ShouldBe(new[] { 2, 0, 1 });
        plan.Z.ShouldBe(new[] { 0, 1, 2 });
        plan.To.ShouldBe(0);
    }

    [Fact]
    public void Plan_NegativeZs_StillAscendAfterTheMove()
    {
        var plan = TrackReorder.Plan(new[] { -3, -1, 7 }, from: 0, slot: 3)!;

        plan.Order.ShouldBe(new[] { 1, 2, 0 });
        plan.Z.ShouldBe(new[] { -1, 7, 7 });
    }
}
