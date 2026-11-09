using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>A chain pasted over the next one must keep its far end grabbable, wherever that end lies.</summary>
public class TimelineOverlapEdgeTests
{
    private static SequenceChainReferenceSave Ref(string chain, double start, float loops = 1) =>
        new() { Source = "hero.achx", Chain = chain, Start = start, Loops = loops };

    // Every cycle is 1 s. A starts at 0 and asks for 'aLoops' s; B is 2..3 s.
    private static (TimelineViewport Viewport, System.Collections.Generic.IReadOnlyList<TimelineBlock> Blocks) Setup(float aLoops, double bStart = 2)
    {
        var track = new SequenceTrackSave { Chains = { Ref("A", 0, aLoops), Ref("B", bStart) } };
        return (new TimelineViewport(), TimelineLayout.Blocks(new SequenceSave { Tracks = { track } }, _ => 1.0));
    }

    private static TimelineHit? Hit((TimelineViewport Viewport, System.Collections.Generic.IReadOnlyList<TimelineBlock> Blocks) s, double time, double dx = 0)
    {
        double y = s.Viewport.LaneTop(0) + TimelineLayout.BodyTop + 2;
        return TimelineLayout.HitTest(s.Viewport, s.Blocks, 1, s.Viewport.XAt(time) + dx, y);
    }

    [Fact]
    public void AEndsInsideB_TheEdgeInTheBodyBandIsA_AndTheRestOfBIsB()
    {
        var s = Setup(2.5f);
        Hit(s, 2.5).ShouldBe(new TimelineHit(s.Blocks[0], TimelineHitPart.Edge));
        Hit(s, 2.5, 4).ShouldBe(new TimelineHit(s.Blocks[0], TimelineHitPart.Edge));
        Hit(s, 2.5, -4).ShouldBe(new TimelineHit(s.Blocks[0], TimelineHitPart.Edge));
        Hit(s, 2.8)!.Value.Block.Reference.Chain.ShouldBe("B");
        Hit(s, 2.2)!.Value.Block.Reference.Chain.ShouldBe("B");
    }

    [Fact]
    public void AEndsBeyondB_TheEdgeIsGrabbableInTheBodyBand()
    {
        var s = Setup(4);
        Hit(s, 4).ShouldBe(new TimelineHit(s.Blocks[0], TimelineHitPart.Edge));
        Hit(s, 4, -5).ShouldBe(new TimelineHit(s.Blocks[0], TimelineHitPart.Edge));
    }

    [Fact]
    public void AEndsBeyondB_BothEdgesNearEachOther_NearestWins_TieGoesToTheLaterBlock()
    {
        // A ends at 3.01 s, B at 3 s; at 100 px/s that is 1 px apart.
        var s = Setup(3.01f);
        s.Viewport.SetPixelsPerSecond(100);
        Hit(s, 3.01, 0.5)!.Value.Block.Reference.Chain.ShouldBe("A");
        Hit(s, 3, -0.5)!.Value.Block.Reference.Chain.ShouldBe("B");
        Hit(s, 3.005)!.Value.Block.Reference.Chain.ShouldBe("B");
    }

    [Fact]
    public void AEndsExactlyAtBsStart_NothingChanges()
    {
        var s = Setup(2);
        Hit(s, 2, -1).ShouldBe(new TimelineHit(s.Blocks[0], TimelineHitPart.Edge));
        Hit(s, 2, 1).ShouldBe(new TimelineHit(s.Blocks[1], TimelineHitPart.Body));
    }

    [Fact]
    public void CutBlockThatIsSelectedStillLosesToNothing_ClickInsideBBodyFarFromTheEdge()
    {
        var s = Setup(4);
        Hit(s, 2.5)!.Value.Block.Reference.Chain.ShouldBe("B");
        Hit(s, 1)!.Value.Block.Reference.Chain.ShouldBe("A");
    }
}
