using System;
using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A game that steps by a frame's length enters a frame, loops and finishes on the update that reaches the boundary.</summary>
public class FrameBoundaryTests
{
    private sealed class Frame : AnimationFrameBase { }

    private static AnimationPlayer<Frame> Playing(double frameSeconds, bool loop, bool pingPong, bool reverse)
    {
        var chain = new AnimationChain<Frame> { Name = "a", Loop = loop, PingPong = pingPong, Reversed = reverse };
        for (int i = 0; i < 5; i++) chain.Add(new Frame { FrameLength = TimeSpan.FromSeconds(frameSeconds) });
        var player = new AnimationPlayer<Frame>(new AnimationChainList<Frame> { chain });
        player.Play("a");
        return player;
    }

    [Theory]
    [InlineData(0.1, false, false)]
    [InlineData(0.1, true, false)]
    [InlineData(0.1, false, true)]
    [InlineData(0.1, true, true)]
    [InlineData(0.05, false, false)]
    [InlineData(0.07, false, true)]
    [InlineData(0.033, false, false)]
    public void Update_StepOfOneFrame_EntersAFrameEachTime(double frameSeconds, bool reverse, bool pingPong)
    {
        var player = Playing(frameSeconds, loop: true, pingPong, reverse);
        int[] cycle = reverse ? [4, 3, 2, 1, 0] : [0, 1, 2, 3, 4];
        if (pingPong) cycle = [.. cycle, .. cycle.Reverse()];

        for (int step = 1; step <= 200; step++)
        {
            player.Update(TimeSpan.FromSeconds(frameSeconds));
            player.CurrentFrameIndex.ShouldBe(cycle[step % cycle.Length], $"step {step}");
        }
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.07)]
    public void AnimationFinished_StepOfOneFrame_IsRaisedOnTheStepThatReachesTheEnd(double frameSeconds)
    {
        var player = Playing(frameSeconds, loop: false, pingPong: true, reverse: false);
        int finished = 0;
        player.AnimationFinished += () => finished++;

        for (int step = 1; step <= 10; step++)
        {
            finished.ShouldBe(0);
            player.Update(TimeSpan.FromSeconds(frameSeconds));
        }

        finished.ShouldBe(1);
    }

    [Theory]
    [InlineData(10.1f, TimeMeasurementUnit.Second, 101_000_000)]
    [InlineData(0.1f, TimeMeasurementUnit.Undefined, 1_000_000)]
    [InlineData(16.667f, TimeMeasurementUnit.Millisecond, 166_670)]
    [InlineData(1 / 60f, TimeMeasurementUnit.Second, 166_667)]
    public void GetDuration_IsTheDecimalTheFileHolds(float length, TimeMeasurementUnit unit, long ticks) =>
        new AnimationFrameSave { FrameLength = length }.GetDuration(unit).Ticks.ShouldBe(ticks);
}
