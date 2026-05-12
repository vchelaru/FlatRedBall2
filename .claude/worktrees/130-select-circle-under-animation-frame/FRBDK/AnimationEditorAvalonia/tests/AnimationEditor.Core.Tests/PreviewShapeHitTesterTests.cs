using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Unit tests for <see cref="PreviewShapeHitTester"/> — the pure, SkiaSharp-free
/// hit-tester that backs click-to-select for circles and rectangles in the preview panel.
/// Issue #130.
/// </summary>
public class PreviewShapeHitTesterTests
{
    // ── Circle ──────────────────────────────────────────────────────────────

    [Fact]
    public void HitsCircle_ReturnsTrue_WhenClickAtCenter()
    {
        Assert.True(PreviewShapeHitTester.HitsCircle(100f, 100f, 100f, 100f, screenRadius: 20f));
    }

    [Fact]
    public void HitsCircle_ReturnsTrue_WhenClickInsideRadius()
    {
        // 15px from center, radius=20 → inside
        Assert.True(PreviewShapeHitTester.HitsCircle(115f, 100f, 100f, 100f, screenRadius: 20f));
    }

    [Fact]
    public void HitsCircle_ReturnsTrue_WhenClickWithinTolerance()
    {
        // 23px from center, radius=20, tolerance=5 → 23 ≤ 25
        Assert.True(PreviewShapeHitTester.HitsCircle(123f, 100f, 100f, 100f, screenRadius: 20f, tolerance: 5f));
    }

    [Fact]
    public void HitsCircle_ReturnsFalse_WhenClickBeyondRadiusPlusTolerance()
    {
        // 30px from center, radius=20, tolerance=5 → 30 > 25
        Assert.False(PreviewShapeHitTester.HitsCircle(130f, 100f, 100f, 100f, screenRadius: 20f, tolerance: 5f));
    }

    [Fact]
    public void HitsCircle_ReturnsFalse_WhenClickFarAway()
    {
        Assert.False(PreviewShapeHitTester.HitsCircle(300f, 300f, 100f, 100f, screenRadius: 10f));
    }

    [Fact]
    public void HitsCircle_ReturnsTrue_WhenClickOnEdgeExactly()
    {
        // 20px from center, radius=20, no tolerance override → exactly on edge
        Assert.True(PreviewShapeHitTester.HitsCircle(120f, 100f, 100f, 100f, screenRadius: 20f, tolerance: 0f));
    }

    [Fact]
    public void HitsCircle_ReturnsFalse_WhenZeroRadius_AndClickOutsideTolerance()
    {
        // radius=0, tolerance=5 → must be within 5px of center
        Assert.False(PreviewShapeHitTester.HitsCircle(106f, 100f, 100f, 100f, screenRadius: 0f, tolerance: 5f));
    }

    // ── Rect ────────────────────────────────────────────────────────────────

    [Fact]
    public void HitsRect_ReturnsTrue_WhenClickAtCenter()
    {
        Assert.True(PreviewShapeHitTester.HitsRect(100f, 100f, 100f, 100f, halfW: 30f, halfH: 20f));
    }

    [Fact]
    public void HitsRect_ReturnsTrue_WhenClickInsideBounds()
    {
        // (120, 110): 20px right, 10px down from center → within halfW=30, halfH=20
        Assert.True(PreviewShapeHitTester.HitsRect(120f, 110f, 100f, 100f, halfW: 30f, halfH: 20f));
    }

    [Fact]
    public void HitsRect_ReturnsTrue_WhenClickWithinToleranceX()
    {
        // 33px right of center, halfW=30, tolerance=5 → 33 ≤ 35
        Assert.True(PreviewShapeHitTester.HitsRect(133f, 100f, 100f, 100f, halfW: 30f, halfH: 20f, tolerance: 5f));
    }

    [Fact]
    public void HitsRect_ReturnsFalse_WhenClickBeyondToleranceX()
    {
        // 36px right of center, halfW=30, tolerance=5 → 36 > 35
        Assert.False(PreviewShapeHitTester.HitsRect(136f, 100f, 100f, 100f, halfW: 30f, halfH: 20f, tolerance: 5f));
    }

    [Fact]
    public void HitsRect_ReturnsFalse_WhenClickBeyondToleranceY()
    {
        // 26px below center, halfH=20, tolerance=5 → 26 > 25
        Assert.False(PreviewShapeHitTester.HitsRect(100f, 126f, 100f, 100f, halfW: 30f, halfH: 20f, tolerance: 5f));
    }

    [Fact]
    public void HitsRect_ReturnsTrue_WhenClickOnEdgeExactly()
    {
        // 30px right of center, halfW=30, tolerance=0 → exactly on edge
        Assert.True(PreviewShapeHitTester.HitsRect(130f, 100f, 100f, 100f, halfW: 30f, halfH: 20f, tolerance: 0f));
    }
}
