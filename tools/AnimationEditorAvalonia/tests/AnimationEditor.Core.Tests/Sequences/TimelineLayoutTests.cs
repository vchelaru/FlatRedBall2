using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineLayoutTests
{
    private static SequenceChainReferenceSave Ref(string chain, double start, float loops = 1) =>
        new() { Source = "hero.achx", Chain = chain, Start = start, Loops = loops };

    // Walk is 1 s per cycle, Gone is missing.
    private static double? Cycle(SequenceChainReferenceSave reference) => reference.Chain == "Gone" ? null : 1.0;

    private static double LaneY(TimelineViewport viewport, int lane, double offset) => viewport.LaneTop(lane) + offset;

    [Fact]
    public void Blocks_CutChainKeepsItsFullLength_AndAMissingChainIsAOneSecondPlaceholder()
    {
        var track = new SequenceTrackSave { Chains = { Ref("Walk", 0, loops: 3), Ref("Gone", 2) } };
        var sequence = new SequenceSave { Tracks = { track } };

        var blocks = TimelineLayout.Blocks(sequence, Cycle);

        var walk = blocks.Single(b => b.Reference.Chain == "Walk");
        walk.Played.ShouldBe(2, 1e-9);
        walk.Full.ShouldBe(3, 1e-9);
        walk.IsCut.ShouldBeTrue();
        var gone = blocks.Single(b => b.Reference.Chain == "Gone");
        gone.IsMissing.ShouldBeTrue();
        gone.Full.ShouldBe(TimelineLayout.MissingLength);
        gone.CanResize.ShouldBeFalse();
    }

    [Fact]
    public void HitTest_BodyEdgeAndCutTail()
    {
        // Walk 0..3 s cut at 2 s by Run (2..3 s).
        var track = new SequenceTrackSave { Chains = { Ref("Walk", 0, loops: 3), Ref("Run", 2) } };
        var sequence = new SequenceSave { Tracks = { track } };
        var blocks = TimelineLayout.Blocks(sequence, Cycle);
        var viewport = new TimelineViewport();
        double bodyY = LaneY(viewport, 0, TimelineLayout.BodyTop + 2);
        double tailY = LaneY(viewport, 0, TimelineLayout.TailTop + 2);

        TimelineHit? Hit(double time, double y) => TimelineLayout.HitTest(viewport, blocks, 1, viewport.XAt(time), y);

        Hit(1, bodyY).ShouldBe(new TimelineHit(blocks[0], TimelineHitPart.Body));
        // Under Run's body, the press is Run's, even where Walk's cut tail runs below.
        Hit(2.5, bodyY)!.Value.Block.Reference.Chain.ShouldBe("Run");
        Hit(3, bodyY).ShouldBe(new TimelineHit(blocks[1], TimelineHitPart.Edge));
        // In the tail band: Walk's cut tail, whose far end is Walk's resize edge.
        Hit(2.5, tailY).ShouldBe(new TimelineHit(blocks[0], TimelineHitPart.Body));
        Hit(3, tailY).ShouldBe(new TimelineHit(blocks[0], TimelineHitPart.Edge));
        Hit(5, bodyY).ShouldBeNull();
    }

    [Fact]
    public void LaneHeights_OnlyTracksWithEvents_GrowByTheEventRow()
    {
        var sequence = new SequenceSave
        {
            Tracks =
            {
                new SequenceTrackSave { Chains = { Ref("Walk", 0) } },
                new SequenceTrackSave { Chains = { Ref("Run", 0), Ref("Hit", 1) } },
                new SequenceTrackSave(),
            },
        };
        var blocks = TimelineLayout.Blocks(sequence, Cycle);

        var heights = TimelineLayout.LaneHeights(blocks, 3, block => block.Reference.Chain == "Hit");

        heights.ShouldBe(new[] { TimelineViewport.LaneHeight, TimelineViewport.LaneHeight + TimelineLayout.EventRowHeight, TimelineViewport.LaneHeight });
    }

    [Fact]
    public void HitTest_InATallLanesEventRow_HitsTheBlockBody_AndNeverACutTail()
    {
        // Walk 0..3 s cut at 2 s by Run: the event row under Walk's cut tail is empty space, under Run it is Run's.
        var track = new SequenceTrackSave { Chains = { Ref("Walk", 0, loops: 3), Ref("Run", 2) } };
        var sequence = new SequenceSave { Tracks = { track } };
        var blocks = TimelineLayout.Blocks(sequence, Cycle);
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { TimelineViewport.LaneHeight + TimelineLayout.EventRowHeight });
        double eventY = LaneY(viewport, 0, TimelineLayout.EventRowTop + 6);

        TimelineHit? Hit(double time) => TimelineLayout.HitTest(viewport, blocks, 1, viewport.XAt(time), eventY);

        Hit(1).ShouldBe(new TimelineHit(blocks[0], TimelineHitPart.Body));
        Hit(2.5)!.Value.Block.Reference.Chain.ShouldBe("Run");
        // Past Run's end (3 s) only Walk's cut tail would reach, and the tail strip is above the event row.
        Hit(3.2).ShouldBeNull();
    }

    [Fact]
    public void HitTest_AfterATallLane_FindsTheNextTracksBlocksAtTheirShiftedPosition()
    {
        var sequence = new SequenceSave
        {
            Tracks =
            {
                new SequenceTrackSave { Chains = { Ref("Walk", 0) } },
                new SequenceTrackSave { Chains = { Ref("Run", 0) } },
            },
        };
        var blocks = TimelineLayout.Blocks(sequence, Cycle);
        var viewport = new TimelineViewport();
        viewport.SetLaneHeights(new[] { TimelineViewport.LaneHeight + TimelineLayout.EventRowHeight, TimelineViewport.LaneHeight });
        double secondLaneY = viewport.LaneTop(1) + TimelineLayout.BodyTop + 2;

        var hit = TimelineLayout.HitTest(viewport, blocks, 2, viewport.XAt(0.5), secondLaneY);

        hit!.Value.Block.Reference.Chain.ShouldBe("Run");
        // The same y with every lane 48 px would land on the first lane's event row, not on Run's body.
        viewport.LaneTop(1).ShouldBe(TimelineViewport.RulerHeight + 60);
    }

    [Fact]
    public void Blocks_EndingAFloatRoundingPastTheNextStart_IsNotCut()
    {
        // 2.5 float loops of a float-summed 0.1 s × 5 cycle land a hair past 1.25, where the edge magnet put it.
        double cycle = 5 * (double)0.1f;
        var track = new SequenceTrackSave { Chains = { Ref("Walk", 0, loops: 2.5f), Ref("Run", 1.25) } };

        var walk = TimelineLayout.Blocks(new SequenceSave { Tracks = { track } }, _ => cycle)[0];

        walk.IsCut.ShouldBeFalse();
    }

    [Fact]
    public void SnapStart_RoundsToTheStep_AndNeverGoesBeforeZero()
    {
        TimelineLayout.SnapStart(1.234).ShouldBe(1.25, 1e-9);
        TimelineLayout.SnapStart(-0.4).ShouldBe(0);
    }

    [Fact]
    public void EdgeDragLoops_MagnetIsAFixedPixelDistanceAtAnyZoom()
    {
        var reference = Ref("Walk", 0);
        var block = TimelineLayout.Blocks(new SequenceSave { Tracks = { new SequenceTrackSave { Chains = { reference } } } }, Cycle)[0];
        double magnet = TimelineLayout.MagnetPixels;

        // At 100 px/s the magnet reaches 0.08 s; at 1000 px/s only 0.008 s.
        TimelineLayout.EdgeDragLoops(block, 2 + (magnet - 1) / 100, pixelsPerSecond: 100, nextStart: null).ShouldBe(2f);
        TimelineLayout.EdgeDragLoops(block, 2 + (magnet + 1) / 1000, pixelsPerSecond: 1000, nextStart: null)
            .ShouldBe((float)(2 + (magnet + 1) / 1000), 1e-5f);
    }

    [Fact]
    public void ShowsLabel_BlankBelowThirtySixPixels()
    {
        TimelineLayout.ShowsLabel(35.9).ShouldBeFalse();
        TimelineLayout.ShowsLabel(36).ShouldBeTrue();
    }

    [Fact]
    public void IsOverEye_IsTrueOnTheEyeInTheTopBandAndFalseOnTheNameOrLowerInTheLane()
    {
        TimelineLayout.IsOverEye(TimelineLayout.EyeCenterX, TimelineLayout.EyeCenterY).ShouldBeTrue();
        TimelineLayout.IsOverEye(TimelineLayout.EyeCenterX, TimelineLayout.EyeCenterY + 30).ShouldBeFalse();
        TimelineLayout.IsOverEye(TimelineLayout.HeaderNameLeft + 20, TimelineLayout.EyeCenterY).ShouldBeFalse();
        // Where the scenarios (and users) click a header to select it: beside the eye, not on it.
        TimelineLayout.IsOverEye(20, 10).ShouldBeFalse();
    }
}
