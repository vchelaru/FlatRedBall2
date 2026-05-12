using AnimationEditor.Core.Rendering;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class PlainClickFrameRegionCalculatorTests
{
    // ── Bitmap-wide constraints ───────────────────────────────────────────────

    [Fact]
    public void BitmapSmall_LastFrameLarger_ClampsToBitmapBounds()
    {
        // 20×20 bitmap, 32×32 last frame, click at (10,10)
        // w=32, h=32; cx=10, cy=10
        // minX=max(0, 10-16)=0; maxX=min(20, 0+32)=20
        var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(10, 10, 20, 20, 32, 32);
        Assert.Equal(0, minX);
        Assert.Equal(0, minY);
        Assert.Equal(20, maxX);
        Assert.Equal(20, maxY);
    }

    // ── Clamping at edges ─────────────────────────────────────────────────────

    [Fact]
    public void ClickAtOrigin_FrameStartsAtZero()
    {
        var (minX, minY, _, _) = PlainClickFrameRegionCalculator.Compute(0, 0, 64, 64);
        Assert.Equal(0, minX);
        Assert.Equal(0, minY);
    }

    [Fact]
    public void ClickNearBottomRight_ClampsToBitmapBounds()
    {
        // cx=62, halfW=8 → minX=max(0, 54)=54; maxX=min(64, 70)=64
        var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(62, 62, 64, 64);
        Assert.Equal(54, minX);
        Assert.Equal(54, minY);
        Assert.Equal(64, maxX);
        Assert.Equal(64, maxY);
    }

    [Fact]
    public void ClickNearTopLeft_ClampsToZero()
    {
        // cx=4, halfW=8 → minX=max(0, -4)=0; maxX=min(64, 0+16)=16
        var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(4, 4, 64, 64);
        Assert.Equal(0, minX);
        Assert.Equal(0, minY);
        Assert.Equal(16, maxX);
        Assert.Equal(16, maxY);
    }

    // ── Last-frame dimensions ─────────────────────────────────────────────────

    [Fact]
    public void LastFrame32x32_ClickAtCenter_Returns32x32Centered()
    {
        var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(32, 32, 64, 64, 32, 32);
        Assert.Equal(16, minX);
        Assert.Equal(16, minY);
        Assert.Equal(48, maxX);
        Assert.Equal(48, maxY);
    }

    [Fact]
    public void LastFrameSmallerThan16_Uses16()
    {
        var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(32, 32, 64, 64, 8, 8);
        Assert.Equal(24, minX);
        Assert.Equal(24, minY);
        Assert.Equal(40, maxX);
        Assert.Equal(40, maxY);
    }

    [Fact]
    public void LastFrameWidthLarger_HeightSmaller_MixedDimensions()
    {
        // lastFrameW=32 > 16, lastFrameH=8 < 16 → size = 32×16
        // cx=32, cy=32
        // minX = max(0, 32 - 32/2) = 16;  maxX = min(64, 16 + 32) = 48
        // minY = max(0, 32 - 16/2) = 24;  maxY = min(64, 24 + 16) = 40
        var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(32, 32, 64, 64, 32, 8);
        Assert.Equal(16, minX);
        Assert.Equal(24, minY);
        Assert.Equal(48, maxX);
        Assert.Equal(40, maxY);
    }

    // ── Default size (no last frame) ──────────────────────────────────────────

    [Fact]
    public void NoLastFrame_ClickAtCenter_Returns16x16Centered()
    {
        var (minX, minY, maxX, maxY) = PlainClickFrameRegionCalculator.Compute(32, 32, 64, 64);
        Assert.Equal(24, minX);
        Assert.Equal(24, minY);
        Assert.Equal(40, maxX);
        Assert.Equal(40, maxY);
    }

    [Fact]
    public void NoLastFrame_HeightIs16()
    {
        var (_, minY, _, maxY) = PlainClickFrameRegionCalculator.Compute(32, 32, 64, 64);
        Assert.Equal(16, maxY - minY);
    }

    [Fact]
    public void NoLastFrame_WidthIs16()
    {
        var (minX, _, maxX, _) = PlainClickFrameRegionCalculator.Compute(32, 32, 64, 64);
        Assert.Equal(16, maxX - minX);
    }
}
