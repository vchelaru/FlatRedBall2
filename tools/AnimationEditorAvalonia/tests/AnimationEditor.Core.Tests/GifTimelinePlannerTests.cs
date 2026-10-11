using AnimationEditor.Core.Export.Gif;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class GifTimelinePlannerTests
{
    private static AnimationChainSave Chain(string name, bool loop, params (float length, float x)[] frames)
    {
        var chain = new AnimationChainSave { Name = name, Loop = loop };
        foreach (var (length, x) in frames)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = length, RelativeX = x });
        return chain;
    }

    [Fact]
    public void Plan_SnappedChain_MergesIdenticalSteps_IntoOneLongFramePerAnimationFrame()
    {
        var walk = Chain("Walk", loop: true, (0.1f, 0f), (0.1f, 4f));

        var timeline = GifTimelinePlanner.Plan(new[] { new GifSourceChain(walk, Interpolate: false) }, frameRate: 50);

        timeline.Select(f => f.DelayCentiseconds).ShouldBe(new[] { 10, 10 });
        timeline.Select(f => f.Layers[0].OffsetX).ShouldBe(new[] { 0f, 4f });
    }

    [Fact]
    public void Plan_InterpolatedChain_EasesTheOffsetAcrossEachFrame()
    {
        // 0.1s at 50fps is 5 steps from x=0 toward x=10: 0, 2, 4, 6, 8.
        var slide = Chain("Slide", loop: false, (0.1f, 0f), (0.1f, 10f));

        var timeline = GifTimelinePlanner.Plan(new[] { new GifSourceChain(slide, Interpolate: true) }, frameRate: 50);

        timeline.Take(5).Select(f => MathF.Round(f.Layers[0].OffsetX, 3)).ShouldBe(new[] { 0f, 2f, 4f, 6f, 8f });
        timeline[5].Layers[0].OffsetX.ShouldBe(10f, tolerance: 1e-3f);
    }

    [Fact]
    public void Plan_ShorterLoopingChain_CyclesUntilTheLongestChainEnds()
    {
        var longClip = Chain("Long", loop: false, (0.2f, 0f), (0.2f, 0f));
        var blink = Chain("Blink", loop: true, (0.1f, 1f), (0.1f, 2f));

        var timeline = GifTimelinePlanner.Plan(
            new[] { new GifSourceChain(longClip, false), new GifSourceChain(blink, false) }, frameRate: 50);

        timeline.Select(f => f.Layers[1].OffsetX).ShouldBe(new[] { 1f, 2f, 1f, 2f });
        timeline.Sum(f => f.DelayCentiseconds).ShouldBe(40);
    }

    [Fact]
    public void Plan_ShorterNonLoopingChain_HoldsItsLastFrame()
    {
        var longClip = Chain("Long", loop: false, (0.2f, 0f), (0.2f, 0f));
        var once = Chain("Once", loop: false, (0.1f, 1f), (0.1f, 2f));

        var timeline = GifTimelinePlanner.Plan(
            new[] { new GifSourceChain(longClip, false), new GifSourceChain(once, false) }, frameRate: 50);

        // Three timeline frames: the long clip changes frame at 0.2s while "Once" sits on its last frame.
        timeline.Select(f => f.Layers[1].OffsetX).ShouldBe(new[] { 1f, 2f, 2f });
        timeline.Sum(f => f.DelayCentiseconds).ShouldBe(40);
    }

    [Fact]
    public void Plan_KeepsLayersInSelectionOrder_BackToFront()
    {
        var back = Chain("Back", loop: true, (0.1f, 0f));
        var front = Chain("Front", loop: true, (0.1f, 0f));

        var timeline = GifTimelinePlanner.Plan(
            new[] { new GifSourceChain(back, false), new GifSourceChain(front, false) }, frameRate: 50);

        timeline[0].Layers.Select(l => l.Chain.Name).ShouldBe(new[] { "Back", "Front" });
    }

    [Fact]
    public void Plan_ChainsWithoutFrames_ProduceNoTimeline()
    {
        var empty = new AnimationChainSave { Name = "Empty" };

        GifTimelinePlanner.Plan(new[] { new GifSourceChain(empty, false) }, frameRate: 50).ShouldBeEmpty();
    }
}
