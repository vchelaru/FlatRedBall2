using System;
using FlatRedBall2.Tweening;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class EntityTweenTests
{
    private class TestEntity : Entity { }
    private class TestScreen : Screen { }

    private class PausableEntity : Entity
    {
        public bool CanAdvance { get; set; } = true;
        protected internal override bool ShouldAdvanceTweens => CanAdvance;
    }

    private static (TestScreen screen, Factory<TestEntity> factory) MakeScreenAndFactory()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<TestEntity>(screen);
        return (screen, factory);
    }

    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void Tween_AfterCall_ReturnedTweenerIsRunning()
    {
        var (_, factory) = MakeScreenAndFactory();
        var entity = factory.Create();

        var tweener = entity.Tween(_ => { }, from: 0f, to: 1f, duration: TimeSpan.FromSeconds(1));

        tweener.Running.ShouldBeTrue();
    }

    [Fact]
    public void Tween_Completes_SetterLandsOnToAndEndedFiresOnce()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float lastValue = float.NaN;
        int endedCount = 0;
        var tweener = entity.Tween(v => lastValue = v, from: 0f, to: 10f, duration: TimeSpan.FromSeconds(1));
        tweener.Ended += () => endedCount++;

        // Drive Activity well past the tween duration (5 steps of 0.3s = 1.5s).
        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));

        lastValue.ShouldBe(10f);
        endedCount.ShouldBe(1);
        entity._tweens.Count.ShouldBe(0);
    }

    [Fact]
    public void Tween_EntityDestroyed_SetterNotCalledAgainAndListCleared()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        int callCount = 0;
        entity.Tween(_ => callCount++, from: 0f, to: 1f, duration: TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));
        int callsAfterOneTick = callCount;
        entity.Destroy();
        // Entity is gone from the screen list so Screen.Update no longer drives its tweens;
        // we additionally want the tween list itself empty so nothing leaks.
        screen.Update(Frame(0.1f));

        callCount.ShouldBe(callsAfterOneTick);
        entity._tweens.Count.ShouldBe(0);
    }

    [Fact]
    public void Tween_MultipleOnSameEntity_BothAdvance()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        int aCalls = 0, bCalls = 0;
        entity.Tween(_ => aCalls++, 0f, 1f, TimeSpan.FromSeconds(1));
        entity.Tween(_ => bCalls++, 0f, 1f, TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));

        aCalls.ShouldBeGreaterThan(0);
        bCalls.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Tween_StopCalledBeforeActivity_SetterNeverCalledAndEndedNeverFires()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        int setterCalls = 0;
        int endedCount = 0;
        var tweener = entity.Tween(_ => setterCalls++, 0f, 1f, TimeSpan.FromSeconds(1));
        tweener.Ended += () => endedCount++;

        tweener.Stop();
        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.5f));

        setterCalls.ShouldBe(0);
        endedCount.ShouldBe(0);
    }

    [Fact]
    public void Tween_ShouldAdvanceTweensFalse_DoesNotAdvance_FlippingTrueResumes()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<PausableEntity>(screen);
        var entity = factory.Create();
        entity.CanAdvance = false;
        int callCount = 0;
        entity.Tween(_ => callCount++, 0f, 1f, TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));
        callCount.ShouldBe(0);

        entity.CanAdvance = true;
        screen.Update(Frame(0.1f));
        callCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Tween_DuringActivity_InvokesSetterWithInRangeValue()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float lastValue = float.NaN;
        int callCount = 0;
        entity.Tween(v => { lastValue = v; callCount++; }, from: 0f, to: 10f, duration: TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));

        callCount.ShouldBeGreaterThan(0);
        lastValue.ShouldBeInRange(0f, 10f);
    }
}
