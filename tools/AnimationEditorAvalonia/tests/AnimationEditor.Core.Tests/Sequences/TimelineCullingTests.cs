using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineCullingTests
{
    private static TimelineBlock Block(int track, double start, double played, double full) =>
        new(track, 0, new SequenceChainReferenceSave { Start = start }, played, full, CycleLength: 1, IsMissing: false);

    // 120 px per second, nothing scrolled: time t is at x = 120 + 120 t. The lanes span x 120..1120 (a 1120 px wide view).
    private const double ViewWidth = 1120;
    private const double ViewHeight = 400;

    [Fact]
    public void IsBlockVisible_BlockInsideTheLanes_IsTrue()
    {
        var viewport = new TimelineViewport();

        TimelineCulling.IsBlockVisible(viewport, Block(0, 2, 1, 1), ViewWidth, ViewHeight).ShouldBeTrue();
    }

    [Fact]
    public void IsBlockVisible_BlockEndingLeftOfTheLanes_IsFalse()
    {
        var viewport = new TimelineViewport();
        viewport.ScrollHorizontally(5 * viewport.PixelsPerSecond);

        // Ends at 3 s; the lanes now start at 5 s.
        TimelineCulling.IsBlockVisible(viewport, Block(0, 2, 1, 1), ViewWidth, ViewHeight).ShouldBeFalse();
    }

    [Fact]
    public void IsBlockVisible_BlockStartingRightOfTheLanes_IsFalse()
    {
        var viewport = new TimelineViewport();

        // The lanes end at about 8.3 s.
        TimelineCulling.IsBlockVisible(viewport, Block(0, 9, 1, 1), ViewWidth, ViewHeight).ShouldBeFalse();
    }

    [Fact]
    public void IsBlockVisible_CutTailReachingIntoTheLanes_IsTrue()
    {
        var viewport = new TimelineViewport();
        viewport.ScrollHorizontally(5 * viewport.PixelsPerSecond);

        // Plays only until 3 s, but the hatched tail runs to 6 s, which is on screen.
        TimelineCulling.IsBlockVisible(viewport, Block(0, 2, 1, 4), ViewWidth, ViewHeight).ShouldBeTrue();
    }

    [Fact]
    public void IsBlockVisible_LaneScrolledAboveTheView_IsFalse()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 48.0, 48.0, 48.0, 48.0 });
        // Scrolled past the new-track row and lane 0: lane 1 is above the view, lane 2 is the first one in it.
        viewport.ScrollVertically(3 * 48, trackCount: 4, viewHeight: TimelineViewport.RulerHeight + 96);

        TimelineCulling.IsBlockVisible(viewport, Block(1, 2, 1, 1), ViewWidth, TimelineViewport.RulerHeight + 96).ShouldBeFalse();
        TimelineCulling.IsBlockVisible(viewport, Block(2, 2, 1, 1), ViewWidth, TimelineViewport.RulerHeight + 96).ShouldBeTrue();
    }

    [Fact]
    public void IsBlockVisible_LaneBelowTheView_IsFalse()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { 48.0, 48.0, 48.0 });

        TimelineCulling.IsBlockVisible(viewport, Block(2, 2, 1, 1), ViewWidth, TimelineViewport.RulerHeight + 48).ShouldBeFalse();
    }
}
