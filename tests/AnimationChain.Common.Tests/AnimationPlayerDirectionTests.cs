using System;
using System.Collections.Generic;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// PingPong and Reverse in <see cref="AnimationPlayer{TFrame}"/> (#1538): mirrors of the forward timeline. The chains are
/// the user's repro: A at x=0 with Bounce Out to B at x=80, 0.6s each.
/// </summary>
public class AnimationPlayerDirectionTests
{
    private class TestFrame : AnimationFrameBase { }

    private static AnimationChain<TestFrame> BounceChain(bool loop = true, bool pingPong = false, bool reverse = false, int frames = 2)
    {
        var chain = new AnimationChain<TestFrame> { Name = "c", Loop = loop, PingPong = pingPong, Reversed = reverse };
        for (int i = 0; i < frames; i++)
        {
            var frame = new TestFrame { FrameLength = TimeSpan.FromSeconds(0.6), RelativeX = i * 80 };
            if (i < frames - 1) frame.OffsetCurve = KeyframeCurve.Parse("Bounce", "Out");
            frame.Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = i.ToString() });
            chain.Add(frame);
        }
        return chain;
    }

    private static AnimationPlayer<TestFrame> Play(AnimationChain<TestFrame> chain)
    {
        var player = new AnimationPlayer<TestFrame>(new AnimationChainList<TestFrame> { chain });
        player.Play("c");
        return player;
    }

    private static float XAt(AnimationChain<TestFrame> chain, double seconds)
    {
        var player = Play(chain);
        player.Update(TimeSpan.FromSeconds(seconds));
        return player.CurrentOffset.X;
    }

    [Fact]
    public void Play_SeedsPingPongAndReverseFromTheChain()
    {
        var player = Play(BounceChain(pingPong: true, reverse: true));

        player.IsPingPong.ShouldBeTrue();
        player.IsReversed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.45)]
    public void PingPong_SecondHalf_IsTheForwardTimelinePlayedBackward(double d)
    {
        // The bounce at 1.8 + d is the forward bounce at 0.6 - d; B holds 0.6..1.8.
        var forward = BounceChain();
        var pingPong = BounceChain(pingPong: true);

        XAt(pingPong, 1.8 + d).ShouldBe(XAt(forward, 0.6 - d), 0.001f);
        XAt(pingPong, 0.6 + d).ShouldBe(80f);
        XAt(pingPong, 1.2 + d).ShouldBe(80f);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.7)]
    [InlineData(1.0)]
    public void Reverse_ValueAtT_IsTheForwardValueAtLengthMinusT(double t)
    {
        // 2_Bounce_Reverse: B holds for 0.6s, then A's bounce plays backward, a 1.2s loop.
        XAt(BounceChain(reverse: true), t).ShouldBe(XAt(BounceChain(), 1.2 - t), 0.001f);
    }

    [Fact]
    public void Reverse_LoopsEvery1Point2Seconds_AndStartsOnTheLastFrame()
    {
        var player = Play(BounceChain(reverse: true));
        player.CurrentFrameIndex.ShouldBe(1);

        player.Update(TimeSpan.FromSeconds(1.2 + 0.3));

        player.CurrentFrameIndex.ShouldBe(1);
        player.CurrentOffset.X.ShouldBe(80f);
    }

    [Fact]
    public void PingPongReverse_StartsWithTheMirroredHalf()
    {
        var chain = BounceChain(pingPong: true, reverse: true);

        XAt(chain, 0.3).ShouldBe(80f);
        XAt(chain, 0.9).ShouldBe(XAt(BounceChain(), 0.3), 0.001f);
        XAt(chain, 1.5).ShouldBe(XAt(BounceChain(), 0.3), 0.001f);
        XAt(chain, 2.1).ShouldBe(80f);
    }

    [Fact]
    public void PingPong_ThreeFrames_CycleIsTwiceTheLength_EndFramesShowTwice()
    {
        var player = Play(BounceChain(pingPong: true, frames: 3));
        var frames = new List<int>();
        player.Update(TimeSpan.FromSeconds(0.3));
        for (int i = 0; i < 7; i++)
        {
            frames.Add(player.CurrentFrameIndex);
            player.Update(TimeSpan.FromSeconds(0.6));
        }

        // 0.3, 0.9, ... 3.9 (3.6 wraps): A B C C B A A
        frames.ShouldBe(new List<int> { 0, 1, 2, 2, 1, 0, 0 });
    }

    [Fact]
    public void PingPong_NonLooping_PlaysOneFullCycleThenFinishes_EventsOnlyOnFrameChanges()
    {
        var visited = new List<string>();
        var player = Play(BounceChain(loop: false, pingPong: true, frames: 3));
        player.FrameEventRaised += e => visited.Add(e.Name);
        int finished = 0;
        player.AnimationFinished += () => finished++;

        player.Update(TimeSpan.FromSeconds(3.5));
        finished.ShouldBe(0);
        player.Update(TimeSpan.FromSeconds(1));

        finished.ShouldBe(1);
        player.CurrentFrameIndex.ShouldBe(0);
        player.TimeIntoAnimation.TotalSeconds.ShouldBe(3.6, 1e-9);
        visited.ShouldBe(new List<string> { "1", "2", "1", "0" });
    }

    [Fact]
    public void IsReversed_SetAfterPlay_OverridesTheSeededValue()
    {
        var player = Play(BounceChain());

        player.IsReversed = true;
        player.Update(TimeSpan.FromSeconds(0.3));

        player.CurrentFrameIndex.ShouldBe(1);
    }
}
