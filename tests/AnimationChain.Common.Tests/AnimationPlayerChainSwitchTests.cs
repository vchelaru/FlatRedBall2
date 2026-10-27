using System;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>
/// Chain switching matches FRB1's hardened behavior (#1554): a different chain restarts from time 0 with the frame
/// applied at once, the same chain is a no-op, and seeking by frame index re-derives time from the index.
/// </summary>
public class AnimationPlayerChainSwitchTests
{
    private class TestFrame : AnimationFrameBase { }

    private static AnimationChain<TestFrame> Chain(string name, bool reverse = false, bool pingPong = false, int frames = 3)
    {
        var chain = new AnimationChain<TestFrame> { Name = name, Loop = true, Reversed = reverse, PingPong = pingPong };
        for (int i = 0; i < frames; i++)
            chain.Add(new TestFrame { FrameLength = TimeSpan.FromSeconds(0.5) });
        return chain;
    }

    private static AnimationPlayer<TestFrame> Player(params AnimationChain<TestFrame>[] chains)
    {
        var list = new AnimationChainList<TestFrame>();
        foreach (var c in chains) list.Add(c);
        return new AnimationPlayer<TestFrame>(list);
    }

    [Fact]
    public void Play_DifferentChain_RestartsTimeAndFrameImmediately()
    {
        var player = Player(Chain("a"), Chain("b"));
        player.Play("a");
        player.Update(TimeSpan.FromSeconds(0.7));
        player.CurrentFrameIndex.ShouldBe(1);

        player.Play("b");

        player.CurrentChainName.ShouldBe("b");
        player.CurrentFrameIndex.ShouldBe(0);
        player.TimeIntoAnimation.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Play_SameChain_IsANoOp()
    {
        var player = Player(Chain("a"));
        player.Play("a");
        player.Update(TimeSpan.FromSeconds(0.7));

        player.Play("a");

        player.CurrentFrameIndex.ShouldBe(1);
        player.TimeIntoAnimation.TotalSeconds.ShouldBe(0.7, 0.0001);
    }

    [Fact]
    public void Play_ReverseChain_ShowsLastFrameFromTimeZero()
    {
        var player = Player(Chain("a"), Chain("r", reverse: true));
        player.Play("a");
        player.Update(TimeSpan.FromSeconds(0.7));

        player.Play("r");

        player.CurrentFrameIndex.ShouldBe(2);
        player.TimeIntoAnimation.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Play_PingPongReverseChain_ShowsLastFrameFromTimeZero()
    {
        var player = Player(Chain("a"), Chain("pr", reverse: true, pingPong: true));
        player.Play("a");
        player.Update(TimeSpan.FromSeconds(0.7));

        player.Play("pr");

        player.CurrentFrameIndex.ShouldBe(2);
        player.TimeIntoAnimation.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CurrentFrameIndex_Set_KeepsThatFrameOnItsExactBoundary(bool reverse, bool pingPong)
    {
        var player = Player(Chain("a", reverse, pingPong));
        player.Play("a");

        for (int i = 0; i < 3; i++)
        {
            player.CurrentFrameIndex = i;
            player.CurrentFrameIndex.ShouldBe(i);
            player.Update(TimeSpan.Zero);
            player.CurrentFrameIndex.ShouldBe(i);
        }
    }

    [Fact]
    public void CurrentFrameIndex_Set_RederivesTimeFromTheFrameStart()
    {
        var player = Player(Chain("a"));
        player.Play("a");

        player.CurrentFrameIndex = 2;

        player.TimeIntoAnimation.TotalSeconds.ShouldBe(1.0, 0.0001);
    }
}
