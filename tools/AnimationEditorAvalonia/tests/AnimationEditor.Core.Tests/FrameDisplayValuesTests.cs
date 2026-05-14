using AnimationEditor.Core.Rendering;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="FrameDisplayValues"/> — UV coordinate → pixel / tile-index
/// conversion helpers used by the property inspector panel.
/// </summary>
public class FrameDisplayValuesTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AnimationFrameSave Frame(float left, float right, float top, float bottom)
        => new()
        {
            LeftCoordinate   = left,
            RightCoordinate  = right,
            TopCoordinate    = top,
            BottomCoordinate = bottom
        };

    // ── GetPixelX ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetPixelX_OriginFrame_ReturnsZero()
        => Assert.Equal(0, FrameDisplayValues.GetPixelX(Frame(0f, 0.5f, 0f, 0.5f), 256));

    [Fact]
    public void GetPixelX_HalfwayLeft_ReturnsHalfTextureWidth()
        => Assert.Equal(128, FrameDisplayValues.GetPixelX(Frame(0.5f, 1f, 0f, 1f), 256));

    [Fact]
    public void GetPixelX_Rounds_NotTruncates()
        // 0.496 * 256 = 127.0 → rounds to 127; 0.502 * 256 = 128.5 → rounds to 129
        => Assert.Equal(129, FrameDisplayValues.GetPixelX(Frame(0.502f, 1f, 0f, 1f), 256));

    [Fact]
    public void GetPixelX_FullWidth_ReturnsTextureWidth()
        => Assert.Equal(512, FrameDisplayValues.GetPixelX(Frame(1f, 1f, 0f, 1f), 512));

    [Fact]
    public void GetPixelX_OddTextureWidth_RoundsCorrectly()
        => Assert.Equal(100, FrameDisplayValues.GetPixelX(Frame(0.1f, 0.5f, 0f, 1f), 1000));

    // ── GetPixelY ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetPixelY_TopEdge_ReturnsZero()
        => Assert.Equal(0, FrameDisplayValues.GetPixelY(Frame(0f, 0.5f, 0f, 0.5f), 128));

    [Fact]
    public void GetPixelY_HalfwayTop_ReturnsHalfTextureHeight()
        => Assert.Equal(64, FrameDisplayValues.GetPixelY(Frame(0f, 0.5f, 0.5f, 1f), 128));

    [Fact]
    public void GetPixelY_Rounds_NotTruncates()
        // 0.503 * 128 = 64.4 → rounds to 64
        => Assert.Equal(64, FrameDisplayValues.GetPixelY(Frame(0f, 0.5f, 0.503f, 1f), 128));

    [Fact]
    public void GetPixelY_FullHeight_ReturnsTextureHeight()
        => Assert.Equal(256, FrameDisplayValues.GetPixelY(Frame(0f, 1f, 1f, 1f), 256));

    // ── GetPixelWidth ─────────────────────────────────────────────────────────

    [Fact]
    public void GetPixelWidth_HalfUvRange_ReturnsHalfTexture()
        => Assert.Equal(128, FrameDisplayValues.GetPixelWidth(Frame(0f, 0.5f, 0f, 1f), 256));

    [Fact]
    public void GetPixelWidth_FullUvRange_ReturnsFullTextureWidth()
        => Assert.Equal(512, FrameDisplayValues.GetPixelWidth(Frame(0f, 1f, 0f, 1f), 512));

    [Fact]
    public void GetPixelWidth_ZeroRange_ReturnsMinimumOne()
        // left == right → zero width → clamped to 1
        => Assert.Equal(1, FrameDisplayValues.GetPixelWidth(Frame(0.5f, 0.5f, 0f, 1f), 256));

    [Fact]
    public void GetPixelWidth_NegativeRange_ReturnsMinimumOne()
        // right < left (malformed) → clamped to 1
        => Assert.Equal(1, FrameDisplayValues.GetPixelWidth(Frame(0.8f, 0.2f, 0f, 1f), 256));

    [Fact]
    public void GetPixelWidth_RoundsCorrectly()
        // (0.625 - 0.125) * 64 = 32.0 exactly
        => Assert.Equal(32, FrameDisplayValues.GetPixelWidth(Frame(0.125f, 0.625f, 0f, 1f), 64));

    [Fact]
    public void GetPixelWidth_SubPixelRange_RoundsUpToOne()
        // range = 0.001, texture 64px → 0.064 → rounds to 0 → clamp to 1
        => Assert.Equal(1, FrameDisplayValues.GetPixelWidth(Frame(0f, 0.001f, 0f, 1f), 64));

    // ── GetPixelHeight ────────────────────────────────────────────────────────

    [Fact]
    public void GetPixelHeight_HalfUvRange_ReturnsHalfTexture()
        => Assert.Equal(64, FrameDisplayValues.GetPixelHeight(Frame(0f, 1f, 0.25f, 0.75f), 128));

    [Fact]
    public void GetPixelHeight_FullUvRange_ReturnsFullTextureHeight()
        => Assert.Equal(256, FrameDisplayValues.GetPixelHeight(Frame(0f, 1f, 0f, 1f), 256));

    [Fact]
    public void GetPixelHeight_ZeroRange_ReturnsMinimumOne()
        => Assert.Equal(1, FrameDisplayValues.GetPixelHeight(Frame(0f, 1f, 0.5f, 0.5f), 256));

    [Fact]
    public void GetPixelHeight_NegativeRange_ReturnsMinimumOne()
        => Assert.Equal(1, FrameDisplayValues.GetPixelHeight(Frame(0f, 1f, 0.9f, 0.1f), 256));

    [Fact]
    public void GetPixelHeight_RoundsCorrectly()
        // (0.75 - 0.25) * 64 = 32.0
        => Assert.Equal(32, FrameDisplayValues.GetPixelHeight(Frame(0f, 1f, 0.25f, 0.75f), 64));

    // ── Round-trip consistency ────────────────────────────────────────────────

    [Fact]
    public void PixelXAndWidth_RoundTrip_MatchesOriginalRegion()
    {
        // A 32×32 frame starting at (64, 48) on a 256×256 texture
        var frame = Frame(64f/256, (64+32f)/256, 48f/256, (48+32f)/256);
        Assert.Equal(64,  FrameDisplayValues.GetPixelX(frame, 256));
        Assert.Equal(32,  FrameDisplayValues.GetPixelWidth(frame, 256));
        Assert.Equal(48,  FrameDisplayValues.GetPixelY(frame, 256));
        Assert.Equal(32,  FrameDisplayValues.GetPixelHeight(frame, 256));
    }

}
