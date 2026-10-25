using System;
using System.Collections.Generic;
using FlatRedBall2.Animation;
using KeyframeCurve = FlatRedBall2.AnimationEditorCommon.KeyframeCurve;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

/// <summary>PingPong and Reverse playback on <see cref="Sprite"/> (#1538).</summary>
public class SpritePingPongAndReverseTests
{
    // Three 1s frames; frame i has RelativeX = i * 10 so the displayed frame is observable through X.
    private static Sprite MakeSprite(bool loop = true, bool pingPong = false, bool reverse = false)
    {
        var chain = new AnimationChain { Name = "c", Loop = loop, PingPong = pingPong, Reversed = reverse };
        for (int i = 0; i < 3; i++)
        {
            var frame = new AnimationFrame { FrameLength = TimeSpan.FromSeconds(1), RelativeX = i * 10 };
            frame.Events.Add(new AnimationFrameEvent { Name = i.ToString() });
            chain.Add(frame);
        }
        var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
        sprite.PlayAnimation("c");
        return sprite;
    }

    private static List<int> Step(Sprite sprite, int updates)
    {
        var frames = new List<int>();
        for (int i = 0; i < updates; i++)
        {
            sprite.AnimateSelf(1);
            frames.Add(sprite.CurrentAnimation!.IndexOf(sprite.CurrentFrame!));
        }
        return frames;
    }

    [Fact]
    public void PlayAnimation_SeedsIsPingPongAndIsReversedFromTheChain()
    {
        var sprite = MakeSprite(pingPong: true, reverse: true);

        sprite.IsPingPong.ShouldBeTrue();
        sprite.IsReversed.ShouldBeTrue();
    }

    [Fact]
    public void AnimateSelf_PingPong_BouncesWithoutRepeatingTheEndFrames()
    {
        var sprite = MakeSprite(pingPong: true);
        sprite.AnimateSelf(0.5);

        Step(sprite, 6).ShouldBe(new List<int> { 1, 2, 1, 0, 1, 2 });
    }

    [Fact]
    public void AnimationLooped_PingPong_RaisedOncePerRoundTrip()
    {
        var sprite = MakeSprite(pingPong: true);
        int looped = 0;
        sprite.AnimationLooped += () => looped++;
        sprite.AnimateSelf(0.5);

        Step(sprite, 3); // 0 -> 1 -> 2 -> 1, still in the first cycle
        looped.ShouldBe(0);
        Step(sprite, 1); // back to 0
        looped.ShouldBe(1);
    }

    [Fact]
    public void PlayAnimation_Reverse_StartsOnTheLastFrameAndRaisesItsEvents()
    {
        var events = new List<string>();
        var chain = new AnimationChain { Name = "c", Reversed = true };
        for (int i = 0; i < 3; i++)
        {
            var frame = new AnimationFrame { FrameLength = TimeSpan.FromSeconds(1) };
            frame.Events.Add(new AnimationFrameEvent { Name = i.ToString() });
            chain.Add(frame);
        }
        var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
        sprite.FrameEventRaised += e => events.Add(e.Name);

        sprite.PlayAnimation("c");

        sprite.CurrentFrame.ShouldBe(chain[2]);
        events.ShouldBe(new List<string> { "2" });
    }

    [Fact]
    public void AnimateSelf_ReversePingPong_GoesBackwardThenForward()
    {
        var sprite = MakeSprite(pingPong: true, reverse: true);
        sprite.AnimateSelf(0.5);

        Step(sprite, 4).ShouldBe(new List<int> { 1, 0, 1, 2 });
    }

    [Fact]
    public void AnimateSelf_NonLoopingPingPong_EndsOnFirstFrame_RaisingEveryVisitThenFinished()
    {
        var sprite = MakeSprite(loop: false, pingPong: true);
        var order = new List<string>();
        sprite.FrameEventRaised += e => order.Add(e.Name);
        sprite.AnimationFinished += () => order.Add("finished");

        sprite.AnimateSelf(10);

        sprite.CurrentFrame.ShouldBe(sprite.CurrentAnimation![0]);
        order.ShouldBe(new List<string> { "1", "2", "1", "0", "finished" });
    }

    [Fact]
    public void FrameChanged_PingPong_RaisedOnEachVisitInEitherDirection()
    {
        var sprite = MakeSprite(pingPong: true);
        int changed = 0;
        sprite.FrameChanged += () => changed++;
        sprite.AnimateSelf(0.5);

        Step(sprite, 4);

        changed.ShouldBe(4);
    }

    [Fact]
    public void IsReversed_SetAfterPlay_OverridesTheSeededValue()
    {
        var sprite = MakeSprite();

        sprite.IsReversed = true;
        sprite.AnimateSelf(0.5);

        sprite.CurrentFrame.ShouldBe(sprite.CurrentAnimation![2]);
    }

    [Fact]
    public void AnimateSelf_ReverseWithLinearOffset_GlidesTowardThePreviousFrame()
    {
        var chain = new AnimationChain { Name = "c", Reversed = true, Loop = false };
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(1), RelativeX = 0, OffsetCurve = KeyframeCurve.Parse("Linear", null) });
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(1), RelativeX = 100 });
        var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
        sprite.PlayAnimation("c");

        sprite.AnimateSelf(0.25);

        sprite.X.ShouldBe(75f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_PingPongBackwardHalf_HitboxFollowsTheDisplayedFrame()
    {
        var entity = new Entity();
        var sprite = new Sprite();
        entity.Add(sprite);
        var chain = new AnimationChain { Name = "Attack", PingPong = true };
        for (int i = 0; i < 3; i++)
        {
            var frame = new AnimationFrame { FrameLength = TimeSpan.FromSeconds(1) };
            frame.Shapes.Add(new AnimationAARectFrame { Name = "Sword", Width = 10 + i * 10, Height = 5 });
            chain.Add(frame);
        }
        sprite.AnimationChains = new AnimationChainList { chain };
        sprite.PlayAnimation("Attack");

        sprite.AnimateSelf(3.5); // visit 3: frame 1 on the way back

        AARect? sword = null;
        foreach (var c in entity.Children)
            if (c is AARect r && r.Name == "Sword") sword = r;
        sword!.Width.ShouldBe(20f);
    }
}
