using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class GridFadeCalculatorTests
{
    [Fact]
    public void MinorLineAlphaFactor_AtOrAboveFadeStartZoom_ReturnsOne()
    {
        Assert.Equal(1f, GridFadeCalculator.MinorLineAlphaFactor(GridFadeCalculator.FadeStartZoom));
        Assert.Equal(1f, GridFadeCalculator.MinorLineAlphaFactor(1f));
        Assert.Equal(1f, GridFadeCalculator.MinorLineAlphaFactor(4f));
    }

    [Fact]
    public void MinorLineAlphaFactor_AtOrBelowFadeEndZoom_ReturnsZero()
    {
        Assert.Equal(0f, GridFadeCalculator.MinorLineAlphaFactor(GridFadeCalculator.FadeEndZoom));
        Assert.Equal(0f, GridFadeCalculator.MinorLineAlphaFactor(0.1f));
        Assert.Equal(0f, GridFadeCalculator.MinorLineAlphaFactor(0f));
    }

    [Fact]
    public void MinorLineAlphaFactor_BetweenThresholds_InterpolatesLinearly()
    {
        // Midpoint between FadeEndZoom and FadeStartZoom → factor 0.5.
        float midZoom = (GridFadeCalculator.FadeStartZoom + GridFadeCalculator.FadeEndZoom) / 2f;
        Assert.Equal(0.5f, GridFadeCalculator.MinorLineAlphaFactor(midZoom), precision: 4);
    }

    /// <summary>
    /// The fine grid must be fully invisible AT 50% zoom, not just below it — 0.5 is
    /// inclusive in the suppressed range.
    /// </summary>
    [Fact]
    public void MinorLineAlphaFactor_AtHalfZoom_IsFullySuppressed()
    {
        Assert.Equal(0.5f, GridFadeCalculator.FadeEndZoom);
        Assert.Equal(0f, GridFadeCalculator.MinorLineAlphaFactor(0.5f));
    }

    // ── Major-line thinning ──────────────────────────────────────────────────

    [Fact]
    public void IsMajorLineVisible_AboveMajorThinZoom_AllMajorLinesVisible()
    {
        Assert.True(GridFadeCalculator.IsMajorLineVisible(0, 1f));
        Assert.True(GridFadeCalculator.IsMajorLineVisible(1, 1f));
        Assert.True(GridFadeCalculator.IsMajorLineVisible(2, 0.3f));
        Assert.True(GridFadeCalculator.IsMajorLineVisible(3, 0.3f));
    }

    /// <summary>
    /// At/below MajorThinZoom (0.25, inclusive) only every other major line renders —
    /// the origin (index 0) and even indices stay visible, odd indices are hidden.
    /// </summary>
    [Fact]
    public void IsMajorLineVisible_AtOrBelowMajorThinZoom_OnlyEvenIndicesVisible()
    {
        Assert.True(GridFadeCalculator.IsMajorLineVisible(0, GridFadeCalculator.MajorThinZoom));
        Assert.False(GridFadeCalculator.IsMajorLineVisible(1, GridFadeCalculator.MajorThinZoom));
        Assert.True(GridFadeCalculator.IsMajorLineVisible(2, GridFadeCalculator.MajorThinZoom));
        Assert.False(GridFadeCalculator.IsMajorLineVisible(3, GridFadeCalculator.MajorThinZoom));

        Assert.True(GridFadeCalculator.IsMajorLineVisible(0, 0.1f));
        Assert.False(GridFadeCalculator.IsMajorLineVisible(1, 0.1f));
    }

    [Fact]
    public void IsMajorLineVisible_NegativeIndices_FoldCorrectly()
    {
        Assert.True(GridFadeCalculator.IsMajorLineVisible(-2, GridFadeCalculator.MajorThinZoom));
        Assert.False(GridFadeCalculator.IsMajorLineVisible(-1, GridFadeCalculator.MajorThinZoom));
    }
}
