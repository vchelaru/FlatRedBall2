using System;
using FlatRedBall2.Tweening;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class ScreenVector2AndColorTweenTests
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
    public void Tween_Vector2_Completes_SetterLandsOnToExactly()
    {
        var screen = MakeScreen();
        Vector2 last = Vector2.Zero;
        var to = new Vector2(7f, -3f);
        screen.Tween(v => last = v, Vector2.Zero, to, TimeSpan.FromSeconds(1));

        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));

        last.ShouldBe(to);
    }

    [Fact]
    public void Tween_Color_Completes_SetterLandsOnToExactly()
    {
        var screen = MakeScreen();
        Color last = Color.Transparent;
        var to = new Color(10, 20, 30, 40);
        screen.Tween(c => last = c, Color.Black, to, TimeSpan.FromSeconds(1));

        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));

        last.ShouldBe(to);
    }
}
