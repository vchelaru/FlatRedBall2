using System;
using System.Collections.Generic;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>PingPong and Reverse playback in <see cref="AnimationPlayer{TFrame}"/> (#1538).</summary>
public class AnimationPlayerDirectionTests
{
    private class TestFrame : AnimationFrameBase { }

    // Three 1s frames named by index through an event, so the visited order is observable.
    private static AnimationPlayer<TestFrame> MakePlayer(bool loop, bool pingPong, bool reverse, List<string>? visited = null)
    {
        var chain = new AnimationChain<TestFrame> { Name = "c", Loop = loop, PingPong = pingPong, Reversed = reverse };
        for (int i = 0; i < 3; i++)
        {
            var frame = new TestFrame { FrameLength = TimeSpan.FromSeconds(1) };
            frame.Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = i.ToString() });
            chain.Add(frame);
        }
        var list = new AnimationChainList<TestFrame> { chain };
        var player = new AnimationPlayer<TestFrame>(list);
        if (visited != null) player.FrameEventRaised += e => visited.Add(e.Name);
        player.Play("c");
        return player;
    }

    private static int[] FrameIndexes(AnimationPlayer<TestFrame> player, int updates)
    {
        var result = new int[updates];
        for (int i = 0; i < updates; i++)
        {
            player.Update(TimeSpan.FromSeconds(1));
            result[i] = player.CurrentFrameIndex;
        }
        return result;
    }

    [Fact]
    public void Play_SeedsPingPongAndReverseFromTheChain()
    {
        var player = MakePlayer(loop: true, pingPong: true, reverse: true);

        player.IsPingPong.ShouldBeTrue();
        player.IsReversed.ShouldBeTrue();
    }

    [Fact]
    public void Update_PingPong_BouncesWithoutRepeatingTheEndFrames()
    {
        var player = MakePlayer(loop: true, pingPong: true, reverse: false);

        // Starts on 0; landing at 1s, 2s, ... (frame lookup holds the boundary in the earlier frame).
        player.Update(TimeSpan.FromSeconds(0.5));
        FrameIndexes(player, 5).ShouldBe(new[] { 1, 2, 1, 0, 1 });
    }

    [Fact]
    public void Update_Reverse_StartsOnTheLastFrameAndPlaysBackward()
    {
        var player = MakePlayer(loop: true, pingPong: false, reverse: true);
        player.CurrentFrameIndex.ShouldBe(2);

        player.Update(TimeSpan.FromSeconds(0.5));
        FrameIndexes(player, 3).ShouldBe(new[] { 1, 0, 2 });
    }

    [Fact]
    public void Update_NonLoopingPingPong_PlaysOneRoundTripThenFinishes()
    {
        var visited = new List<string>();
        var player = MakePlayer(loop: false, pingPong: true, reverse: false, visited);
        int finished = 0;
        player.AnimationFinished += () => finished++;

        player.Update(TimeSpan.FromSeconds(10));

        player.CurrentFrameIndex.ShouldBe(0);
        finished.ShouldBe(1);
        visited.ShouldBe(new[] { "0", "1", "2", "1", "0" });
    }

    [Fact]
    public void IsReversed_SetAfterPlay_OverridesTheSeededValue()
    {
        var player = MakePlayer(loop: true, pingPong: false, reverse: false);

        player.IsReversed = true;
        player.Update(TimeSpan.FromSeconds(0.5));

        player.CurrentFrameIndex.ShouldBe(2);
    }
}
