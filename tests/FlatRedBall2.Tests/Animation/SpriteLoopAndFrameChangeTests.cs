using System;
using System.Collections.Generic;
using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

public class SpriteLoopAndFrameChangeTests
{
    // Each chain gets FrameCount frames of 0.1s.
    private static AnimationChainList MakeChains(params (string Name, int FrameCount, bool Loop)[] chains)
    {
        var list = new AnimationChainList();
        foreach (var (name, frameCount, loop) in chains)
        {
            var chain = new AnimationChain { Name = name, Loop = loop };
            for (int i = 0; i < frameCount; i++)
                chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.1) });
            list.Add(chain);
        }
        return list;
    }

    private static Sprite MakeSprite(string name, int frameCount, bool loop = true)
    {
        var sprite = new Sprite { AnimationChains = MakeChains((name, frameCount, loop)) };
        sprite.PlayAnimation(name);
        return sprite;
    }

    [Fact]
    public void AnimationLooped_LoopingChainWraps_RaisedOnceOnTheWrappingUpdate()
    {
        var sprite = MakeSprite("Walk", 3);
        int looped = 0;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(0.25);
        looped.ShouldBe(0);

        sprite.AnimateSelf(0.1); // 0.35 > 0.3
        looped.ShouldBe(1);

        sprite.AnimateSelf(0.05);
        looped.ShouldBe(1);
    }

    [Fact]
    public void AnimationLooped_DeltaSpansSeveralLoops_RaisedOncePerUpdate()
    {
        var sprite = MakeSprite("Walk", 3);
        int looped = 0;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(0.95); // three wraps

        looped.ShouldBe(1);
    }

    [Fact]
    public void AnimationLooped_SingleFrameLoopingChain_RaisedEachTimeFrameLengthElapses()
    {
        var sprite = MakeSprite("Still", 1);
        int looped = 0;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(0.05);
        looped.ShouldBe(0);
        sprite.AnimateSelf(0.06);
        looped.ShouldBe(1);
        sprite.AnimateSelf(0.1);
        looped.ShouldBe(2);
    }

    [Fact]
    public void AnimationLooped_NonLoopingChain_NeverRaisedButAnimationFinishedIs()
    {
        var sprite = MakeSprite("Once", 2, loop: false);
        int looped = 0, finished = 0;
        sprite.AnimationLooped += () => looped++;
        sprite.AnimationFinished += () => finished++;

        sprite.AnimateSelf(0.5);

        looped.ShouldBe(0);
        finished.ShouldBe(1);
    }

    [Fact]
    public void AnimationLooped_IsLoopingOverriddenToTrueOnNonLoopingChain_Raised()
    {
        var sprite = MakeSprite("Once", 2, loop: false);
        sprite.IsLooping = true;
        int looped = 0;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(0.25);

        looped.ShouldBe(1);
    }

    [Fact]
    public void AnimationLooped_AnimateFalse_NotRaised()
    {
        var sprite = MakeSprite("Walk", 3);
        sprite.Animate = false;
        int looped = 0;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(1.0);

        looped.ShouldBe(0);
    }

    [Fact]
    public void AnimationLooped_AnimationSpeedScalesTimeToLoop()
    {
        var sprite = MakeSprite("Walk", 3);
        sprite.AnimationSpeed = 2f;
        int looped = 0;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(0.2); // 0.4 animation seconds

        looped.ShouldBe(1);
    }

    [Fact]
    public void AnimationLooped_ZeroLengthChain_NotRaised()
    {
        var chain = new AnimationChain { Name = "Z" };
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.Zero });
        var list = new AnimationChainList();
        list.Add(chain);
        var sprite = new Sprite { AnimationChains = list };
        sprite.PlayAnimation("Z");
        int looped = 0;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(1.0);

        looped.ShouldBe(0);
    }

    [Fact]
    public void FrameChanged_AdvancesToNextFrame_RaisedWithNewFrameCurrent()
    {
        var sprite = MakeSprite("Walk", 3);
        var seen = new List<AnimationFrame?>();
        sprite.FrameChanged += () => seen.Add(sprite.CurrentFrame);

        sprite.AnimateSelf(0.05);
        seen.Count.ShouldBe(0);

        sprite.AnimateSelf(0.06);
        seen.Count.ShouldBe(1);
        seen[0].ShouldBeSameAs(sprite.CurrentAnimation![1]);
    }

    [Fact]
    public void FrameChanged_DeltaSkipsSeveralFrames_RaisedOncePerUpdate()
    {
        var sprite = MakeSprite("Walk", 5);
        int changed = 0;
        sprite.FrameChanged += () => changed++;

        sprite.AnimateSelf(0.35); // frame 0 -> 3

        changed.ShouldBe(1);
    }

    [Fact]
    public void FrameChanged_LoopWrapsToFrameZero_Raised()
    {
        var sprite = MakeSprite("Walk", 2);
        sprite.AnimateSelf(0.15); // frame 1
        int changed = 0;
        sprite.FrameChanged += () => changed++;

        sprite.AnimateSelf(0.1); // wraps to frame 0

        changed.ShouldBe(1);
        sprite.CurrentFrame.ShouldBeSameAs(sprite.CurrentAnimation![0]);
    }

    [Fact]
    public void FrameChanged_SingleFrameChainLoops_NotRaised()
    {
        var sprite = MakeSprite("Still", 1);
        int changed = 0, looped = 0;
        sprite.FrameChanged += () => changed++;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(0.35);

        changed.ShouldBe(0);
        looped.ShouldBe(1);
    }

    [Fact]
    public void FrameChanged_DeltaIsWholeNumberOfLoops_LoopedButNotFrameChanged()
    {
        var sprite = MakeSprite("Walk", 3);
        sprite.AnimateSelf(0.05);
        int changed = 0, looped = 0;
        sprite.FrameChanged += () => changed++;
        sprite.AnimationLooped += () => looped++;

        sprite.AnimateSelf(0.3); // exactly one loop, lands on the same frame

        changed.ShouldBe(0);
        looped.ShouldBe(1);
    }

    [Fact]
    public void FrameChanged_NonLoopingReachesLastFrame_RaisedBeforeAnimationFinished()
    {
        var sprite = MakeSprite("Once", 2, loop: false);
        var order = new List<string>();
        sprite.FrameChanged += () => order.Add("changed");
        sprite.AnimationFinished += () => order.Add("finished");

        sprite.AnimateSelf(0.5);

        order.ShouldBe(new[] { "changed", "finished" });
    }

    [Fact]
    public void FrameChanged_NonLoopingAlreadyFinished_NotRaisedAgain()
    {
        var sprite = MakeSprite("Once", 2, loop: false);
        sprite.AnimateSelf(0.5);
        int changed = 0;
        sprite.FrameChanged += () => changed++;

        sprite.AnimateSelf(0.5);

        changed.ShouldBe(0);
    }

    [Fact]
    public void FrameChanged_PlayAnimationSwitchesChain_Raised()
    {
        var sprite = new Sprite { AnimationChains = MakeChains(("A", 2, true), ("B", 2, true)) };
        int changed = 0;
        sprite.FrameChanged += () => changed++;

        sprite.PlayAnimation("A");
        changed.ShouldBe(1);

        sprite.PlayAnimation("B");
        changed.ShouldBe(2);
    }

    [Fact]
    public void FrameChanged_PlayAnimationSameChainAgain_NotRaised()
    {
        var sprite = MakeSprite("A", 2);
        int changed = 0;
        sprite.FrameChanged += () => changed++;

        sprite.PlayAnimation("A");

        changed.ShouldBe(0);
    }

    [Fact]
    public void FrameChanged_PlayAnimationByChainInstance_Raised()
    {
        var chains = MakeChains(("A", 2, true));
        var sprite = new Sprite { AnimationChains = chains };
        int changed = 0;
        sprite.FrameChanged += () => changed++;

        sprite.PlayAnimation(chains[0]);

        changed.ShouldBe(1);
    }

    [Fact]
    public void FrameChanged_HandlerSwitchesAnimation_SuppressesOldChainLoopNotification()
    {
        var sprite = new Sprite { AnimationChains = MakeChains(("A", 3, true), ("B", 3, true)) };
        sprite.PlayAnimation("A");
        int looped = 0;
        sprite.AnimationLooped += () => looped++;
        sprite.FrameChanged += () => { if (sprite.CurrentAnimation!.Name == "A") sprite.PlayAnimation("B"); };

        sprite.AnimateSelf(0.45); // wraps, lands on frame 1

        sprite.CurrentAnimation!.Name.ShouldBe("B");
        looped.ShouldBe(0);
    }
}
