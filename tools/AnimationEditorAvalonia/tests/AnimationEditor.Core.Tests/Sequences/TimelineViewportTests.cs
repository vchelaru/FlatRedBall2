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
}
