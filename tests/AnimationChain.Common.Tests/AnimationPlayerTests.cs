using System;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

public class AnimationPlayerTests
{
    private class TestFrame : AnimationFrameBase { }

    private static AnimationChainList<TestFrame> MakeList(params (string name, double[] frameLengths)[] chains)
    {
        var list = new AnimationChainList<TestFrame>();
        foreach (var (name, frameLengths) in chains)
        {
            var chain = new AnimationChain<TestFrame> { Name = name };
            foreach (var len in frameLengths)
                chain.Add(new TestFrame { FrameLength = TimeSpan.FromSeconds(len) });
            list.Add(chain);
        }
        return list;
    }

    [Fact]
    public void Play_ChainLoopFalse_SeedsIsLoopingFalse()
    {
        var list = MakeList(("Attack", new[] { 0.1 }));
        list["Attack"]!.Loop = false;
        var player = new AnimationPlayer<TestFrame>(list);

        player.Play("Attack");

        player.IsLooping.ShouldBeFalse();
    }

    [Fact]
    public void Play_ChainLoopTrue_SeedsIsLoopingTrue()
    {
        var list = MakeList(("Attack", new[] { 0.1 }));
        list["Attack"]!.Loop = false;
        var player = new AnimationPlayer<TestFrame>(list) { IsLooping = false };
        var loopingChain = new AnimationChain<TestFrame> { Name = "Walk", Loop = true };
        loopingChain.Add(new TestFrame { FrameLength = TimeSpan.FromSeconds(0.1) });
        list.Add(loopingChain);

        player.Play("Walk");

        player.IsLooping.ShouldBeTrue();
    }

    [Fact]
    public void Play_SeededIsLooping_CanStillBeOverridden()
    {
        var list = MakeList(("Attack", new[] { 0.1 }));
        list["Attack"]!.Loop = false;
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Attack");

        player.IsLooping = true; // game code overrides the seeded value

        player.IsLooping.ShouldBeTrue();
    }

    private static TimeSpan Sec(double s) => TimeSpan.FromSeconds(s);

    [Fact]
    public void Play_ByName_SetsFirstFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);

        player.Play("Run");

        player.CurrentFrame.ShouldBeSameAs(list["Run"]![0]);
    }

    [Fact]
    public void Play_UnknownName_IsNoOp()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");

        player.Play("DoesNotExist"); // must not throw

        player.CurrentChain!.Name.ShouldBe("Run");
    }

    [Fact]
    public void Play_SameChainTwice_DoesNotRestart()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.Update(Sec(0.15)); // advances past first frame
        var frameAfterAdvance = player.CurrentFrame;

        player.Play("Run"); // same chain — should not restart

        player.CurrentFrame.ShouldBeSameAs(frameAfterAdvance);
    }

    [Fact]
    public void Play_DifferentChain_Restarts()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }), ("Idle", new[] { 0.2 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.Update(Sec(0.15));

        player.Play("Idle");

        player.CurrentChain!.Name.ShouldBe("Idle");
        player.CurrentFrame.ShouldBeSameAs(list["Idle"]![0]);
    }

    [Fact]
    public void Play_ByChainReference_SameInstance_DoesNotRestart()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        var chain = list["Run"]!;
        player.Play(chain);
        player.Update(Sec(0.15));
        var frameBefore = player.CurrentFrame;

        player.Play(chain);

        player.CurrentFrame.ShouldBeSameAs(frameBefore);
    }

    [Fact]
    public void Pause_StopsAdvancement_UntilResume()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.Update(Sec(0.15)); // frame 1

        player.Pause();
        var pausedFrame = player.CurrentFrame;
        player.Update(Sec(0.5));
        player.CurrentFrame.ShouldBeSameAs(pausedFrame);

        player.Resume();
        player.Update(Sec(0.11));
        player.CurrentFrame.ShouldNotBeSameAs(pausedFrame);
    }

    [Fact]
    public void Stop_RewindsToFirstFrame_AndDisablesAnimate()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.Update(Sec(0.15)); // frame 1

        player.Stop();

        player.Animate.ShouldBeFalse();
        player.CurrentFrameIndex.ShouldBe(0);
        player.TimeIntoAnimation.ShouldBe(TimeSpan.Zero);
        player.CurrentFrame.ShouldBeSameAs(list["Run"]![0]);
    }

    [Fact]
    public void Reset_RewindsWithoutChangingAnimateState()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.Update(Sec(0.15)); // frame 1
        player.Pause();

        player.Reset();

        player.Animate.ShouldBeFalse();
        player.CurrentFrameIndex.ShouldBe(0);
        player.TimeIntoAnimation.ShouldBe(TimeSpan.Zero);
        player.CurrentFrame.ShouldBeSameAs(list["Run"]![0]);
    }

    [Fact]
    public void CurrentFrameIndex_Setter_SeeksToRequestedFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.2, 0.3 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");

        player.CurrentFrameIndex = 2;

        player.CurrentFrameIndex.ShouldBe(2);
        player.TimeIntoAnimation.ShouldBe(TimeSpan.FromSeconds(0.3));
        player.CurrentFrame.ShouldBeSameAs(list["Run"]![2]);
    }

    [Fact]
    public void TimeIntoAnimation_Setter_LoopsWhenIsLooping()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 })); // total = 0.2
        var player = new AnimationPlayer<TestFrame>(list) { IsLooping = true };
        player.Play("Run");

        player.TimeIntoAnimation = Sec(0.25); // wraps to 0.05 => frame 0

        player.CurrentFrameIndex.ShouldBe(0);
        player.TimeIntoAnimation.TotalSeconds.ShouldBe(0.05, tolerance: 0.0001);
        player.CurrentFrame.ShouldBeSameAs(list["Run"]![0]);
    }

    [Fact]
    public void TimeIntoAnimation_Setter_ClampsWhenNotLooping()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 })); // total = 0.2
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.IsLooping = false; // override the seeded default (chain.Loop defaults to true)

        player.TimeIntoAnimation = Sec(0.25);

        player.CurrentFrameIndex.ShouldBe(1);
        player.TimeIntoAnimation.ShouldBe(Sec(0.2));
        player.CurrentFrame.ShouldBeSameAs(list["Run"]![1]);
    }

    [Fact]
    public void Update_AdvancesToNextFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");

        player.Update(Sec(0.15));

        player.CurrentFrame.ShouldBeSameAs(list["Run"]![1]);
    }

    [Fact]
    public void Update_Looping_WrapsToFirstFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 })); // total = 0.2s
        var player = new AnimationPlayer<TestFrame>(list) { IsLooping = true };
        player.Play("Run");

        player.Update(Sec(0.25)); // past end of loop

        // 0.25 mod 0.2 = 0.05 -> still in first frame (0-0.1)
        player.CurrentFrame.ShouldBeSameAs(list["Run"]![0]);
    }

    [Fact]
    public void Update_NonLooping_StopsAtLastFrame()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.IsLooping = false; // override the seeded default (chain.Loop defaults to true)

        player.Update(Sec(0.5)); // well past end

        player.CurrentFrame.ShouldBeSameAs(list["Run"]![1]);
        player.Animate.ShouldBeFalse();
    }

    [Fact]
    public void Update_NonLooping_RaisesAnimationFinished()
    {
        var list = MakeList(("Run", new[] { 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.IsLooping = false; // override the seeded default (chain.Loop defaults to true)
        bool fired = false;
        player.AnimationFinished += () => fired = true;

        player.Update(Sec(0.2));

        fired.ShouldBeTrue();
    }

    [Fact]
    public void Update_AnimationFinished_RaisedOnce()
    {
        var list = MakeList(("Run", new[] { 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Run");
        player.IsLooping = false; // override the seeded default (chain.Loop defaults to true)
        int count = 0;
        player.AnimationFinished += () => count++;

        player.Update(Sec(0.2));
        player.Update(Sec(0.2)); // second update — Animate is false, should not re-fire

        count.ShouldBe(1);
    }

    [Fact]
    public void Update_AnimateIsFalse_DoesNotAdvance()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list) { Animate = false };
        player.Play("Run");
        player.Animate = false;

        player.Update(Sec(0.2));

        player.CurrentFrame.ShouldBeSameAs(list["Run"]![0]);
    }

    [Fact]
    public void AnimationSpeed_HalfSpeed_TakesDoubleTime()
    {
        var list = MakeList(("Run", new[] { 0.1, 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list) { AnimationSpeed = 0.5f };
        player.Play("Run");

        player.Update(Sec(0.15)); // at half speed this is only 0.075s of animation time

        player.CurrentFrame.ShouldBeSameAs(list["Run"]![0]); // still on first frame
    }

    [Fact]
    public void CurrentFrame_BeforePlay_IsNull()
    {
        var list = MakeList(("Run", new[] { 0.1 }));
        var player = new AnimationPlayer<TestFrame>(list);

        player.CurrentFrame.ShouldBeNull();
    }

    [Fact]
    public void Update_NoAnimation_DoesNotThrow()
    {
        var list = new AnimationChainList<TestFrame>();
        var player = new AnimationPlayer<TestFrame>(list);

        player.Update(Sec(0.1)); // nothing playing — must not throw
    }

    [Fact]
    public void Update_EmptyChain_DoesNotThrow()
    {
        var list = MakeList(("Empty", Array.Empty<double>()));
        var player = new AnimationPlayer<TestFrame>(list);
        player.Play("Empty");

        player.Update(Sec(0.1));
    }
}
