using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineCursorTests
{
    private static TimelineHit Hit(TimelineHitPart part) =>
        new(new TimelineBlock(0, 0, new SequenceChainReferenceSave(), 1, 1, 1, false), part);

    [Fact]
    public void Hovering_ShowsResizeOverAnEdge_ArrowOverABodyOrNothing()
    {
        TimelineCursors.For(Hit(TimelineHitPart.Edge), null).ShouldBe(TimelineCursor.ResizeHorizontal);
        TimelineCursors.For(Hit(TimelineHitPart.Body), null).ShouldBe(TimelineCursor.Default);
        TimelineCursors.For(null, null).ShouldBe(TimelineCursor.Default);
    }

    [Fact]
    public void WhileGrippingAnEdge_ResizeStaysWhereverThePointerIs()
    {
        TimelineCursors.For(null, TimelineHitPart.Edge).ShouldBe(TimelineCursor.ResizeHorizontal);
        TimelineCursors.For(Hit(TimelineHitPart.Body), TimelineHitPart.Edge).ShouldBe(TimelineCursor.ResizeHorizontal);
    }

    [Fact]
    public void WhileGrippingABody_PassingOverAnEdgeShowsNoResize()
    {
        TimelineCursors.For(Hit(TimelineHitPart.Edge), TimelineHitPart.Body).ShouldBe(TimelineCursor.Default);
        TimelineCursors.For(null, TimelineHitPart.Body).ShouldBe(TimelineCursor.Default);
    }
}
