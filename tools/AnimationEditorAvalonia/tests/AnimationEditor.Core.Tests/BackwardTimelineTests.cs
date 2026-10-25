using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>The timeline's playhead and lanes while a frame plays backward (#1538).</summary>
public class BackwardTimelineTests
{
    [Fact]
    public void PlayheadOffset_Forward_MovesRightFromTheCellsLeftEdge()
    {
        Assert.Equal(10, TimelineBuilder.PlayheadOffset(elapsed: 0.1, pixelsPerSecond: 100, travelWidth: 40, backward: false), 3);
    }

    [Fact]
    public void PlayheadOffset_Backward_MovesLeftFromTheCellsRightEdge()
    {
        Assert.Equal(30, TimelineBuilder.PlayheadOffset(elapsed: 0.1, pixelsPerSecond: 100, travelWidth: 40, backward: true), 3);
        Assert.Equal(0, TimelineBuilder.PlayheadOffset(elapsed: 1, pixelsPerSecond: 100, travelWidth: 40, backward: true), 3);
    }

    [Fact]
    public void TimelineLanePlanner_Backward_BuildsBackwardLanes()
    {
        var chain = new AnimationChainSave { Name = "c", Loop = false };
        for (int i = 0; i < 2; i++)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f, RelativeX = i * 10, OffsetInterpolation = "Linear" });

        var plan = TimelineLanePlanner.Build(chain, System.Array.Empty<object>(), new double[] { 10, 10 }, backward: true);

        Assert.All(plan.First!.Spans, s => Assert.True(s.Backward));
    }
}
