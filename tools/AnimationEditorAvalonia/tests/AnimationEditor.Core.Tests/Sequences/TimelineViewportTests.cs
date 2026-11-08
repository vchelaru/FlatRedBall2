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
    public void RowAt_MapsRulerNewTrackRowLanesAndBelow_WithVerticalScroll()
    {
        var viewport = new TimelineViewport();
        double lane = TimelineViewport.LaneHeight;
        double firstRow = TimelineViewport.RulerHeight + 1;

        viewport.RowAt(5, trackCount: 2).ShouldBe(new TimelineRow(TimelineRowKind.Ruler));
        viewport.RowAt(firstRow, 2).ShouldBe(new TimelineRow(TimelineRowKind.NewTrack));
        viewport.RowAt(firstRow + lane, 2).ShouldBe(new TimelineRow(TimelineRowKind.Track, 0));
        viewport.RowAt(firstRow + 2 * lane, 2).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
        viewport.RowAt(firstRow + 3 * lane, 2).ShouldBe(new TimelineRow(TimelineRowKind.None));

        // Scrolled one lane up in a view one lane tall, the new-track row is gone and the first visible lane is track 0.
        viewport.ScrollVertically(lane, trackCount: 2, viewHeight: TimelineViewport.RulerHeight + lane);
        viewport.RowAt(firstRow, 2).ShouldBe(new TimelineRow(TimelineRowKind.Track, 0));
    }

    [Fact]
    public void RowAt_NoTracks_TheNewTrackRowIsTheOnlyRowUnderTheRuler()
    {
        var viewport = new TimelineViewport();

        viewport.RowAt(TimelineViewport.RulerHeight + 1, trackCount: 0).ShouldBe(new TimelineRow(TimelineRowKind.NewTrack));
        viewport.RowAt(TimelineViewport.RulerHeight + TimelineViewport.LaneHeight + 1, 0).ShouldBe(new TimelineRow(TimelineRowKind.None));
    }

    // Lane 0 and 2 carry an event row (60 px), lane 1 does not (48 px).
    private static TimelineViewport MixedViewport()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 60.0, 48.0, 60.0 });
        return viewport;
    }

    [Fact]
    public void LaneTop_FollowsEachTracksOwnHeight_BelowTheNewTrackRowAtTheTop()
    {
        var viewport = MixedViewport();
        double ruler = TimelineViewport.RulerHeight;
        double newRow = TimelineViewport.LaneHeight;

        viewport.LaneTop(0).ShouldBe(ruler + newRow);
        viewport.LaneTop(1).ShouldBe(ruler + newRow + 60);
        viewport.LaneTop(2).ShouldBe(ruler + newRow + 108);
        viewport.LaneTop(3).ShouldBe(ruler + newRow + 168);
        viewport.LaneHeightOf(0).ShouldBe(60);
        viewport.LaneHeightOf(1).ShouldBe(48);
        viewport.LaneHeightOf(3).ShouldBe(TimelineViewport.LaneHeight);
        viewport.ContentHeight(3).ShouldBe(ruler + newRow + 168);
    }

    [Fact]
    public void RowAt_AMixOfTallAndNormalLanes_PutsEachBoundaryAtItsOwnHeight()
    {
        var viewport = MixedViewport();
        double lanes = TimelineViewport.RulerHeight + TimelineViewport.LaneHeight;

        viewport.RowAt(TimelineViewport.RulerHeight + 1, 3).ShouldBe(new TimelineRow(TimelineRowKind.NewTrack));
        viewport.RowAt(lanes + 59, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 0));
        viewport.RowAt(lanes + 61, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
        viewport.RowAt(lanes + 107, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
        viewport.RowAt(lanes + 109, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 2));
        viewport.RowAt(lanes + 169, 3).ShouldBe(new TimelineRow(TimelineRowKind.None));
    }

    [Fact]
    public void Pan_MovesTheContentWithThePointer_OnBothAxesAtOnce()
    {
        var viewport = new TimelineViewport();
        viewport.SetScrollSeconds(5);
        viewport.ScrollVertically(100, trackCount: 10, viewHeight: 200);

        // Dragging left by 120 px (one second at the default zoom) and up by 30 shows later time and lower lanes.
        viewport.Pan(-TimelineViewport.DefaultPixelsPerSecond, -30, trackCount: 10, viewHeight: 200);

        viewport.ScrollSeconds.ShouldBe(6, 1e-9);
        viewport.ScrollY.ShouldBe(130);
        viewport.Pan(TimelineViewport.DefaultPixelsPerSecond * 2, 50, trackCount: 10, viewHeight: 200);
        viewport.ScrollSeconds.ShouldBe(4, 1e-9);
        viewport.ScrollY.ShouldBe(80);
    }

    [Fact]
    public void Pan_StopsAtTimeZeroAndAtBothEndsOfTheLanes()
    {
        var viewport = new TimelineViewport();
        viewport.SetScrollSeconds(1);

        viewport.Pan(100_000, 100_000, trackCount: 10, viewHeight: 200);
        viewport.ScrollSeconds.ShouldBe(0);
        viewport.ScrollY.ShouldBe(0);

        viewport.Pan(0, -1_000_000, trackCount: 10, viewHeight: 200);
        viewport.ScrollY.ShouldBe(viewport.ContentHeight(10) - 200);
        // Everything fits: nothing to pan vertically.
        viewport.Pan(0, -50, trackCount: 1, viewHeight: 500);
        viewport.ScrollY.ShouldBe(0);
    }

    [Fact]
    public void ScrollVertically_UsesTheTallLanesInTheContentExtent()
    {
        var viewport = MixedViewport();

        viewport.ScrollVertically(1_000_000, trackCount: 3, viewHeight: 100);

        viewport.ScrollY.ShouldBe(TimelineViewport.RulerHeight + TimelineViewport.LaneHeight + 168 - 100);
        viewport.LaneTop(0).ShouldBe(TimelineViewport.RulerHeight + TimelineViewport.LaneHeight - viewport.ScrollY);
    }

    [Fact]
    public void SetLaneHeights_ForFewerTracksThanAskedAbout_TreatsTheRestAsBaseLanes()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 60.0 });

        double lanes = TimelineViewport.RulerHeight + TimelineViewport.LaneHeight;
        viewport.LaneTop(2).ShouldBe(lanes + 60 + TimelineViewport.LaneHeight);
        viewport.RowAt(lanes + 61, 3).ShouldBe(new TimelineRow(TimelineRowKind.Track, 1));
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
