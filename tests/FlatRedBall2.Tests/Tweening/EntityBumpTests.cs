using System;
using System.Collections.Generic;
using FlatRedBall2.Tweening;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

public class EntityBumpTests
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
    public void Bump_AllSupportedCurves_RunEndToEndAndSettleAtRest()
    {
        foreach (var curve in new[] { BumpCurve.Elastic, BumpCurve.Back, BumpCurve.Bounce })
        {
            var (screen, factory) = MakeScreenAndFactory();
            var entity = factory.Create();
            float lastValue = float.NaN;
            float maxSeen = float.MinValue;
            entity.Bump(v => { lastValue = v; if (v > maxSeen) maxSeen = v; },
                restValue: 100f, amplitude: 10f, duration: TimeSpan.FromSeconds(1), curve: curve);

            for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
                screen.Update(Frame(0.005f));

            lastValue.ShouldBe(100f);
            maxSeen.ShouldBe(110f, tolerance: 1f);
        }
    }

    [Fact]
    public void Bump_AmplitudeIndependentOfRestValue_PeakDeltaSame()
    {
        var (screen1, factory1) = MakeScreenAndFactory();
        var (screen2, factory2) = MakeScreenAndFactory();
        var e1 = factory1.Create();
        var e2 = factory2.Create();
        float maxA = float.MinValue, maxB = float.MinValue;

        e1.Bump(v => { if (v > maxA) maxA = v; }, restValue: 100f, amplitude: 10f, duration: TimeSpan.FromSeconds(1));
        e2.Bump(v => { if (v > maxB) maxB = v; }, restValue: 1000f, amplitude: 10f, duration: TimeSpan.FromSeconds(1));

        for (int i = 0; i < 500; i++)
        {
            screen1.Update(Frame(0.005f));
            screen2.Update(Frame(0.005f));
        }

        (maxA - 100f).ShouldBe(maxB - 1000f, tolerance: 0.5f);
        (maxA - 100f).ShouldBe(10f, tolerance: 0.5f);
    }

    [Fact]
    public void Bump_BackCurve_HasExactlyOneLocalMaximum()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        var samples = new List<float>();
        entity.Bump(v => samples.Add(v), restValue: 0f, amplitude: 10f,
            duration: TimeSpan.FromSeconds(1), curve: BumpCurve.Back);

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        // Count local maxima: a sample is a local max if both neighbors are strictly less.
        // Back is single-overshoot, so we expect exactly one such peak.
        int localMaxes = 0;
        for (int i = 1; i < samples.Count - 1; i++)
            if (samples[i] > samples[i - 1] && samples[i] > samples[i + 1])
                localMaxes++;
        localMaxes.ShouldBe(1);
    }

    [Fact]
    public void Bump_BounceCurve_NeverGoesBelowRest()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float minSeen = float.MaxValue;
        entity.Bump(v => { if (v < minSeen) minSeen = v; },
            restValue: 100f, amplitude: 10f,
            duration: TimeSpan.FromSeconds(1), curve: BumpCurve.Bounce);

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        // Bounce.Out's tail oscillates between 1.0 and lower values; after sign-flipping for
        // positive amplitude, the visible value oscillates between rest and rest+amplitude — never
        // below rest. Small float epsilon allowed for sampling drift.
        minSeen.ShouldBeGreaterThanOrEqualTo(100f - 0.01f);
    }

    [Fact]
    public void Bump_BounceCurve_PeaksAtAmplitude()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float maxSeen = float.MinValue;
        entity.Bump(v => { if (v > maxSeen) maxSeen = v; },
            restValue: 100f, amplitude: 10f,
            duration: TimeSpan.FromSeconds(1), curve: BumpCurve.Bounce);

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        maxSeen.ShouldBe(110f, tolerance: 1f);
    }

    [Fact]
    public void Bump_BounceCurve_SettlesAtRest()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float lastValue = float.NaN;
        entity.Bump(v => lastValue = v, restValue: 100f, amplitude: 10f,
            duration: TimeSpan.FromSeconds(1), curve: BumpCurve.Bounce);

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        lastValue.ShouldBe(100f);
    }

    [Fact]
    public void Bump_BounceCurve_StartsAtRest()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float firstValue = float.NaN;
        entity.Bump(v => { if (float.IsNaN(firstValue)) firstValue = v; },
            restValue: 100f, amplitude: 10f,
            duration: TimeSpan.FromSeconds(1), curve: BumpCurve.Bounce);

        screen.Update(Frame(0.001f));

        // Pre-advance lands the raw tweener at exactly t=tCross (raw≈1), so the first visible
        // setter call should be very close to restValue — NOT restValue + amplitude.
        firstValue.ShouldBe(100f, tolerance: 0.5f);
    }

    [Fact]
    public void Bump_Completes_FinalSetterValueIsRestValue()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float lastValue = float.NaN;
        entity.Bump(v => lastValue = v, restValue: 100f, amplitude: 10f, duration: TimeSpan.FromSeconds(1));

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        lastValue.ShouldBe(100f);
        entity._tweens.Count.ShouldBe(0);
    }

    [Fact]
    public void Bump_FirstFrame_SetterValueIsRestValue()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float firstValue = float.NaN;
        entity.Bump(v => { if (float.IsNaN(firstValue)) firstValue = v; },
            restValue: 100f, amplitude: 10f, duration: TimeSpan.FromSeconds(1));

        screen.Update(Frame(0.001f));

        // The pre-advance leaves the raw tweener at exactly t=tCross (raw≈1), so the first
        // visible setter call should be very close to restValue.
        firstValue.ShouldBe(100f, tolerance: 0.5f);
    }

    [Fact]
    public void Bump_NegativeAmplitude_Back_KicksBelowRest()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float minSeen = float.MaxValue;
        float maxSeen = float.MinValue;
        float lastValue = float.NaN;
        entity.Bump(v => { lastValue = v; if (v < minSeen) minSeen = v; if (v > maxSeen) maxSeen = v; },
            restValue: 100f, amplitude: -10f,
            duration: TimeSpan.FromSeconds(1), curve: BumpCurve.Back);

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        minSeen.ShouldBe(90f, tolerance: 1f);
        // Back has a single excursion; for negative amplitude the value should stay at or below rest.
        maxSeen.ShouldBe(100f, tolerance: 0.5f);
        lastValue.ShouldBe(100f);
    }

    [Fact]
    public void Bump_NegativeAmplitude_Bounce_BouncesBelowRest()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float minSeen = float.MaxValue;
        float maxSeen = float.MinValue;
        float lastValue = float.NaN;
        entity.Bump(v => { lastValue = v; if (v < minSeen) minSeen = v; if (v > maxSeen) maxSeen = v; },
            restValue: 100f, amplitude: -10f,
            duration: TimeSpan.FromSeconds(1), curve: BumpCurve.Bounce);

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        minSeen.ShouldBe(90f, tolerance: 1f);
        // Bounce + negative amplitude: visible value oscillates between rest and rest+amplitude (below).
        // It should never go above rest.
        maxSeen.ShouldBeLessThanOrEqualTo(100f + 0.01f);
        lastValue.ShouldBe(100f);
    }

    [Fact]
    public void Bump_NegativeAmplitude_Elastic_DipsBelowRestFirst()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float minSeen = float.MaxValue;
        float lastValue = float.NaN;
        entity.Bump(v => { lastValue = v; if (v < minSeen) minSeen = v; },
            restValue: 100f, amplitude: -10f,
            duration: TimeSpan.FromSeconds(1), curve: BumpCurve.Elastic);

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        // The dip (peak of the negative-amplitude bump) reaches restValue + amplitude == 90.
        minSeen.ShouldBe(90f, tolerance: 1f);
        lastValue.ShouldBe(100f);
    }

    [Fact]
    public void Bump_Peak_ApproximatesRestPlusAmplitude()
    {
        var (screen, factory) = MakeScreenAndFactory();
        var entity = factory.Create();
        float maxSeen = float.MinValue;
        entity.Bump(v => { if (v > maxSeen) maxSeen = v; },
            restValue: 50f, amplitude: 20f, duration: TimeSpan.FromSeconds(1));

        for (int i = 0; i < 500 && entity._tweens.Count > 0; i++)
            screen.Update(Frame(0.005f));

        // Peak should equal restValue + amplitude within sampling tolerance (~5%).
        maxSeen.ShouldBe(70f, tolerance: 1f);
    }
}
