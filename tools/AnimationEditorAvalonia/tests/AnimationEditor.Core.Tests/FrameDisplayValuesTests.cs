using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Tests for <see cref="FrameDisplayValues"/> — a frame's region as the whole
/// pixels shown by the property inspector panel.
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
        => Assert.Equal(0, FrameDisplayValues.GetPixelX(Frame(0f, 128f, 0f, 128f)));

    [Fact]
    public void GetPixelX_HalfwayLeft_ReturnsHalfTextureWidth()
        => Assert.Equal(128, FrameDisplayValues.GetPixelX(Frame(128f, 256f, 0f, 256f)));

    [Fact]
    public void GetPixelX_Rounds_NotTruncates()
        // 128.512 → rounds to 129
        => Assert.Equal(129, FrameDisplayValues.GetPixelX(Frame(128.512f, 256f, 0f, 256f)));

    [Fact]
    public void GetPixelX_FullWidth_ReturnsTextureWidth()
        => Assert.Equal(512, FrameDisplayValues.GetPixelX(Frame(512f, 512f, 0f, 512f)));

    [Fact]
    public void GetPixelX_OddTextureWidth_RoundsCorrectly()
        => Assert.Equal(100, FrameDisplayValues.GetPixelX(Frame(100f, 500f, 0f, 1000f)));

    // ── GetPixelY ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetPixelY_TopEdge_ReturnsZero()
        => Assert.Equal(0, FrameDisplayValues.GetPixelY(Frame(0f, 64f, 0f, 64f)));

    [Fact]
    public void GetPixelY_HalfwayTop_ReturnsHalfTextureHeight()
        => Assert.Equal(64, FrameDisplayValues.GetPixelY(Frame(0f, 64f, 64f, 128f)));

    [Fact]
    public void GetPixelY_Rounds_NotTruncates()
        // 64.384 → rounds to 64
        => Assert.Equal(64, FrameDisplayValues.GetPixelY(Frame(0f, 64f, 64.384f, 128f)));

    [Fact]
    public void GetPixelY_FullHeight_ReturnsTextureHeight()
        => Assert.Equal(256, FrameDisplayValues.GetPixelY(Frame(0f, 256f, 256f, 256f)));

    // ── GetPixelWidth ─────────────────────────────────────────────────────────

    [Fact]
    public void GetPixelWidth_HalfUvRange_ReturnsHalfTexture()
        => Assert.Equal(128, FrameDisplayValues.GetPixelWidth(Frame(0f, 128f, 0f, 256f)));

    [Fact]
    public void GetPixelWidth_FullUvRange_ReturnsFullTextureWidth()
        => Assert.Equal(512, FrameDisplayValues.GetPixelWidth(Frame(0f, 512f, 0f, 512f)));

    [Fact]
    public void GetPixelWidth_ZeroRange_ReturnsMinimumOne()
        // left == right → zero width → clamped to 1
        => Assert.Equal(1, FrameDisplayValues.GetPixelWidth(Frame(128f, 128f, 0f, 256f)));

    [Fact]
    public void GetPixelWidth_NegativeRange_ReturnsMinimumOne()
        // right < left (malformed) → clamped to 1
        => Assert.Equal(1, FrameDisplayValues.GetPixelWidth(Frame(204.8f, 51.2f, 0f, 256f)));

    [Fact]
    public void GetPixelWidth_RoundsCorrectly()
        // 40 - 8 = 32
        => Assert.Equal(32, FrameDisplayValues.GetPixelWidth(Frame(8f, 40f, 0f, 64f)));

    [Fact]
    public void GetPixelWidth_SubPixelRange_RoundsUpToOne()
        // 0.064 → rounds to 0 → clamp to 1
        => Assert.Equal(1, FrameDisplayValues.GetPixelWidth(Frame(0f, 0.064f, 0f, 64f)));

    // ── GetPixelHeight ────────────────────────────────────────────────────────

    [Fact]
    public void GetPixelHeight_HalfUvRange_ReturnsHalfTexture()
        => Assert.Equal(64, FrameDisplayValues.GetPixelHeight(Frame(0f, 128f, 32f, 96f)));

    [Fact]
    public void GetPixelHeight_FullUvRange_ReturnsFullTextureHeight()
        => Assert.Equal(256, FrameDisplayValues.GetPixelHeight(Frame(0f, 256f, 0f, 256f)));

    [Fact]
    public void GetPixelHeight_ZeroRange_ReturnsMinimumOne()
        => Assert.Equal(1, FrameDisplayValues.GetPixelHeight(Frame(0f, 256f, 128f, 128f)));

    [Fact]
    public void GetPixelHeight_NegativeRange_ReturnsMinimumOne()
        => Assert.Equal(1, FrameDisplayValues.GetPixelHeight(Frame(0f, 256f, 230.4f, 25.6f)));

    [Fact]
    public void GetPixelHeight_RoundsCorrectly()
        // 48 - 16 = 32
        => Assert.Equal(32, FrameDisplayValues.GetPixelHeight(Frame(0f, 64f, 16f, 48f)));

    // ── Round-trip consistency ────────────────────────────────────────────────

    [Fact]
    public void PixelXAndWidth_RoundTrip_MatchesOriginalRegion()
    {
        // A 32×32 frame starting at (64, 48)
        var frame = Frame(64f, 64+32f, 48f, 48+32f);
        Assert.Equal(64,  FrameDisplayValues.GetPixelX(frame));
        Assert.Equal(32,  FrameDisplayValues.GetPixelWidth(frame));
        Assert.Equal(48,  FrameDisplayValues.GetPixelY(frame));
        Assert.Equal(32,  FrameDisplayValues.GetPixelHeight(frame));
    }

}
