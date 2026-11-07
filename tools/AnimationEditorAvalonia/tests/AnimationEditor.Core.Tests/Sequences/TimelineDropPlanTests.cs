using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineDropPlanTests
{
    private const double ViewWidth = 800;

    // A point `seconds` into the lanes (default zoom, no scroll), vertically in the middle of `row`.
    private static (double X, double Y) Point(TimelineViewport viewport, double seconds, int row) =>
        (viewport.XAt(seconds), viewport.LaneTop(row) + TimelineViewport.LaneHeight / 2);

    [Fact]
    public void For_BelowATallLane_TargetsTheNextTrack_AndItsEventRowStillBelongsToTheTallOne()
    {
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { TimelineViewport.LaneHeight + 12, TimelineViewport.LaneHeight });
        double x = viewport.XAt(0.5);

        var inEventRow = TimelineDropPlan.For(viewport, ViewWidth, 2, x, viewport.LaneTop(0) + TimelineViewport.LaneHeight + 6, 0.5);
        var firstOfNext = TimelineDropPlan.For(viewport, ViewWidth, 2, x, viewport.LaneTop(1) + 2, 0.5);
        var newTrack = TimelineDropPlan.For(viewport, ViewWidth, 2, x, viewport.LaneTop(2) + 2, 0.5);

        inEventRow.Row.ShouldBe(0);
        inEventRow.Kind.ShouldBe(TimelineDropKind.Track);
        firstOfNext.Row.ShouldBe(1);
        newTrack.Kind.ShouldBe(TimelineDropKind.NewTrack);
        newTrack.Row.ShouldBe(2);
    }

    [Fact]
    public void For_OverATrackLane_TargetsThatTrackAtTheSnappedTime()
    {
        var viewport = new TimelineViewport();
        var (x, y) = Point(viewport, 0.87, row: 1);

        var plan = TimelineDropPlan.For(viewport, ViewWidth, trackCount: 2, x, y, cycleLength: 0.5);

        plan.Kind.ShouldBe(TimelineDropKind.Track);
        plan.Row.ShouldBe(1);
        plan.Start.ShouldBe(0.85, 1e-9);
        plan.Length.ShouldBe(0.5);
        plan.Left.ShouldBe(viewport.XAt(0.85), 1e-9);
        plan.Width.ShouldBe(0.5 * viewport.PixelsPerSecond, 1e-9);
    }

    [Fact]
    public void For_OverTheNewTrackRow_TargetsANewTrackAtTheSameSnappedTime()
    {
        var viewport = new TimelineViewport();
        var (x, y) = Point(viewport, 0.87, row: 2);

        var plan = TimelineDropPlan.For(viewport, ViewWidth, trackCount: 2, x, y, cycleLength: 0.2);

        plan.Kind.ShouldBe(TimelineDropKind.NewTrack);
        plan.Row.ShouldBe(2);
        plan.Start.ShouldBe(0.85, 1e-9);
        plan.TimeLabel.ShouldBe("0.85 s");
    }

    [Fact]
    public void For_AMissingChain_UsesTheMissingLength()
    {
        var viewport = new TimelineViewport();
        var (x, y) = Point(viewport, 0.5, row: 0);

        var plan = TimelineDropPlan.For(viewport, ViewWidth, trackCount: 1, x, y, cycleLength: 0);

        plan.Length.ShouldBe(TimelineLayout.MissingLength);
    }

    [Fact]
    public void For_OverTheHeaderOrPastTheRightEdge_TargetsNothing()
    {
        var viewport = new TimelineViewport();
        double y = viewport.LaneTop(0) + TimelineViewport.LaneHeight / 2;

        TimelineDropPlan.For(viewport, ViewWidth, 1, TimelineViewport.HeaderWidth - 5, y, 0.5).Kind.ShouldBe(TimelineDropKind.None);
        TimelineDropPlan.For(viewport, ViewWidth, 1, ViewWidth + 5, y, 0.5).Kind.ShouldBe(TimelineDropKind.None);
    }

    [Fact]
    public void For_OverTheRulerOrBelowTheNewTrackRow_TargetsNothing()
    {
        var viewport = new TimelineViewport();
        double x = viewport.XAt(1);

        TimelineDropPlan.For(viewport, ViewWidth, 1, x, TimelineViewport.RulerHeight - 2, 0.5).Kind.ShouldBe(TimelineDropKind.None);
        TimelineDropPlan.For(viewport, ViewWidth, 1, x, viewport.LaneTop(3), 0.5).Kind.ShouldBe(TimelineDropKind.None);
    }

    [Fact]
    public void For_WhenScrolledAlong_StartsAtTheTimeUnderThePointer_NotTheTimeFromTheOrigin()
    {
        var viewport = new TimelineViewport();
        viewport.ScrollHorizontally(240);
        var (x, y) = Point(viewport, 3.0, row: 0);

        var plan = TimelineDropPlan.For(viewport, ViewWidth, 1, x, y, 0.5);

        plan.Kind.ShouldBe(TimelineDropKind.Track);
        plan.Start.ShouldBe(3.0, 1e-9);
        plan.Left.ShouldBe(x, 1e-9);
    }

    [Fact]
    public void For_AtExactlyTheRightEdge_StillTargetsTheLane()
    {
        var viewport = new TimelineViewport();
        double y = viewport.LaneTop(0) + TimelineViewport.LaneHeight / 2;

        TimelineDropPlan.For(viewport, ViewWidth, 1, ViewWidth, y, 0.5).Kind.ShouldBe(TimelineDropKind.Track);
    }

    [Fact]
    public void For_AtTheLeftEdgeOfTheLanes_StartsAtZero()
    {
        var viewport = new TimelineViewport();
        var (_, y) = Point(viewport, 0, row: 0);

        var plan = TimelineDropPlan.For(viewport, ViewWidth, 1, TimelineViewport.HeaderWidth, y, 0.5);

        plan.Start.ShouldBe(0);
        plan.TimeLabel.ShouldBe("0 s");
    }

    [Fact]
    public void For_NearAnotherChainsEnd_StartsExactlyThere_AndNamesTheTarget()
    {
        var viewport = new TimelineViewport();
        var targets = new[] { new SnapTarget(1.5, SnapKind.ChainEdge, Track: 0, Chain: 0) };
        var (x, y) = Point(viewport, 1.5 + 4 / viewport.PixelsPerSecond, row: 1);

        var plan = TimelineDropPlan.For(viewport, ViewWidth, 2, x, y, cycleLength: 0.5, targets);

        plan.Start.ShouldBe(1.5, 1e-9);
        plan.Snapped!.Value.Time.ShouldBe(1.5);
    }

    [Fact]
    public void For_TheGhostsEndNearAnotherChainsStart_SnapsItsEnd()
    {
        var viewport = new TimelineViewport();
        var targets = new[] { new SnapTarget(2.0, SnapKind.ChainEdge, 0, 0) };
        var (x, y) = Point(viewport, 1.53, row: 1);

        // A 0.5 s ghost at 1.53 ends at 2.03: its end is pulled to 2.0, so it starts at 1.5.
        var plan = TimelineDropPlan.For(viewport, ViewWidth, 2, x, y, cycleLength: 0.5, targets);

        plan.Start.ShouldBe(1.5, 1e-9);
    }

    [Fact]
    public void For_Bypass_IgnoresTargetsAndTheGrid()
    {
        var viewport = new TimelineViewport();
        var targets = new[] { new SnapTarget(1.5, SnapKind.ChainEdge, 0, 0) };
        var (x, y) = Point(viewport, 1.5 + 4 / viewport.PixelsPerSecond, row: 1);

        var plan = TimelineDropPlan.For(viewport, ViewWidth, 2, x, y, cycleLength: 0.5, targets, bypass: true);

        plan.Start.ShouldBe(1.5 + 4 / viewport.PixelsPerSecond, 1e-9);
        plan.Snapped.ShouldBeNull();
    }
}
