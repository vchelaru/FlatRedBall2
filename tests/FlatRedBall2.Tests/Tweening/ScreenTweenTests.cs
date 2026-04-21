using System;
using FlatRedBall2.Tweening;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class ScreenTweenTests
{
    private class TestScreen : Screen { }

    private static TestScreen MakeScreen()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        return screen;
    }

    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void Tween_Completes_SetterLandsOnToAndEndedFiresOnce()
    {
        var screen = MakeScreen();
        float lastValue = float.NaN;
        int endedCount = 0;
        var tweener = screen.Tween(v => lastValue = v, from: 0f, to: 5f, durationSeconds: 1f);
        tweener.Ended += () => endedCount++;

        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));

        lastValue.ShouldBe(5f);
        endedCount.ShouldBe(1);
        screen._tweens.Count.ShouldBe(0);
    }

    [Fact]
    public void Tween_ScreenDestroyed_TweenListCleared()
    {
        var engine = new FlatRedBallService();
        engine.Start<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime());
        var originalScreen = (TestScreen)engine.CurrentScreen;
        int callCount = 0;
        originalScreen.Tween(_ => callCount++, from: 0f, to: 1f, durationSeconds: 1f);

        originalScreen.MoveToScreen<TestScreen>();
        engine.Update(new Microsoft.Xna.Framework.GameTime()); // triggers teardown of original
        int callsAtTeardown = callCount;
        engine.Update(new Microsoft.Xna.Framework.GameTime());

        callCount.ShouldBe(callsAtTeardown);
        originalScreen._tweens.Count.ShouldBe(0);
    }

    [Fact]
    public void Tween_DuringActivity_InvokesSetter()
    {
        var screen = MakeScreen();
        int callCount = 0;
        screen.Tween(_ => callCount++, from: 0f, to: 1f, durationSeconds: 1f);

        screen.Update(Frame(0.1f));

        callCount.ShouldBeGreaterThan(0);
    }
}
