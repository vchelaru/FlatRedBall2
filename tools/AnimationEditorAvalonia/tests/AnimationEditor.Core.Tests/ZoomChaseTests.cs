using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="ZoomChase"/> — the pure, frame-rate-independent easing that
/// drives smooth (animated) mouse-wheel zoom (#425). No UI, no timer.
/// </summary>
public class ZoomChaseTests
{
    [Fact]
    public void IsSettled_FarApart_ReturnsFalse()
    {
        Assert.False(ZoomChase.IsSettled(1.0f, 1.5f));
    }

    [Fact]
    public void IsSettled_WithinThreshold_ReturnsTrue()
    {
        // 1.4995 is within SnapRelativeThreshold (0.15 %) of 1.5.
        Assert.True(ZoomChase.IsSettled(1.4995f, 1.5f));
    }

    [Fact]
    public void Step_CurrentAboveTarget_MovesDownTowardTarget()
    {
        float next = ZoomChase.Step(2.0f, 1.5f, 0.016f);
        Assert.True(next < 2.0f && next > 1.5f, $"expected value in (1.5, 2.0), got {next}");
    }

    [Fact]
    public void Step_CurrentBelowTarget_MovesUpTowardTargetWithoutOvershooting()
    {
        float next = ZoomChase.Step(1.0f, 1.5f, 0.016f);
        Assert.True(next > 1.0f && next < 1.5f, $"expected value in (1.0, 1.5), got {next}");
    }

    [Fact]
    public void Step_FrameRateIndependent_OneStepEqualsTwoHalfSteps()
    {
        // Exponential smoothing is exact under timestep splitting: a single 32 ms step
        // must equal two consecutive 16 ms steps (so a dropped frame doesn't change the curve).
        float oneStep = ZoomChase.Step(1.0f, 1.5f, 0.032f);
        float twoHalf = ZoomChase.Step(ZoomChase.Step(1.0f, 1.5f, 0.016f), 1.5f, 0.016f);
        Assert.Equal(oneStep, twoHalf, precision: 4);
    }

    [Fact]
    public void Step_RepeatedApplication_ConvergesExactlyAndTerminates()
    {
        // Chasing 1.0 → 2.0 at 60 fps must settle on the exact target within ~1 s of ticks,
        // not dribble forever down the asymptotic tail.
        float z = 1.0f;
        int iterations = 0;
        while (!ZoomChase.IsSettled(z, 2.0f) && iterations < 1000)
        {
            z = ZoomChase.Step(z, 2.0f, 0.016f);
            iterations++;
        }
        Assert.Equal(2.0f, z);
        Assert.True(iterations < 60, $"chase should settle within ~1 s of 60 fps ticks; took {iterations}");
    }

    [Fact]
    public void Step_WithinSnapThreshold_ReturnsTargetExactly()
    {
        Assert.Equal(1.5f, ZoomChase.Step(1.4995f, 1.5f, 0.016f));
    }
}
