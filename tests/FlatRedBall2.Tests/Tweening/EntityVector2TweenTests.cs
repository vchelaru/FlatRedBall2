using System;
using FlatRedBall2.Tweening;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class EntityVector2TweenTests
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

    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void Tween_Vector2_Completes_SetterLandsOnToExactly()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Vector2 last = Vector2.Zero;
        var from = new Vector2(0f, 0f);
        var to = new Vector2(10f, 20f);
        entity.Tween(v => last = v, from, to, TimeSpan.FromSeconds(1));

        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));

        last.ShouldBe(to);
    }

    [Fact]
    public void Tween_Vector2_Midway_InterpolatesBothComponents()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Vector2 last = Vector2.Zero;
        var from = new Vector2(0f, 0f);
        var to = new Vector2(10f, 20f);
        // Linear easing so the midway tick is predictably between from and to.
        entity.Tween(v => last = v, from, to, TimeSpan.FromSeconds(1),
            FlatRedBall.Glue.StateInterpolation.InterpolationType.Linear,
            FlatRedBall.Glue.StateInterpolation.Easing.InOut);

        screen.Update(Frame(0.5f));

        last.X.ShouldBeInRange(0.01f, 9.99f);
        last.Y.ShouldBeInRange(0.01f, 19.99f);
        // Y should advance proportionally to X (linear, same t).
        (last.Y / last.X).ShouldBe(2f, tolerance: 0.001f);
    }

    [Fact]
    public void Tween_Vector2_NullSetter_Throws()
    {
        var (_, factory) = MakeScreenAndFactory();
        var entity = factory.Create();

        Should.Throw<ArgumentNullException>(() =>
            entity.Tween((Action<Vector2>)null, Vector2.Zero, Vector2.One, TimeSpan.FromSeconds(1)));
    }
}
