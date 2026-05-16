using System;
using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using FlatRedBall2.Tweening;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class PauseTests
{
    private class TestScreen : Screen { }

    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    private static AnimationChainList MakeChain(string name, int frameCount, float frameLength)
    {
        var chain = new AnimationChain { Name = name };
        for (int i = 0; i < frameCount; i++)
            chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(frameLength) });
        var list = new AnimationChainList();
        list.Add(chain);
        return list;
    }

    [Fact]
    public void Update_WhenPaused_SpriteAnimationDoesNotAdvance()
    {
        var screen = new TestScreen();
        screen.PauseThisScreen();
        var sprite = new Sprite { IsLooping = false };
        sprite.AnimationChains = MakeChain("Anim", 2, 0.1f); // total 0.2s
        sprite.PlayAnimation("Anim");
        screen.Add(sprite);

        // dt = 0.5s is more than enough to complete the 0.2s animation
        screen.Update(Frame(0.5f));

        // Animation was NOT advanced — Animate is still true (non-looping anim did not finish)
        sprite.Animate.ShouldBeTrue();
    }

    [Fact]
    public void Update_WhenPaused_TweenDoesNotAdvance()
    {
        var screen = new TestScreen();
        screen.PauseThisScreen();
        bool setterCalled = false;
        screen.Tween(_ => setterCalled = true, from: 0f, to: 1f, duration: TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));

        setterCalled.ShouldBeFalse();
    }

    [Fact]
    public void Update_WhenPausedAndShouldAdvanceOnPause_SpriteAnimationAdvances()
    {
        var screen = new TestScreen();
        screen.PauseThisScreen();
        var sprite = new Sprite
        {
            IsLooping = false,
            ShouldAnimationAdvanceOnPause = true,
        };
        sprite.AnimationChains = MakeChain("Anim", 2, 0.1f); // total 0.2s
        sprite.PlayAnimation("Anim");
        screen.Add(sprite);

        // dt = 0.5s is more than enough to complete the 0.2s animation
        screen.Update(Frame(0.5f));

        // Animation DID advance — non-looping animation completed, so Animate is now false
        sprite.Animate.ShouldBeFalse();
    }

    [Fact]
    public void Update_WhenPausedAndShouldAdvanceOnPause_TweenAdvances()
    {
        var screen = new TestScreen();
        screen.PauseThisScreen();
        screen._tweens.ShouldAdvanceOnPause = true;
        bool setterCalled = false;
        screen.Tween(_ => setterCalled = true, from: 0f, to: 1f, duration: TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));

        setterCalled.ShouldBeTrue();
    }
}
