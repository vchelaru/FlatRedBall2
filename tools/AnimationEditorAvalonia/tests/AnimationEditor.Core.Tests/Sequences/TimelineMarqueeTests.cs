using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

public class TimelineMarqueeTests
{
    // Two tracks. Track 0: 0..0.5 s and 1..1.5 s. Track 1: 0.5..1 s.
    private static (TimelineViewport Viewport, IReadOnlyList<TimelineBlock> Blocks) Setup()
    {
        var sequence = new SequenceSave();
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { new() { Chain = "A", Start = 0 }, new() { Chain = "B", Start = 1 } } });
        sequence.Tracks.Add(new SequenceTrackSave { Chains = { new() { Chain = "C", Start = 0.5 } } });
        var viewport = new TimelineViewport();
        return (viewport, TimelineLayout.Blocks(sequence, _ => 0.5));
    }

    private static double Body(TimelineViewport viewport, int lane) =>
        viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;

    [Fact]
    public void Chains_SelectsEveryBlockTheRectangleTouches_AcrossTracks()
    {
        var (viewport, blocks) = Setup();

        var hit = TimelineMarquee.Chains(viewport, blocks, viewport.XAt(0.25), Body(viewport, 0), viewport.XAt(0.75), Body(viewport, 1));

        hit.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(1, 0) }, ignoreOrder: true);
    }

    [Fact]
    public void Chains_AnyCornerOrder_GivesTheSameResult()
    {
        var (viewport, blocks) = Setup();

        var hit = TimelineMarquee.Chains(viewport, blocks, viewport.XAt(1.25), Body(viewport, 0), viewport.XAt(0.25), Body(viewport, 0));

        hit.ShouldBe(new[] { new TimelineChainId(0, 0), new TimelineChainId(0, 1) }, ignoreOrder: true);
    }

    [Fact]
    public void Chains_ARectangleOverEmptyLaneSpace_SelectsNothing()
    {
        var (viewport, blocks) = Setup();

        TimelineMarquee.Chains(viewport, blocks, viewport.XAt(2), Body(viewport, 0), viewport.XAt(3), Body(viewport, 1)).ShouldBeEmpty();
    }

    [Fact]
    public void Chains_ARectangleInTheLaneGapAboveTheBodies_SelectsNothing()
    {
        var (viewport, blocks) = Setup();
        double top = viewport.LaneTop(0);

        TimelineMarquee.Chains(viewport, blocks, viewport.XAt(0), top, viewport.XAt(2), top + 1).ShouldBeEmpty();
    }
}
