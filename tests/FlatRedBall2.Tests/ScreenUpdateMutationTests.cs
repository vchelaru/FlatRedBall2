using System;
using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using FlatRedBall2.Tweening;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

// Guards against two regressions in Screen.Update's per-frame loops:
// 1) Mutation-during-iteration: callbacks (AnimationFinished, tween Ended, CustomActivity)
//    may Destroy entities, which mutates _entities and _renderList mid-loop.
// 2) Per-frame heap allocations: Screen.Update is a hotpath — snapshotting via new List<>
//    is forbidden. See .claude/skills/engine-tdd/SKILL.md.
public class ScreenUpdateMutationTests
{
    private class TestScreen : Screen { }

    private class DestroyOnAnimFinishedEntity : Entity
    {
        public Sprite Sprite { get; private set; } = null!;
        public override void CustomInitialize()
        {
            Sprite = new Sprite { AnimationChains = MakeChain(), IsLooping = false };
            Add(Sprite);
            Sprite.PlayAnimation("Once");
            Sprite.AnimationFinished += Destroy;
        }
    }

    private class DestroyOnTweenEndedEntity : Entity
    {
        public override void CustomInitialize()
        {
            var tweener = this.Tween(_ => { }, from: 0f, to: 1f, duration: TimeSpan.FromSeconds(0.1));
            tweener.Ended += Destroy;
        }
    }

    // Destroys a sibling entity on its first CustomActivity.
    private class SiblingKiller : Entity
    {
        public Entity? Target;
        public override void CustomActivity(FrameTime frameTime) => Target?.Destroy();
    }

    private static AnimationChainList MakeChain()
    {
        var chain = new AnimationChain { Name = "Once" };
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.1f) });
        chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.1f) });
        var list = new AnimationChainList();
        list.Add(chain);
        return list;
    }

    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void Update_AnimationFinishedDestroysEntity_DoesNotThrow()
    {
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<DestroyOnAnimFinishedEntity>(screen);
        // Multiple so the loop has siblings around the destroyed renderable.
        factory.Create();
        factory.Create();
        factory.Create();

        Should.NotThrow(() => screen.Update(Frame(0.5f)));
    }

    [Fact]
    public void Update_TweenEndedDestroysEntity_DoesNotThrow()
    {
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var factory = new Factory<DestroyOnTweenEndedEntity>(screen);
        factory.Create();
        factory.Create();
        factory.Create();

        Should.NotThrow(() => screen.Update(Frame(0.5f)));
    }

    [Fact]
    public void Update_CustomActivityDestroysAnotherEntity_DoesNotThrow()
    {
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var killerFactory = new Factory<SiblingKiller>(screen);
        var targetFactory = new Factory<Entity>(screen);
        var target = targetFactory.Create();
        var killer = killerFactory.Create();
        killer.Target = target;

        Should.NotThrow(() => screen.Update(Frame(1f / 60f)));
    }

    [Fact]
    public void Update_SteadyState_AllocatesZeroBytes()
    {
        var screen = new TestScreen { Engine = new FlatRedBallService() };
        var plainFactory = new Factory<Entity>(screen);
        for (int i = 0; i < 10; i++) plainFactory.Create();

        // Add a sprite with a looping animation so the animate loop has work to do.
        var spriteFactory = new Factory<SpriteBearingEntity>(screen);
        for (int i = 0; i < 5; i++) spriteFactory.Create();

        // Warmup — JIT, first-run allocations, cache fills.
        for (int i = 0; i < 20; i++) screen.Update(Frame(1f / 60f));

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) screen.Update(Frame(1f / 60f));
        long after = GC.GetAllocatedBytesForCurrentThread();

        (after - before).ShouldBe(0,
            "Screen.Update is a per-frame hotpath — no heap allocation permitted in the loop body.");
    }

    private class SpriteBearingEntity : Entity
    {
        public override void CustomInitialize()
        {
            var sprite = new Sprite { AnimationChains = MakeLoopChain(), IsLooping = true };
            Add(sprite);
            sprite.PlayAnimation("Loop");
        }

        private static AnimationChainList MakeLoopChain()
        {
            var chain = new AnimationChain { Name = "Loop" };
            chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.1f) });
            chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(0.1f) });
            var list = new AnimationChainList();
            list.Add(chain);
            return list;
        }
    }
}
