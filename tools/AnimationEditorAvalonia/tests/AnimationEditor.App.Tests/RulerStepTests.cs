using AnimationEditor.App.Controls;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for ruler step selection, major-tick detection, and label formatting at all zoom
/// levels, including high zoom (2000%+) and negative world coordinates.
/// </summary>
public class RulerStepTests
{
    // ── IsMajorTick ────────────────────────────────────────────────────────────

    [Fact]
    public void IsMajorTick_PositiveExactMultiple_IsTrue()
    {
        // majorStep=2, minorStep=0.4 — positive multiples of 2 must be major
        Assert.True(PreviewControl.IsMajorTick(4f, majorStep: 2f, minorStep: 0.4f));
    }

    [Fact]
    public void IsMajorTick_NegativeExactMultiple_IsTrue()
    {
        // -4 is on the 2-unit grid — must be major (fails with wx % step)
        Assert.True(PreviewControl.IsMajorTick(-4f, majorStep: 2f, minorStep: 0.4f));
    }

    [Fact]
    public void IsMajorTick_NegativeNearMiss_IsFalse()
    {
        // -3.6 is NOT on the 2-unit grid
        Assert.False(PreviewControl.IsMajorTick(-3.6f, majorStep: 2f, minorStep: 0.4f));
    }

    [Fact]
    public void IsMajorTick_NegativeWithFloatDrift_IsTrue()
    {
        // Simulate the float-accumulation scenario: wx slightly below -4
        // (the bug scenario — wx % 2 gives ≈ -2, not ≈ 0)
        Assert.True(PreviewControl.IsMajorTick(-4f + 1e-5f, majorStep: 2f, minorStep: 0.4f));
        Assert.True(PreviewControl.IsMajorTick(-4f - 1e-5f, majorStep: 2f, minorStep: 0.4f));
    }


    // ── GetRulerStep – normal zoom ────────────────────────────────────────────

    [Fact]
    public void GetRulerStep_At1xZoom_Returns50()
    {
        Assert.Equal(50f, PreviewControl.GetRulerStep(1f));
    }

    [Fact]
    public void GetRulerStep_At5xZoom_Returns10()
    {
        Assert.Equal(10f, PreviewControl.GetRulerStep(5f));
    }

    [Fact]
    public void GetRulerStep_At20xZoom_Returns5()
    {
        // targetWorld = 50/20 = 2.5 → smallest candidate ≥ 2.5 is 5
        Assert.Equal(5f, PreviewControl.GetRulerStep(20f));
    }

    [Fact]
    public void GetRulerStep_At50xZoom_Returns1()
    {
        // targetWorld = 50/50 = 1.0 → smallest candidate ≥ 1.0 is 1
        Assert.Equal(1f, PreviewControl.GetRulerStep(50f));
    }

    // ── GetRulerStep – high zoom (sub-pixel steps required) ──────────────────

    [Fact]
    public void GetRulerStep_At100xZoom_Returns0Point5()
    {
        // targetWorld = 50/100 = 0.5 → should pick 0.5 not 1
        Assert.Equal(0.5f, PreviewControl.GetRulerStep(100f));
    }

    [Fact]
    public void GetRulerStep_At200xZoom_Returns0Point25()
    {
        // targetWorld = 50/200 = 0.25 → should pick 0.25
        Assert.Equal(0.25f, PreviewControl.GetRulerStep(200f));
    }

    [Fact]
    public void GetRulerStep_At400xZoom_Returns0Point125()
    {
        // targetWorld = 50/400 = 0.125 → should pick 0.125
        Assert.Equal(0.125f, PreviewControl.GetRulerStep(400f));
    }

    [Fact]
    public void GetRulerStep_BeyondAllCandidates_ReturnsSmallestCandidate()
    {
        // targetWorld = 50/10000 = 0.005, below all candidates → return 0.125
        Assert.Equal(0.125f, PreviewControl.GetRulerStep(10000f));
    }

    // ── FormatRulerLabel ──────────────────────────────────────────────────────

    [Fact]
    public void FormatRulerLabel_WholeStep_IntegerValue_ReturnsInteger()
    {
        Assert.Equal("10", PreviewControl.FormatRulerLabel(majorStep: 5f, worldValue: 10f));
    }

    [Fact]
    public void FormatRulerLabel_WholeStep_NegativeValue_ReturnsInteger()
    {
        Assert.Equal("-25", PreviewControl.FormatRulerLabel(majorStep: 25f, worldValue: -25f));
    }

    [Fact]
    public void FormatRulerLabel_FractionalStep_HalfValue_ShowsDecimal()
    {
        Assert.Equal("0.5", PreviewControl.FormatRulerLabel(majorStep: 0.5f, worldValue: 0.5f));
    }

    [Fact]
    public void FormatRulerLabel_FractionalStep_QuarterValue_ShowsDecimal()
    {
        Assert.Equal("0.25", PreviewControl.FormatRulerLabel(majorStep: 0.25f, worldValue: 0.25f));
    }

    [Fact]
    public void FormatRulerLabel_FractionalStep_WholeValue_ShowsIntegerDigits()
    {
        // worldValue = 1.0, step = 0.5 → "1" (0.### trims trailing zeros)
        Assert.Equal("1", PreviewControl.FormatRulerLabel(majorStep: 0.5f, worldValue: 1f));
    }

    [Fact]
    public void FormatRulerLabel_FractionalStep_EighthValue_ShowsDecimal()
    {
        Assert.Equal("0.125", PreviewControl.FormatRulerLabel(majorStep: 0.125f, worldValue: 0.125f));
    }
}
