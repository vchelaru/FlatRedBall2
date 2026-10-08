using System;
using System.Collections.Generic;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

public class SpriteFrameEventTests
{
    // One 0.1s frame per entry; a non-null entry puts an event with that name on the frame.
    private static AnimationChainList MakeChain(string chainName, params string?[] frameEvents)
    {
        var chain = new AnimationChain { Name = chainName };
        foreach (var eventName in frameEvents)
        {
            var frame = new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.1) };
            if (eventName != null) frame.Events.Add(new AnimationFrameEvent { Name = eventName });
            chain.Add(frame);
        }
        var list = new AnimationChainList();
        list.Add(chain);
        return list;
    }

    private static List<string> Record(Sprite sprite)
    {
        var raised = new List<string>();
        sprite.FrameEventRaised += e => raised.Add(e.Name);
        return raised;
    }

    [Fact]
    public void AnimateSelf_DeltaSkipsFrames_RaisesSkippedFrameEventsInOrder()
    {
        var sprite = new Sprite { AnimationChains = MakeChain("Walk", null, "B", "C", "D") };
        var raised = Record(sprite);
        sprite.PlayAnimation("Walk");

        // 0 -> 0.25s lands on frame 2, passing through frame 1 within one tick.
        sprite.AnimateSelf(0.25);

        raised.ShouldBe(new[] { "B", "C" });
    }

    [Fact]
    public void AnimateSelf_DeltaSpansMoreThanOneLoop_RaisesEachFrameOnceEndingOnLandingFrame()
    {
        var sprite = new Sprite { AnimationChains = MakeChain("Walk", "A", "B", "C") };
        var raised = Record(sprite);
        sprite.PlayAnimation("Walk");
        raised.Clear();

        // 0.65s over a 0.3s loop lands on frame 0 after two wraps.
        sprite.AnimateSelf(0.65);

        raised.ShouldBe(new[] { "B", "C", "A" });
    }

    [Fact]
    public void AnimateSelf_LoopWraps_RaisesFrameZeroEventAgain()
    {
        var sprite = new Sprite { AnimationChains = MakeChain("Walk", "A", null) };
        var raised = Record(sprite);
        sprite.PlayAnimation("Walk");

        sprite.AnimateSelf(0.15);
        sprite.AnimateSelf(0.1);

        raised.ShouldBe(new[] { "A", "A" });
    }

    [Fact]
    public void AnimateSelf_HandlerSwitchesAnimation_StopsRaisingOldChainEvents()
    {
        var list = MakeChain("Attack", null, "Hit", "Late");
        list.Add(MakeChain("Idle", "IdleStart")[0]);
        var sprite = new Sprite { AnimationChains = list };
        var raised = Record(sprite);
        sprite.FrameEventRaised += e => { if (e.Name == "Hit") sprite.PlayAnimation("Idle"); };
        sprite.PlayAnimation("Attack");

        sprite.AnimateSelf(0.25);

        raised.ShouldBe(new[] { "Hit", "IdleStart" });
        sprite.CurrentAnimation!.Name.ShouldBe("Idle");
    }

    [Fact]
    public void AnimateSelf_NonLoopingReachesEnd_RaisesLastFrameEventBeforeAnimationFinished()
    {
        var sprite = new Sprite { AnimationChains = MakeChain("Die", null, "Last") };
        var raised = Record(sprite);
        sprite.AnimationFinished += () => raised.Add("Finished");
        sprite.PlayAnimation("Die");
        sprite.IsLooping = false;

        sprite.AnimateSelf(1.0);

        raised.ShouldBe(new[] { "Last", "Finished" });
    }

    [Fact]
    public void PlayAnimation_FrameZeroHasEvent_RaisesImmediately()
    {
        var sprite = new Sprite { AnimationChains = MakeChain("Jump", "Takeoff", null) };
        var raised = Record(sprite);

        sprite.PlayAnimation("Jump");
        sprite.PlayAnimation("Jump"); // already playing: no restart, no second raise

        raised.ShouldBe(new[] { "Takeoff" });
    }

    [Fact]
    public void ToAnimationChainList_FrameEvents_CopiedToRuntimeFrame()
    {
        var frameSave = new AnimationFrameSave { FrameLength = 0.1f };
        frameSave.Events.Add(new AnimationFrameEvent { Name = "Footstep", Data = "left" });
        var save = new AnimationChainListSave();
        save.AnimationChains.Add(new AnimationChainSave { Name = "Walk", Frames = { frameSave } });

        var frameEvent = save.ToAnimationChainList(new ContentLoader())[0][0].Events.ShouldHaveSingleItem();

        frameEvent.Name.ShouldBe("Footstep");
        frameEvent.Data.ShouldBe("left");
        frameEvent.ShouldNotBeSameAs(frameSave.Events[0]);
    }

    [Fact]
    public void AnimateSelf_FinishedHandlerSwitchesAnimation_LastFrameEventAlreadyRaised()
    {
        var list = MakeChain("Die", null, "Last");
        list.Add(MakeChain("Idle", "IdleStart")[0]);
        var sprite = new Sprite { AnimationChains = list };
        var raised = Record(sprite);
        sprite.AnimationFinished += () => { raised.Add("Finished"); sprite.PlayAnimation("Idle"); };
        sprite.PlayAnimation("Die");
        sprite.IsLooping = false;

        sprite.AnimateSelf(1.0);

        raised.ShouldBe(new[] { "Last", "Finished", "IdleStart" });
    }

    [Fact]
    public void AnimateSelf_EventHandlerSwitchesAnimation_SkipsOldChainAnimationFinished()
    {
        var list = MakeChain("Die", null, "Last");
        list.Add(MakeChain("Idle", "IdleStart")[0]);
        var sprite = new Sprite { AnimationChains = list };
        var raised = Record(sprite);
        sprite.AnimationFinished += () => raised.Add("Finished");
        sprite.FrameEventRaised += e => { if (e.Name == "Last") sprite.PlayAnimation("Idle"); };
        sprite.PlayAnimation("Die");
        sprite.IsLooping = false;

        sprite.AnimateSelf(1.0);

        raised.ShouldBe(new[] { "Last", "IdleStart" });
    }
}
