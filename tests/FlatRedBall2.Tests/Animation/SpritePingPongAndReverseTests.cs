using System;
using System.Collections.Generic;
using FlatRedBall2.Animation;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;
using KeyframeCurve = FlatRedBall2.AnimationEditorCommon.KeyframeCurve;

namespace FlatRedBall2.Tests.Animation;

/// <summary>
/// PingPong and Reverse on <see cref="Sprite"/> (#1538): mirrors of the forward timeline. The chains are the user's
/// repro: A at x=0 with Bounce Out to B at x=80, 0.6s each.
/// </summary>
public class SpritePingPongAndReverseTests
{
    private static AnimationChain BounceChain(bool loop = true, bool pingPong = false, bool reverse = false, int frames = 2)
    {
        var chain = new AnimationChain { Name = "c", Loop = loop, PingPong = pingPong, Reversed = reverse };
        for (int i = 0; i < frames; i++)
        {
            var frame = new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.6), RelativeX = i * 80 };
            if (i < frames - 1) frame.OffsetCurve = KeyframeCurve.Parse("Bounce", "Out");
            frame.Events.Add(new AnimationFrameEvent { Name = i.ToString() });
            chain.Add(frame);
        }
        return chain;
    }

    private static Sprite Play(AnimationChain chain)
    {
        var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
        sprite.PlayAnimation("c");
        return sprite;
    }

    private static float XAt(AnimationChain chain, double seconds)
    {
        var sprite = Play(chain);
        sprite.AnimateSelf(seconds);
        return sprite.X;
    }

    private static int FrameOf(Sprite sprite) => sprite.CurrentAnimation!.IndexOf(sprite.CurrentFrame!);

    [Fact]
    public void PlayAnimation_SeedsIsPingPongAndIsReversedFromTheChain()
    {
        var sprite = Play(BounceChain(pingPong: true, reverse: true));

        sprite.IsPingPong.ShouldBeTrue();
        sprite.IsReversed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    public void PingPong_SecondHalf_IsTheForwardTimelinePlayedBackward(double d)
    {
        // 3_Bounce_PingPong: A bounces into B, B holds 0.6s, holds another 0.6s at the turnaround, then A bounces back out.
        XAt(BounceChain(pingPong: true), 1.8 + d).ShouldBe(XAt(BounceChain(), 0.6 - d), 0.001f);
        XAt(BounceChain(pingPong: true), d).ShouldBe(XAt(BounceChain(), d), 0.001f);
        XAt(BounceChain(pingPong: true), 0.6 + d).ShouldBe(80f);
        XAt(BounceChain(pingPong: true), 1.2 + d).ShouldBe(80f);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.7)]
    [InlineData(1.1)]
    public void Reverse_ValueAtT_IsTheForwardValueAtLengthMinusT(double t)
    {
        // 2_Bounce_Reverse: B holds for 0.6s, then A's bounce plays backward, a 1.2s loop.
        XAt(BounceChain(reverse: true), t).ShouldBe(XAt(BounceChain(), 1.2 - t), 0.001f);
    }

    [Fact]
    public void AnimationLooped_PingPong_FiresOncePerTwoPoint4SecondCycle()
    {
        var sprite = Play(BounceChain(pingPong: true));
        int looped = 0;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(2.3);
        looped.ShouldBe(0);
        sprite.AnimateSelf(0.2);
        looped.ShouldBe(1);
        sprite.AnimateSelf(2.4);
        looped.ShouldBe(2);
    }

    [Fact]
    public void FrameChanged_PingPong_NotRaisedAtTheTurnaroundOrTheLoopPoint()
    {
        var sprite = Play(BounceChain(pingPong: true));
        var changes = new List<double>();
        double time = 0;
        sprite.FrameChanged += () => changes.Add(time);

        for (int i = 0; i < 50; i++)
        {
            time += 0.07; // a step that never lands exactly on a boundary
            sprite.AnimateSelf(0.07);
        }

        // B is entered after 0.6 + n*2.4 and A after 1.8 + n*2.4; nothing at 1.2 (turnaround) or 2.4 (loop).
        changes.Count.ShouldBe(3);
        changes[0].ShouldBe(0.63, 0.001);
        changes[1].ShouldBe(1.82, 0.001);
        changes[2].ShouldBe(3.01, 0.001);
    }

    [Fact]
    public void FrameEventRaised_PingPong_OnlyWhenTheShownFrameChanges()
    {
        var sprite = Play(BounceChain(pingPong: true, frames: 3));
        var events = new List<string>();
        sprite.FrameEventRaised += e => events.Add(e.Name);

        sprite.AnimateSelf(3.6 + 0.3); // one full cycle plus a bit

        events.ShouldBe(new List<string> { "1", "2", "1", "0" });
    }

    [Fact]
    public void PlayAnimation_Reverse_StartsOnTheLastFrameAndRaisesItsEvents()
    {
        var chain = BounceChain(reverse: true);
        var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
        var events = new List<string>();
        sprite.FrameEventRaised += e => events.Add(e.Name);

        sprite.PlayAnimation("c");

        FrameOf(sprite).ShouldBe(1);
        events.ShouldBe(new List<string> { "1" });
    }

    [Fact]
    public void PingPongReverse_StartsWithTheMirroredHalf()
    {
        XAt(BounceChain(pingPong: true, reverse: true), 0.3).ShouldBe(80f);
        XAt(BounceChain(pingPong: true, reverse: true), 0.9).ShouldBe(XAt(BounceChain(), 0.3), 0.001f);
        XAt(BounceChain(pingPong: true, reverse: true), 1.5).ShouldBe(XAt(BounceChain(), 0.3), 0.001f);
    }

    [Fact]
    public void PingPong_NonLooping_PlaysOneFullCycle_ThenFinishesOnTheFirstFrame()
    {
        var sprite = Play(BounceChain(loop: false, pingPong: true));
        int finished = 0;
        sprite.AnimationFinished += () => finished++;

        sprite.AnimateSelf(2.3);
        finished.ShouldBe(0);
        sprite.AnimateSelf(0.2);

        finished.ShouldBe(1);
        FrameOf(sprite).ShouldBe(0);
        sprite.X.ShouldBe(0f);
    }

    [Fact]
    public void IsReversed_SetAfterPlay_OverridesTheSeededValue()
    {
        var sprite = Play(BounceChain());

        sprite.IsReversed = true;
        sprite.AnimateSelf(0.3);

        FrameOf(sprite).ShouldBe(1);
    }

    [Fact]
    public void PingPong_BackwardHalf_HitboxFollowsTheShownFrame()
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

        sprite.AnimateSelf(4.5); // forward time 1.5: frame 1 on the way back

        AARect? sword = null;
        foreach (var c in entity.Children)
            if (c is AARect r && r.Name == "Sword") sword = r;
        sword!.Width.ShouldBe(20f);
    }
}
