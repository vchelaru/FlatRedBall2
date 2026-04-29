using System;
using FlatRedBall2.Rendering;
using FlatRedBall2.Tweening;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class EntityColorTweenTests
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
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    [Fact]
    public void Tween_ColorRgb_Completes_SetterLandsOnToExactly()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Color last = Color.Transparent;
        var to = new Color(50, 100, 150, 200);
        entity.Tween(c => last = c, Color.Black, to, TimeSpan.FromSeconds(1));

        for (int i = 0; i < 5; i++)
            screen.Update(Frame(0.3f));

        last.ShouldBe(to);
    }

    [Fact]
    public void Tween_ColorRgb_Midway_BlendsEachChannel()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Color last = Color.Black;
        // Black -> White at t=0.5 should yield a mid-gray on every channel.
        entity.Tween(c => last = c, Color.Black, Color.White, TimeSpan.FromSeconds(1),
            ColorTweenMode.Rgb,
            FlatRedBall.Glue.StateInterpolation.InterpolationType.Linear,
            FlatRedBall.Glue.StateInterpolation.Easing.InOut);

        screen.Update(Frame(0.5f));

        ((int)last.R).ShouldBeInRange(50, 205);
        last.G.ShouldBe(last.R);
        last.B.ShouldBe(last.R);
    }

    [Fact]
    public void Tween_ColorHsv_RedToBlue_MidpointPassesThroughMagenta()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Color last = Color.Black;
        // Red (hue 0) -> Blue (hue 240). Shortest arc goes via magenta (hue ~300, B>G),
        // not via green (hue ~120, G>B).
        entity.Tween(c => last = c, Color.Red, Color.Blue, TimeSpan.FromSeconds(1),
            ColorTweenMode.Hsv,
            FlatRedBall.Glue.StateInterpolation.InterpolationType.Linear,
            FlatRedBall.Glue.StateInterpolation.Easing.InOut);

        screen.Update(Frame(0.5f));

        last.B.ShouldBeGreaterThan(last.G);
    }

    [Fact]
    public void Tween_ColorHsv_HueWrapAround_MidpointStaysNearRed()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Color last = Color.Black;
        // Hue 350 -> hue 10. Shortest arc crosses 0 (red), not the long way around through cyan (180).
        var fromColor = Colors.FromHsv(350f, 1f, 1f);
        var toColor = Colors.FromHsv(10f, 1f, 1f);
        entity.Tween(c => last = c, fromColor, toColor, TimeSpan.FromSeconds(1),
            ColorTweenMode.Hsv,
            FlatRedBall.Glue.StateInterpolation.InterpolationType.Linear,
            FlatRedBall.Glue.StateInterpolation.Easing.InOut);

        screen.Update(Frame(0.5f));

        last.R.ShouldBeGreaterThan(last.G);
        last.R.ShouldBeGreaterThan(last.B);
    }

    [Fact]
    public void Tween_ColorHsv_AlphaInterpolatesLinearly()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        Color last = Color.Black;
        var from = new Color((byte)255, (byte)0, (byte)0, (byte)0);
        var to = new Color((byte)255, (byte)0, (byte)0, (byte)255);
        entity.Tween(c => last = c, from, to, TimeSpan.FromSeconds(1),
            ColorTweenMode.Hsv,
            FlatRedBall.Glue.StateInterpolation.InterpolationType.Linear,
            FlatRedBall.Glue.StateInterpolation.Easing.InOut);

        screen.Update(Frame(0.5f));

        ((int)last.A).ShouldBeInRange(100, 155);
    }

    [Fact]
    public void Tween_Color_NullSetter_Throws()
    {
        var (_, factory) = MakeScreenAndFactory();
        var entity = factory.Create();

        Should.Throw<ArgumentNullException>(() =>
            entity.Tween((Action<Color>)null, Color.Black, Color.White, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void ToHsv_RoundTripThroughFromHsv_PreservesColor()
    {
        var colors = new[]
        {
            new Color(255, 0, 0),
            new Color(0, 255, 0),
            new Color(0, 0, 255),
            new Color(128, 64, 200),
            new Color(50, 50, 50),
        };

        foreach (var c in colors)
        {
            var (h, s, v) = c.ToHsv();
            var roundTrip = Colors.FromHsv(h, s, v);
            // Allow +/- 1 byte for rounding through the float pipeline.
            ((int)System.Math.Abs(roundTrip.R - c.R)).ShouldBeLessThanOrEqualTo(1);
            ((int)System.Math.Abs(roundTrip.G - c.G)).ShouldBeLessThanOrEqualTo(1);
            ((int)System.Math.Abs(roundTrip.B - c.B)).ShouldBeLessThanOrEqualTo(1);
        }
    }
}
