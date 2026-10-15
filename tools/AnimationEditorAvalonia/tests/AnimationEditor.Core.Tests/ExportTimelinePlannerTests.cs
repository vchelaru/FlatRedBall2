using AnimationEditor.Core.Export;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ExportTimelinePlannerTests
{
    private static AnimationChainSave Chain(string name, bool loop, params (float length, float x)[] frames)
    {
        var chain = new AnimationChainSave { Name = name, Loop = loop };
        foreach (var (length, x) in frames)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = length, RelativeX = x });
        return chain;
    }

    // Stored on the first frame so its offset glides to the next frame's.
    private static AnimationChainSave Linear(AnimationChainSave chain)
    {
        chain.Frames[0].OffsetInterpolation = "Linear";
        return chain;
    }

    // GIF-style ticks: centiseconds, 2cs minimum.
    private static IReadOnlyList<ExportTimelineFrame> PlanGif(int frameRate, params ExportSourceChain[] sources) =>
        ExportTimelinePlanner.Plan(sources, frameRate, ticksPerSecond: 100, minTicks: 2);

    [Fact]
    public void Plan_SnappedChain_MergesIdenticalSteps_IntoOneLongFramePerAnimationFrame()
    {
        var walk = Chain("Walk", loop: true, (0.1f, 0f), (0.1f, 4f));

        var timeline = PlanGif(50, new ExportSourceChain(walk));

        timeline.Select(f => f.DelayTicks).ShouldBe(new[] { 10, 10 });
        timeline.Select(f => f.Layers[0].OffsetX).ShouldBe(new[] { 0f, 4f });
    }

    [Fact]
    public void Plan_StoredLinearCurve_GlidesTheOffsetAcrossEachFrame()
    {
        // 0.1s at 50fps is 5 steps from x=0 toward x=10: 0, 2, 4, 6, 8.
        var slide = Linear(Chain("Slide", loop: false, (0.1f, 0f), (0.1f, 10f)));

        var timeline = PlanGif(50, new ExportSourceChain(slide));

        timeline.Take(5).Select(f => MathF.Round(f.Layers[0].OffsetX, 3)).ShouldBe(new[] { 0f, 2f, 4f, 6f, 8f });
        timeline[5].Layers[0].OffsetX.ShouldBe(10f, tolerance: 1e-3f);
    }

    [Fact]
    public void Plan_ShorterLoopingChain_CyclesUntilTheLongestChainEnds()
    {
        var longClip = Chain("Long", loop: false, (0.2f, 0f), (0.2f, 0f));
        var blink = Chain("Blink", loop: true, (0.1f, 1f), (0.1f, 2f));

        var timeline = PlanGif(50, new ExportSourceChain(longClip), new ExportSourceChain(blink));

        timeline.Select(f => f.Layers[1].OffsetX).ShouldBe(new[] { 1f, 2f, 1f, 2f });
        timeline.Sum(f => f.DelayTicks).ShouldBe(40);
    }

    [Fact]
    public void Plan_ShorterNonLoopingChain_HoldsItsLastFrame()
    {
        var longClip = Chain("Long", loop: false, (0.2f, 0f), (0.2f, 0f));
        var once = Chain("Once", loop: false, (0.1f, 1f), (0.1f, 2f));

        var timeline = PlanGif(50, new ExportSourceChain(longClip), new ExportSourceChain(once));

        // Three timeline frames: the long clip changes frame at 0.2s while "Once" sits on its last frame.
        timeline.Select(f => f.Layers[1].OffsetX).ShouldBe(new[] { 1f, 2f, 2f });
        timeline.Sum(f => f.DelayTicks).ShouldBe(40);
    }

    [Fact]
    public void Plan_KeepsLayersInSelectionOrder_BackToFront()
    {
        var back = Chain("Back", loop: true, (0.1f, 0f));
        var front = Chain("Front", loop: true, (0.1f, 0f));

        var timeline = PlanGif(50, new ExportSourceChain(back), new ExportSourceChain(front));

        timeline[0].Layers.Select(l => l.Chain.Name).ShouldBe(new[] { "Back", "Front" });
    }

    [Fact]
    public void Plan_ChainsWithoutFrames_ProduceNoTimeline()
    {
        var empty = new AnimationChainSave { Name = "Empty" };

        PlanGif(50, new ExportSourceChain(empty)).ShouldBeEmpty();
    }

    [Fact]
    public void Plan_MillisecondTicksAt60Fps_SamplesEverySixtiethOfASecond()
    {
        // 0.1s eased from x=0 to x=6 at 60fps is 6 steps of 1 each; delays total exactly 200ms over both frames.
        var slide = Linear(Chain("Slide", loop: false, (0.1f, 0f), (0.1f, 6f)));

        var timeline = ExportTimelinePlanner.Plan(
            new[] { new ExportSourceChain(slide) }, frameRate: 60, ticksPerSecond: 1000, minTicks: 10);

        timeline.Take(6).Select(f => MathF.Round(f.Layers[0].OffsetX, 3)).ShouldBe(new[] { 0f, 1f, 2f, 3f, 4f, 5f });
        timeline.Sum(f => f.DelayTicks).ShouldBe(200);
        timeline.ShouldAllBe(f => f.DelayTicks >= 10);
    }

    [Fact]
    public void Plan_StoredColorCurve_GlidesTheLayerColor_AndKeepsStepsApart()
    {
        var fade = Chain("Fade", loop: false, (0.1f, 0f), (0.1f, 0f));
        fade.Frames[0].Alpha = 0;
        fade.Frames[1].Alpha = 200;
        fade.Frames[0].ColorInterpolation = "Linear";

        var timeline = PlanGif(50, new ExportSourceChain(fade));

        timeline.Take(5).Select(f => f.Layers[0].Color.Alpha).ShouldBe(new int?[] { 0, 40, 80, 120, 160 });
        timeline.Select(f => f.Layers[0].Frame).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public void Plan_SnapColor_StillMergesIdenticalSteps()
    {
        var steady = Chain("Steady", loop: true, (0.1f, 0f), (0.1f, 0f));
        steady.Frames[0].Alpha = 10;
        steady.Frames[1].Alpha = 20;

        var timeline = PlanGif(50, new ExportSourceChain(steady));

        timeline.Select(f => f.DelayTicks).ShouldBe(new[] { 10, 10 });
        timeline.Select(f => f.Layers[0].Color.Alpha).ShouldBe(new int?[] { 10, 20 });
    }
}
