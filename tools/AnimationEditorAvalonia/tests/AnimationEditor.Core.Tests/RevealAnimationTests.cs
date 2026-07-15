using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class RevealAnimationTests
{
    [Fact]
    public void Scale_GrowsThenSettles_NeverBelowActualSize()
    {
        // Grow-and-settle: the box starts large and eases monotonically down to its real size,
        // never dipping under it (no undershoot that would make it smaller than the region).
        float prev = RevealAnimation.Scale(0f);
        for (int i = 1; i <= 100; i++)
        {
            float s = RevealAnimation.Scale(i / 100f);
            Assert.True(s >= 1f - 1e-4f, $"scale dipped below final size at t={i / 100f}: {s}");
            Assert.True(s <= prev + 1e-4f, $"scale grew instead of settling at t={i / 100f}");
            prev = s;
        }
    }

    [Fact]
    public void Scale_ProgressOne_SettlesToActualSize()
    {
        Assert.Equal(1f, RevealAnimation.Scale(1f), 3);
    }

    [Fact]
    public void Scale_ProgressZero_StartsLargerThanActualSize()
    {
        Assert.True(RevealAnimation.Scale(0f) > 1f);
    }

    [Fact]
    public void StepProgress_OneTick_AdvancesTowardOneWithoutFinishing()
    {
        float next = RevealAnimation.StepProgress(0f, RevealAnimation.DefaultIntervalSeconds);
        Assert.True(next > 0f && next < 1f, $"expected progress in (0, 1), got {next}");
    }

    [Fact]
    public void StepProgress_Repeated_ReachesOne()
    {
        float p = 0f;
        for (int i = 0; i < 1000 && p < 1f; i++)
            p = RevealAnimation.StepProgress(p, RevealAnimation.DefaultIntervalSeconds);
        Assert.Equal(1f, p);
    }

    // ── InflationPixels: screen-space pixel growth (#716 follow-up) ────────────

    [Fact]
    public void InflationPixels_GrowsThenSettles_NeverBelowZero()
    {
        // Same grow-and-settle shape as Scale, but expressed as a fixed pixel amount so the
        // pop stays visible regardless of the viewport's current zoom (a box that's tiny
        // on-screen when zoomed out still gets the same absolute-pixel bump).
        float prev = RevealAnimation.InflationPixels(0f);
        for (int i = 1; i <= 100; i++)
        {
            float px = RevealAnimation.InflationPixels(i / 100f);
            Assert.True(px >= -1e-4f, $"inflation went negative at t={i / 100f}: {px}");
            Assert.True(px <= prev + 1e-4f, $"inflation grew instead of settling at t={i / 100f}");
            prev = px;
        }
    }

    [Fact]
    public void InflationPixels_ProgressOne_SettlesToZero()
    {
        Assert.Equal(0f, RevealAnimation.InflationPixels(1f), 3);
    }

    [Fact]
    public void InflationPixels_ProgressZero_StartsAtMaxPixels()
    {
        Assert.Equal(RevealAnimation.StartInflationPixels, RevealAnimation.InflationPixels(0f), 3);
    }

    // ── HandleAlpha: resize-handle fade overlapping the tail of the shrink (#716 follow-up) ────

    [Fact]
    public void HandleAlpha_BeforeStartFraction_IsZero()
    {
        // easeOutCubic decelerates hard near t=1, so InflationPixels is already visually
        // negligible well before progress reaches 1 -- HandleAlpha starts its own ramp partway
        // through the *same* progress timeline instead of waiting for full settle, so the fade
        // finishes together with the shrink instead of after a perceptible dead pause.
        Assert.Equal(0f, RevealAnimation.HandleAlpha(0f));
        Assert.Equal(0f, RevealAnimation.HandleAlpha(RevealAnimation.HandleFadeStartFraction - 0.05f));
    }

    [Fact]
    public void HandleAlpha_AtProgressOne_IsFullyOpaque()
    {
        Assert.Equal(1f, RevealAnimation.HandleAlpha(1f), 3);
    }

    [Fact]
    public void HandleAlpha_RampsMonotonicallyOverTailFraction()
    {
        float prev = RevealAnimation.HandleAlpha(RevealAnimation.HandleFadeStartFraction);
        Assert.Equal(0f, prev, 3);
        for (float t = RevealAnimation.HandleFadeStartFraction; t <= 1f; t += 0.05f)
        {
            float a = RevealAnimation.HandleAlpha(t);
            Assert.True(a >= prev - 1e-4f, $"alpha decreased at t={t}: {a} < {prev}");
            Assert.True(a is >= 0f and <= 1f, $"alpha out of [0,1] at t={t}: {a}");
            prev = a;
        }
    }
}
