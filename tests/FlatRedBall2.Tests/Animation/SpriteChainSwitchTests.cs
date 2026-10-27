using System;
using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

/// <summary>Chain switching on <see cref="Sprite"/> matches FRB1's hardened behavior (#1554).</summary>
public class SpriteChainSwitchTests
{
    private static AnimationChain Chain(string name, bool reverse = false, bool pingPong = false)
    {
        var chain = new AnimationChain { Name = name, Loop = true, Reversed = reverse, PingPong = pingPong };
        for (int i = 0; i < 3; i++)
            chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.5), RelativeX = i * 10 });
        return chain;
    }

    private static Sprite Playing(params AnimationChain[] chains)
    {
        var list = new AnimationChainList();
        foreach (var c in chains) list.Add(c);
        var sprite = new Sprite { AnimationChains = list };
        sprite.PlayAnimation(chains[0].Name);
        sprite.AnimateSelf(0.7);
        return sprite;
    }

    private static int FrameOf(Sprite s) => s.CurrentAnimation!.IndexOf(s.CurrentFrame!);

    [Fact]
    public void PlayAnimation_DifferentChain_RestartsAndAppliesFrameImmediately()
    {
        var sprite = Playing(Chain("a"), Chain("b"));
        FrameOf(sprite).ShouldBe(1);

        sprite.PlayAnimation("b");

        sprite.CurrentAnimation!.Name.ShouldBe("b");
        FrameOf(sprite).ShouldBe(0);
        sprite.X.ShouldBe(0f);
        sprite.AnimateSelf(0.3);
        FrameOf(sprite).ShouldBe(0);
    }

    [Fact]
    public void PlayAnimation_SameChain_IsANoOp()
    {
        var sprite = Playing(Chain("a"));

        sprite.PlayAnimation("a");

        FrameOf(sprite).ShouldBe(1);
        sprite.AnimateSelf(0.4);
        FrameOf(sprite).ShouldBe(2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayAnimation_ReverseChain_ShowsLastFrameFromTimeZero(bool pingPong)
    {
        var sprite = Playing(Chain("a"), Chain("r", reverse: true, pingPong: pingPong));

        sprite.PlayAnimation("r");

        FrameOf(sprite).ShouldBe(2);
        sprite.X.ShouldBe(20f);
        sprite.AnimateSelf(0.1);
        FrameOf(sprite).ShouldBe(2);
    }

    [Fact]
    public void PlayAnimation_DifferentChain_RaisesFrameChangedWithNewFrameApplied()
    {
        var sprite = Playing(Chain("a"), Chain("r", reverse: true));
        AnimationFrame? seen = null;
        sprite.FrameChanged += () => seen = sprite.CurrentFrame;

        sprite.PlayAnimation("r");

        seen.ShouldNotBeNull();
        seen.ShouldBeSameAs(sprite.CurrentAnimation![2]);
    }
}
