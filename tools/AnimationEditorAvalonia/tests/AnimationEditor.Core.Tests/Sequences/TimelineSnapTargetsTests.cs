using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineSnapTargetsTests
{
    private static SequenceChainReferenceSave Ref(string chain, double start, float loops = 1) =>
        new() { Source = "hero.achx", Chain = chain, Start = start, Loops = loops };

    // Every chain is 0.5 s per cycle.
    private static double? Cycle(SequenceChainReferenceSave reference) => 0.5;

    private static System.Collections.Generic.IReadOnlyList<TimelineBlock> Blocks(params SequenceTrackSave[] tracks)
    {
        var sequence = new SequenceSave();
        sequence.Tracks.AddRange(tracks);
        return TimelineLayout.Blocks(sequence, Cycle);
    }

    private static SequenceTrackSave Track(params SequenceChainReferenceSave[] chains) => new() { Chains = chains.ToList() };

    private static AnimationChainSave ChainOf(bool pingPong, params float[] lengths)
    {
        var chain = new AnimationChainSave { PingPong = pingPong };
        foreach (float length in lengths) chain.Frames.Add(new AnimationFrameSave { FrameLength = length });
        return chain;
    }

    [Fact]
    public void Others_OffersEveryOtherChainsEdgesOnEveryTrack_ThePlayhead_AndZero_NeverTheDraggedChain()
    {
        var dragged = Ref("Walk", 0);
        var cut = Ref("Run", 1, loops: 4);       // 1..3 asked, cut at 2 by Hurt
        var hurt = Ref("Hurt", 2);               // 2..2.5
        var other = Ref("Jump", 5);              // on the other track: 5..5.5
        var blocks = Blocks(Track(dragged, cut, hurt), Track(other));

        var targets = TimelineSnapTargets.Others(blocks, dragged, playhead: 0.77);

        var edges = targets.Where(t => t.Kind == SnapKind.ChainEdge).Select(t => t.Time).OrderBy(t => t).ToList();
        edges.ShouldBe(new[] { 1.0, 2.0, 2.0, 2.5, 3.0, 5.0, 5.5 });
        targets.Single(t => t.Kind == SnapKind.Playhead).Time.ShouldBe(0.77);
        targets.Single(t => t.Kind == SnapKind.Zero).Time.ShouldBe(0);
        // Walk's own start (0) and end (0.5) are not offered.
        targets.Where(t => t.Kind == SnapKind.ChainEdge).ShouldNotContain(t => t.Time == 0.5);
        targets.Where(t => t.Kind == SnapKind.ChainEdge && t.Track == 1).Select(t => t.Chain).ShouldBe(new[] { 0, 0 });
    }

    [Fact]
    public void Others_AChainCutByTheDraggedOne_OffersNoPlayedEnd_OnlyItsFullEnd()
    {
        var early = Ref("Run", 0, loops: 4);     // asks for 0..2, cut at 1 by the dragged Walk
        var dragged = Ref("Walk", 1);
        var blocks = Blocks(Track(early, dragged));

        var targets = TimelineSnapTargets.Others(blocks, dragged, playhead: 0);

        targets.Where(t => t.Kind == SnapKind.ChainEdge).Select(t => t.Time).OrderBy(t => t).ShouldBe(new[] { 0.0, 2.0 });
    }

    [Theory]
    [InlineData(120)]
    [InlineData(2000)]
    public void ResizeEdge_NearAnotherChainsPlayedEnd_LandsExactlyOnItAtAnyZoom(double pixelsPerSecond)
    {
        var walk = Ref("Walk", 0);
        var run = Ref("Run", 0.55);
        var blocks = Blocks(Track(walk), Track(run));
        var others = TimelineSnapTargets.Others(blocks, walk, playhead: 9);
        // Run plays 0.55..1.05; drag Walk's edge to 3 px past that.
        double raw = 1.05 + 3 / pixelsPerSecond;

        var result = TimelineSnapTargets.ResizeEdge(blocks[0], raw, others, null, pixelsPerSecond, bypass: false);

        result.Snap.Time.ShouldBe(1.05, 1e-9);
        result.Snap.Target!.Value.Kind.ShouldBe(SnapKind.ChainEdge);
        // Loops are a float, so the edge it gives is exact to float precision, not to 1e-9.
        (0 + result.Loops * 0.5).ShouldBe(1.05, 1e-6);
    }

    [Fact]
    public void ResizeEdge_AtVeryHighZoom_FarFromEveryTarget_IsFree()
    {
        var walk = Ref("Walk", 0);
        var run = Ref("Run", 0.55);
        var blocks = Blocks(Track(walk), Track(run));
        var others = TimelineSnapTargets.Others(blocks, walk, playhead: 9);

        // 1.05 + 0.01 s is 20 px away at 2000 px/s: no target in reach, no grid.
        var result = TimelineSnapTargets.ResizeEdge(blocks[0], 1.06, others, null, 2000, bypass: false);

        result.Snap.Target.ShouldBeNull();
        (result.Loops * 0.5).ShouldBe(1.06, 1e-6);
    }

    [Fact]
    public void ResizeEdge_SnapsToTheOwnWholeCycles_AndTheFrameBoundariesWhenZoomedIn()
    {
        var walk = Ref("Walk", 0);
        var blocks = Blocks(Track(walk));
        var frames = new[] { 0.1, 0.2, 0.3, 0.4 };

        var cycle = TimelineSnapTargets.ResizeEdge(blocks[0], 1.5 + 4 / 120.0, TimelineSnapTargets.Others(blocks, walk, 9), null, 120, false);
        var frame = TimelineSnapTargets.ResizeEdge(blocks[0], 0.7 + 3 / 1500.0, TimelineSnapTargets.Others(blocks, walk, 9), frames, 1500, false);

        cycle.Snap.Target!.Value.Kind.ShouldBe(SnapKind.OwnCycle);
        cycle.Loops.ShouldBe(3f, 1e-6f);
        frame.Snap.Target!.Value.Kind.ShouldBe(SnapKind.FrameBoundary);
        frame.Snap.Time.ShouldBe(0.7, 1e-9);
    }

    [Fact]
    public void ResizeEdge_Bypass_UsesTheRawEdge()
    {
        var walk = Ref("Walk", 0);
        var blocks = Blocks(Track(walk));

        var result = TimelineSnapTargets.ResizeEdge(blocks[0], 1.0 + 2 / 120.0, TimelineSnapTargets.Others(blocks, walk, 9), null, 120, bypass: true);

        result.Snap.Target.ShouldBeNull();
        result.Loops.ShouldBe((float)((1.0 + 2 / 120.0) / 0.5), 1e-5f);
    }

    [Fact]
    public void ResizeEdge_DraggedBeforeTheStart_KeepsTheMinimumLoops_EvenNextToATargetThere()
    {
        var walk = Ref("Walk", 2);
        var other = Ref("Run", 0.5);
        var blocks = Blocks(Track(walk), Track(other));

        // Zero and Run's start are far left of Walk; the edge is dragged to 1.0, left of the start.
        var result = TimelineSnapTargets.ResizeEdge(blocks[0], 1.0, TimelineSnapTargets.Others(blocks, walk, 9), null, 120, false);
        var nearTarget = TimelineSnapTargets.ResizeEdge(blocks[0], 0.5, TimelineSnapTargets.Others(blocks, walk, 9), null, 120, false);

        result.Loops.ShouldBe(SequenceTimeline.MinimumLoops);
        nearTarget.Loops.ShouldBe(SequenceTimeline.MinimumLoops);
        nearTarget.Snap.Target.ShouldBeNull();
    }

    [Fact]
    public void ResizeEdge_PastTheNextChain_StillAllowed()
    {
        var walk = Ref("Walk", 0);
        var run = Ref("Run", 1.25);
        var blocks = Blocks(Track(walk, run));

        var result = TimelineSnapTargets.ResizeEdge(blocks[0], 1.9, TimelineSnapTargets.Others(blocks, walk, 9), null, 120, false);

        result.Loops.ShouldBe(3.8f, 1e-5f);
    }

    [Fact]
    public void FrameBoundaries_ListTheCuts_AndPingPongSkipsTheTurnaroundWhereAFrameShowsTwice()
    {
        // 0.1, 0.2, 0.3: cuts at 0.1 and 0.3.
        TimelineSnapTargets.FrameBoundaries(ChainOf(false, 0.1f, 0.2f, 0.3f)).Select(x => System.Math.Round(x, 3)).ShouldBe(new[] { 0.1, 0.3 });
        // Ping-pong plays 0.1 0.2 0.3 | 0.3 0.2 0.1: no cut at 0.6 where the 0.3 frame is shown twice.
        TimelineSnapTargets.FrameBoundaries(ChainOf(true, 0.1f, 0.2f, 0.3f)).Select(x => System.Math.Round(x, 3)).ShouldBe(new[] { 0.1, 0.3, 0.9, 1.1 });
    }
}
