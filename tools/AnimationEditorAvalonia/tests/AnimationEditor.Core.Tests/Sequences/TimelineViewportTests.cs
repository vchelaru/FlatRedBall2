using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineViewportTests
{
    [Fact]
    public void ZoomAround_KeepsTheTimeUnderThePointer()
    {
        var viewport = new TimelineViewport();
        viewport.ScrollHorizontally(240);
        double x = TimelineViewport.HeaderWidth + 300;
        double before = viewport.TimeAt(x);

        viewport.ZoomAround(x, 2);

        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.DefaultPixelsPerSecond * 2);
        viewport.TimeAt(x).ShouldBe(before, 1e-9);
    }

    [Fact]
    public void ZoomAround_OutPastTheStart_NeverScrollsBeforeZero()
    {
        var viewport = new TimelineViewport();
        viewport.ScrollHorizontally(10);

        viewport.ZoomAround(TimelineViewport.HeaderWidth + 500, 0.25);

        viewport.ScrollSeconds.ShouldBe(0);
        viewport.XAt(0).ShouldBe(TimelineViewport.HeaderWidth);
    }

    [Fact]
    public void RowAt_MapsRulerLanesNewTrackRowAndBelow_WithVerticalScroll()
    {
        var viewport = new TimelineViewport();
        double lane = TimelineViewport.LaneHeight;
        double firstLane = TimelineViewport.RulerHeight + 1;

        viewport.RowAt(5, trackCount: 2).ShouldBe(new TimelineRow(TimelineRowKind.Ruler));
        viewport.RowAt(firstLane + lane, 2).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
        viewport.RowAt(firstLane + 2 * lane, 2).ShouldBe(new TimelineRow(TimelineRowKind.NewTrack));
        viewport.RowAt(firstLane + 3 * lane, 2).ShouldBe(new TimelineRow(TimelineRowKind.None));

        // Scrolled one lane up in a view one lane tall, the first visible lane is track 1.
        viewport.ScrollVertically(lane, trackCount: 2, viewHeight: TimelineViewport.RulerHeight + lane);
        viewport.RowAt(firstLane, 2).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
    }

    // Lane 0 and 2 carry an event row (60 px), lane 1 does not (48 px).
    private static TimelineViewport MixedViewport()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 60.0, 48.0, 60.0 });
        return viewport;
    }

    [Fact]
    public void LaneTop_FollowsEachTracksOwnHeight_AndTheNewTrackRowIsAFullBaseLane()
    {
        var viewport = MixedViewport();
        double ruler = TimelineViewport.RulerHeight;

        viewport.LaneTop(0).ShouldBe(ruler);
        viewport.LaneTop(1).ShouldBe(ruler + 60);
        viewport.LaneTop(2).ShouldBe(ruler + 108);
        viewport.LaneTop(3).ShouldBe(ruler + 168);
        viewport.LaneHeightOf(0).ShouldBe(60);
        viewport.LaneHeightOf(1).ShouldBe(48);
        viewport.LaneHeightOf(3).ShouldBe(TimelineViewport.LaneHeight);
        viewport.ContentHeight(3).ShouldBe(ruler + 168 + TimelineViewport.LaneHeight);
    }

    [Fact]
    public void RowAt_AMixOfTallAndNormalLanes_PutsEachBoundaryAtItsOwnHeight()
    {
        var viewport = MixedViewport();
        double ruler = TimelineViewport.RulerHeight;

        viewport.RowAt(ruler + 59, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 0));
        viewport.RowAt(ruler + 61, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
        viewport.RowAt(ruler + 107, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
        viewport.RowAt(ruler + 109, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 2));
        viewport.RowAt(ruler + 169, 3).ShouldBe(new TimelineRow(TimelineRowKind.NewTrack));
        viewport.RowAt(ruler + 168 + TimelineViewport.LaneHeight + 1, 3).ShouldBe(new TimelineRow(TimelineRowKind.None));
    }

    [Fact]
    public void ScrollVertically_UsesTheTallLanesInTheContentExtent()
    {
        var viewport = MixedViewport();

        viewport.ScrollVertically(1_000_000, trackCount: 3, viewHeight: 100);

        viewport.ScrollY.ShouldBe(TimelineViewport.RulerHeight + 168 + TimelineViewport.LaneHeight - 100);
        viewport.LaneTop(0).ShouldBe(TimelineViewport.RulerHeight - viewport.ScrollY);
    }

    [Fact]
    public void SetLaneHeights_ForFewerTracksThanAskedAbout_TreatsTheRestAsBaseLanes()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 60.0 });

        viewport.LaneTop(2).ShouldBe(TimelineViewport.RulerHeight + 60 + TimelineViewport.LaneHeight);
        viewport.RowAt(TimelineViewport.RulerHeight + 61, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
    }

    [Fact]
    public void ScrubTime_IsTheTimeUnderX_ClampedToTheSequence()
    {
        var viewport = new TimelineViewport();
        double pps = TimelineViewport.DefaultPixelsPerSecond;

        viewport.ScrubTime(TimelineViewport.HeaderWidth + pps * 1.5, length: 2).ShouldBe(1.5, 1e-9);
        viewport.ScrubTime(TimelineViewport.HeaderWidth + pps * 5, length: 2).ShouldBe(2);
        viewport.ScrubTime(0, length: 2).ShouldBe(0);
    }

    [Fact]
    public void SetPixelsPerSecond_KeepsTheLeftEdgeAndClamps()
    {
        var viewport = new TimelineViewport();
        viewport.ScrollHorizontally(240);
        double left = viewport.ScrollSeconds;

        viewport.SetPixelsPerSecond(300);
        viewport.PixelsPerSecond.ShouldBe(300);
        viewport.ScrollSeconds.ShouldBe(left, 1e-9);
        viewport.SetPixelsPerSecond(1e9);
        viewport.PixelsPerSecond.ShouldBe(TimelineViewport.MaxPixelsPerSecond);
    }

    [Fact]
    public void SetScrollSeconds_SetsTheLeftEdge_NeverBeforeZero()
    {
        var viewport = new TimelineViewport();

        viewport.SetScrollSeconds(4.5);
        viewport.ScrollSeconds.ShouldBe(4.5);
        viewport.SetScrollSeconds(-3);
        viewport.ScrollSeconds.ShouldBe(0);
    }

    [Fact]
    public void SetScrollY_SetsTheOffset_WithinTheContent()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 48.0, 48.0, 48.0, 48.0 });
        double viewHeight = 200;
        double max = viewport.ContentHeight(4) - viewHeight;

        viewport.SetScrollY(30, 4, viewHeight);
        viewport.ScrollY.ShouldBe(30);
        viewport.SetScrollY(1e6, 4, viewHeight);
        viewport.ScrollY.ShouldBe(max);
    }

    [Fact]
    public void Changed_FiresForEveryMutatorThatMovesTheView()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 48.0, 48.0, 48.0, 48.0 });
        int count = 0;
        viewport.Changed += () => count++;

        viewport.ScrollHorizontally(50);
        viewport.SetScrollSeconds(2);
        viewport.ZoomAround(TimelineViewport.HeaderWidth + 100, 2);
        viewport.SetPixelsPerSecond(300);
        viewport.ScrollVertically(20, 4, 200);
        viewport.SetScrollY(40, 4, 200);
        viewport.SetLaneHeights(new[] { 60.0, 48.0 });

        count.ShouldBe(7);
    }
}
