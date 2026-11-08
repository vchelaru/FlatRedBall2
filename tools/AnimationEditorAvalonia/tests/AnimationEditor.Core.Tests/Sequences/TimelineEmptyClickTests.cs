using AnimationEditor.Core.Sequences;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineEmptyClickTests
{
    [Fact]
    public void IsEmptyLaneSpace_IsALaneOrTheBlankAreaUnderTheLastLane_ButNotTheRulerOrTheNewTrackRow()
    {
        var viewport = new TimelineViewport();
        const int tracks = 2;
        double belowLanes = viewport.LaneTop(tracks) + 5;

        TimelineEmptyClick.IsEmptyLaneSpace(viewport.RowAt(viewport.LaneTop(1) + 5, tracks)).ShouldBeTrue();
        TimelineEmptyClick.IsEmptyLaneSpace(viewport.RowAt(belowLanes, tracks)).ShouldBeTrue();
        TimelineEmptyClick.IsEmptyLaneSpace(viewport.RowAt(belowLanes, 0)).ShouldBeTrue();
        TimelineEmptyClick.IsEmptyLaneSpace(viewport.RowAt(2, tracks)).ShouldBeFalse();
        TimelineEmptyClick.IsEmptyLaneSpace(viewport.RowAt(viewport.LaneTop(TimelineViewport.NewTrackLane) + 5, tracks)).ShouldBeFalse();
    }

    [Fact]
    public void PlayheadTime_PlainClickOnEmptyLane_IsTheTimeUnderThePress()
    {
        var viewport = new TimelineViewport();
        double x = viewport.XAt(1.375);

        double? time = TimelineEmptyClick.PlayheadTime(viewport, 3.0, pressedOnEmptyLane: true, modified: false, dragged: false, x);

        time.ShouldNotBeNull().ShouldBe(1.375, 1e-9);
    }

    [Fact]
    public void PlayheadTime_UsesZoomAndScroll()
    {
        var viewport = new TimelineViewport();
        viewport.SetScrollSeconds(2.0);
        viewport.ZoomAround(TimelineViewport.HeaderWidth, 2.0);
        double x = viewport.XAt(2.8);

        double? time = TimelineEmptyClick.PlayheadTime(viewport, 10.0, true, false, false, x);

        time.ShouldNotBeNull().ShouldBe(2.8, 1e-9);
    }

    [Fact]
    public void PlayheadTime_PastTheEnd_ClampsToTheSequenceLength_AndBeforeTheStartToZero()
    {
        var viewport = new TimelineViewport();

        TimelineEmptyClick.PlayheadTime(viewport, 2.2, true, false, false, viewport.XAt(5.0)).ShouldBe(2.2);
        TimelineEmptyClick.PlayheadTime(viewport, 2.2, true, false, false, TimelineViewport.HeaderWidth - 40).ShouldBe(0);
    }

    [Theory]
    [InlineData(false, false, false)]   // pressed on a chain, a header, the new-track row...
    [InlineData(true, true, false)]     // Ctrl or Shift keeps its multi-select meaning
    [InlineData(true, false, true)]     // the press became a rubber band
    public void PlayheadTime_AnyOtherPress_DoesNotSeek(bool onEmptyLane, bool modified, bool dragged)
    {
        var viewport = new TimelineViewport();

        TimelineEmptyClick.PlayheadTime(viewport, 3.0, onEmptyLane, modified, dragged, viewport.XAt(1.0)).ShouldBeNull();
    }
}
