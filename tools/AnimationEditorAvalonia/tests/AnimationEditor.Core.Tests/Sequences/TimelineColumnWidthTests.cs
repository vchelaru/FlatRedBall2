using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

// The track-name column is resizable (#1765): every x mapping reads its width from the viewport.
public class TimelineColumnWidthTests
{
    private static TimelineBlock Block(int track, double start, double played, double full) =>
        new(track, 0, new SequenceChainReferenceSave { Start = start }, played, full, CycleLength: 1, IsMissing: false);

    [Fact]
    public void ColumnWidth_DefaultsToTheOldFixedWidth()
    {
        new TimelineViewport().ColumnWidth.ShouldBe(TimelineViewport.HeaderWidth);
    }

    [Theory]
    [InlineData(10, TimelineViewport.MinColumnWidth)]
    [InlineData(5000, TimelineViewport.MaxColumnWidth)]
    [InlineData(200, 200)]
    [InlineData(double.NaN, TimelineViewport.HeaderWidth)]
    [InlineData(double.PositiveInfinity, TimelineViewport.HeaderWidth)]
    public void SetColumnWidth_IsClampedToTheLimits(double requested, double expected)
    {
        var viewport = new TimelineViewport();

        viewport.SetColumnWidth(requested);

        viewport.ColumnWidth.ShouldBe(expected);
    }

    [Fact]
    public void XAt_AndTimeAt_UseTheColumnWidthAsTheLaneOrigin()
    {
        var viewport = new TimelineViewport();
        viewport.SetColumnWidth(300);

        viewport.XAt(0).ShouldBe(300);
        viewport.XAt(2).ShouldBe(300 + 2 * viewport.PixelsPerSecond, 1e-9);
        viewport.TimeAt(300).ShouldBe(0, 1e-9);
        viewport.TimeAt(viewport.XAt(1.5)).ShouldBe(1.5, 1e-9);
    }

    [Fact]
    public void SetColumnWidth_KeepsTheScrollTimeAtTheLaneEdge()
    {
        var viewport = new TimelineViewport();
        viewport.SetScrollSeconds(3);

        viewport.SetColumnWidth(250);

        viewport.TimeAt(250).ShouldBe(3, 1e-9);
    }

    [Fact]
    public void SetColumnWidth_RaisesChangedOnlyWhenTheWidthChanges()
    {
        var viewport = new TimelineViewport();
        int raised = 0;
        viewport.Changed += () => raised++;

        viewport.SetColumnWidth(200);
        viewport.SetColumnWidth(200);
        viewport.SetColumnWidth(5000);
        viewport.SetColumnWidth(5000);

        raised.ShouldBe(2);
    }

    [Fact]
    public void ZoomAround_KeepsTheTimeUnderThePivot_WithAWiderColumn()
    {
        var viewport = new TimelineViewport();
        viewport.SetColumnWidth(300);
        double pivot = 500;
        double before = viewport.TimeAt(pivot);

        viewport.ZoomAround(pivot, 2);

        viewport.TimeAt(pivot).ShouldBe(before, 1e-9);
    }

    [Fact]
    public void DropPlan_RejectsAPointInsideAWiderColumn_AndAcceptsOneInTheLanes()
    {
        var viewport = new TimelineViewport();
        viewport.SetColumnWidth(300);
        double y = viewport.LaneTop(0) + 10;

        TimelineDropPlan.For(viewport, 1000, 1, 200, y, 1).Kind.ShouldBe(TimelineDropKind.None);
        TimelineDropPlan.For(viewport, 1000, 1, 400, y, 1).Kind.ShouldBe(TimelineDropKind.Track);
    }

    [Fact]
    public void Culling_UsesTheColumnWidthAsTheLeftEdgeOfTheLanes()
    {
        var viewport = new TimelineViewport();
        viewport.SetColumnWidth(300);
        viewport.SetScrollSeconds(1.5);
        // The block spans x 120..240: past the default column's edge (120) but wholly under the 300 px column.

        TimelineCulling.IsBlockVisible(viewport, Block(0, 0, 1, 1), 1000, 400).ShouldBeFalse();
    }

    [Fact]
    public void HorizontalScrollView_ShrinksAsTheColumnGrows()
    {
        var narrow = TimelineScrollBars.Horizontal(0, 100, 1000, 10, columnWidth: 120);
        var wide = TimelineScrollBars.Horizontal(0, 100, 1000, 10, columnWidth: 320);

        (narrow.ViewportSize - wide.ViewportSize).ShouldBe(200 / 100.0, 1e-9);
    }

    [Fact]
    public void PixelsPerSecondToFit_LeavesRoomForAWiderColumn()
    {
        double view = 120 + TimelineScrollBars.EndMarginPixels + 600;

        TimelineViewport.PixelsPerSecondToFit(3, view + 200, columnWidth: 320).ShouldBe(200, 1e-9);
    }
}
