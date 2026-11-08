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
        // Lane 0 is 60 tall (event row), lanes 1 and 2 are 48. Under the 22 px ruler and the 48 px new-track row they span
        // 70-130, 130-178, 178-226.
        var viewport = Viewport(60, 48, 48);

        TrackReorder.SlotAt(viewport, 3, 88).ShouldBe(0);   // upper half of lane 0 (middle is 100)
        TrackReorder.SlotAt(viewport, 3, 108).ShouldBe(1);  // lower half of lane 0, inside its event row
        TrackReorder.SlotAt(viewport, 3, 148).ShouldBe(1);  // upper half of lane 1 (middle is 154)
        TrackReorder.SlotAt(viewport, 3, 158).ShouldBe(2);
        TrackReorder.SlotAt(viewport, 3, 218).ShouldBe(3);  // lower half of the last lane
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
        viewport.ScrollVertically(96, 4, 100);

        // The new-track row and lane 0 are scrolled away: lane 1 now starts right under the ruler (y 22), so y 30 is its upper half.
        TrackReorder.SlotAt(viewport, 4, 30).ShouldBe(1);
    }

    [Fact]
    public void SlotAt_NoTracks_IsZero() => TrackReorder.SlotAt(Viewport(), 0, 100).ShouldBe(0);

    [Fact]
    public void SlotY_IsTheTopOfTheLaneBelowTheGap_AndTheBottomOfTheLastLaneForTheLastSlot()
    {
        var viewport = Viewport(60, 48);

        TrackReorder.SlotY(viewport, 1).ShouldBe(130);
        TrackReorder.SlotY(viewport, 2).ShouldBe(178);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    public void TargetLane_DroppingOnTheGapsNextToItself_ChangesNothing(int from, int slot) =>
        TrackReorder.TargetLane(from, slot).ShouldBeNull();

    [Theory]
    [InlineData(0, 3, 2)]
    [InlineData(2, 0, 0)]
    [InlineData(1, 3, 2)]
    [InlineData(3, 1, 1)]
    public void TargetLane_DroppingOnAFartherGap_IsTheLaneItEndsUpIn(int from, int slot, int expected) =>
        TrackReorder.TargetLane(from, slot).ShouldBe(expected);
}
