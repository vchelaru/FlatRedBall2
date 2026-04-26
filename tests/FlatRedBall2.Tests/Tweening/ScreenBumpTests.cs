using System;
using FlatRedBall2.Tweening;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class ScreenBumpTests
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
    public void Bump_Completes_FinalSetterValueIsRestValue()
    {
        var screen = MakeScreen();
        float lastValue = float.NaN;
        screen.Bump(v => lastValue = v, restValue: 5f, amplitude: 2f, duration: TimeSpan.FromSeconds(1));

        for (int i = 0; i < 500 && screen._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        lastValue.ShouldBe(5f);
        screen._tweens.Count.ShouldBe(0);
    }
}
