using FlatRedBall.Glue.StateInterpolation;
using FlatRedBall2.Tweening;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tweening;

/// <summary>
/// Load-bearing test: locks in the per-curve constants (<c>tCross</c>, <c>naturalOvershoot</c>)
/// that <see cref="BumpCurveConstants"/> uses to remap a 0→1 upstream Tweener into a
/// rest-to-peak-to-rest bump. If <c>FlatRedBall.InterpolationCore</c> ever changes a curve
/// formula, this test fails loudly so we can update the table — instead of every Bump call
/// silently drifting.
/// </summary>
public class BumpConstantsTests
{
    // Sample the upstream curve at 1ms resolution over a 1-second tweener to find:
    //   tCross           = first t in [0, 1] where the value first reaches 1.0 (inclusive crossing)
    //   naturalExcursion = how far the curve travels away from 1.0 *after* tCross. For curves
    //                      that overshoot above 1 (Elastic, Back), this is max(value) - 1. For
    //                      curves that undershoot below 1 (Bounce), this is 1 - min(value).
    // Epsilon for "first time v reaches 1.0" detection. Must be loose enough to catch curves
    // (like Bounce.Out) whose segment-boundary peaks land at 0.999x rather than exactly 1.0 at
    // 1ms sampling resolution, while still tight enough that a curve still climbing toward 1
    // (like the rising portion of Elastic/Back) doesn't trigger early.
    private const float CrossingEpsilon = 0.005f;
    private static (float tCross, float naturalExcursion) Sample(InterpolationType type, Easing easing)
    {
        const float duration = 1f;
        const int steps = 1000;
        var tweener = new Tweener(0f, 1f, duration, type, easing);
        tweener.Start();
        float maxValue = float.MinValue;
        float minAfterCross = float.MaxValue;
        float tCross = -1f;
        for (int i = 1; i <= steps; i++)
        {
            tweener.Update(duration / steps);
            float v = tweener.Position;
            if (v > maxValue) maxValue = v;
            if (tCross < 0f && v >= 1f - CrossingEpsilon) tCross = i / (float)steps;
            if (tCross >= 0f && v < minAfterCross) minAfterCross = v;
        }
        float overshoot = maxValue - 1f;
        float undershoot = 1f - minAfterCross;
        // Pick the larger of the two — that's the curve's actual tail excursion direction.
        return (tCross, overshoot >= undershoot ? overshoot : undershoot);
    }

    [Fact]
    public void Constants_Elastic_MatchUpstreamSampling()
    {
        var (tCross, excursion) = Sample(InterpolationType.Elastic, Easing.Out);
        var expected = BumpCurveConstants.For(BumpCurve.Elastic);
        tCross.ShouldBe(expected.TCross, tolerance: 0.01f);
        excursion.ShouldBe(expected.NaturalExcursion, tolerance: 0.01f);
        expected.MirrorTail.ShouldBeFalse();
    }

    [Fact]
    public void Constants_Back_MatchUpstreamSampling()
    {
        var (tCross, excursion) = Sample(InterpolationType.Back, Easing.Out);
        var expected = BumpCurveConstants.For(BumpCurve.Back);
        tCross.ShouldBe(expected.TCross, tolerance: 0.01f);
        excursion.ShouldBe(expected.NaturalExcursion, tolerance: 0.01f);
        expected.MirrorTail.ShouldBeFalse();
    }

    [Fact]
    public void Constants_Bounce_TCrossAndNaturalUndershoot_MatchUpstream()
    {
        // Bounce.Out is the inverse-shaped sibling of Elastic/Back: it reaches 1.0 quickly
        // and then dips *below* 1.0 in progressively smaller bounces before resettling at 1.0.
        // We use the same pre-advance + remap strategy, but the "excursion" is downward — so
        // CreateBump applies an opposite sign to map [1, 1 - undershoot] onto
        // [restValue, restValue + amplitude].
        var (tCross, excursion) = Sample(InterpolationType.Bounce, Easing.Out);
        var expected = BumpCurveConstants.For(BumpCurve.Bounce);
        tCross.ShouldBe(expected.TCross, tolerance: 0.01f);
        excursion.ShouldBe(expected.NaturalExcursion, tolerance: 0.01f);
        expected.MirrorTail.ShouldBeTrue();
    }
}
