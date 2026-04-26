using System;
using FlatRedBall2.Tweening;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class StopAllTweensTests
{
    private class TestEntity : Entity { }
    private class TestScreen : Screen { }

    private static (TestScreen screen, Factory<TestEntity> factory) MakeScreenAndFactory()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<TestEntity>(screen);
        return (screen, factory);
    }

    private static TestScreen MakeScreen()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        return screen;
    }

    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void Entity_StopAllTweens_AllowsNewTweensAfter()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        entity.Tween(_ => { }, 0f, 1f, TimeSpan.FromSeconds(1));
        entity.StopAllTweens();

        int newCalls = 0;
        entity.Tween(_ => newCalls++, 0f, 1f, TimeSpan.FromSeconds(1));
        screen.Update(Frame(0.1f));

        newCalls.ShouldBeGreaterThan(0);
        entity._tweens.Count.ShouldBe(1);
    }

    [Fact]
    public void Entity_StopAllTweens_ClearsAllPendingTweens()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        int aCalls = 0, bCalls = 0;
        entity.Tween(_ => aCalls++, 0f, 1f, TimeSpan.FromSeconds(1));
        entity.Tween(_ => bCalls++, 0f, 1f, TimeSpan.FromSeconds(1));

        entity.StopAllTweens();
        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.5f));

        aCalls.ShouldBe(0);
        bCalls.ShouldBe(0);
        entity._tweens.Count.ShouldBe(0);
    }

    [Fact]
    public void Entity_StopAllTweens_DoesNotInvokeFinalSetter()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float lastValue = float.NaN;
        entity.Tween(v => lastValue = v, from: 0f, to: 100f, duration: TimeSpan.FromSeconds(1));

        // Advance partway so the setter has fired with some intermediate value < 100.
        screen.Update(Frame(0.1f));
        lastValue.ShouldBeLessThan(100f);
        float midValue = lastValue;

        entity.StopAllTweens();
        screen.Update(Frame(0.1f));

        // Setter must not have been re-invoked (no terminal snap to 100, and no further advance).
        lastValue.ShouldBe(midValue);
    }

    [Fact]
    public void Entity_StopAllTweens_OnEmptyList_DoesNotThrow()
    {
        var (_, factory) = MakeScreenAndFactory();
        var entity = factory.Create();

        Should.NotThrow(() => entity.StopAllTweens());
    }

    [Fact]
    public void Screen_StopAllTweens_AllowsNewTweensAfter()
    {
        var screen = MakeScreen();
        screen.Tween(_ => { }, 0f, 1f, TimeSpan.FromSeconds(1));
        screen.StopAllTweens();

        int newCalls = 0;
        screen.Tween(_ => newCalls++, 0f, 1f, TimeSpan.FromSeconds(1));
        screen.Update(Frame(0.1f));

        newCalls.ShouldBeGreaterThan(0);
        screen._tweens.Count.ShouldBe(1);
    }

    [Fact]
    public void Screen_StopAllTweens_ClearsAllPendingTweens()
    {
        var screen = MakeScreen();
        int aCalls = 0, bCalls = 0;
        screen.Tween(_ => aCalls++, 0f, 1f, TimeSpan.FromSeconds(1));
        screen.Tween(_ => bCalls++, 0f, 1f, TimeSpan.FromSeconds(1));

        screen.StopAllTweens();
        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.5f));

        aCalls.ShouldBe(0);
        bCalls.ShouldBe(0);
        screen._tweens.Count.ShouldBe(0);
    }

    [Fact]
    public void Screen_StopAllTweens_DoesNotInvokeFinalSetter()
    {
        var screen = MakeScreen();
        float lastValue = float.NaN;
        screen.Tween(v => lastValue = v, from: 0f, to: 100f, duration: TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.1f));
        lastValue.ShouldBeLessThan(100f);
        float midValue = lastValue;

        screen.StopAllTweens();
        screen.Update(Frame(0.1f));

        lastValue.ShouldBe(midValue);
    }

    [Fact]
    public void Screen_StopAllTweens_OnEmptyList_DoesNotThrow()
    {
        var screen = MakeScreen();

        Should.NotThrow(() => screen.StopAllTweens());
    }
}
