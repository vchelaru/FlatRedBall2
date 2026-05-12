using AnimationEditor.Core.Rendering;
using FlatRedBall.Content.AnimationChain;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class PixelFrameEditorTests
{
    // ── Helper ───────────────────────────────────────────────────────────────

    static AnimationFrameSave MakeFrame(float left, float right, float top, float bottom)
        => new AnimationFrameSave
        {
            LeftCoordinate   = left,
            RightCoordinate  = right,
            TopCoordinate    = top,
            BottomCoordinate = bottom
        };

    // ── SetX ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SetX_MovesLeftAndRightByDelta_PreservesWidth()
    {
        // Frame starts at pixel 16–48 on a 256px texture  (width = 32px)
        var frame = MakeFrame(16f / 256f, 48f / 256f, 0f, 1f);

        PixelFrameEditor.SetX(frame, 32, 256);   // move to pixel 32

        Assert.Equal(32f / 256f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(64f / 256f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_SamePosition_NoChange()
    {
        var frame = MakeFrame(32f / 256f, 64f / 256f, 0f, 1f);

        PixelFrameEditor.SetX(frame, 32, 256);

        Assert.Equal(32f / 256f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(64f / 256f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_MoveToZero_BothCoordsShiftLeft()
    {
        var frame = MakeFrame(64f / 256f, 96f / 256f, 0f, 1f);

        PixelFrameEditor.SetX(frame, 0, 256);

        Assert.Equal(0f,        frame.LeftCoordinate,  precision: 4);
        Assert.Equal(32f / 256f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_WidthIsPreservedAfterMove()
    {
        var frame = MakeFrame(10f / 100f, 40f / 100f, 0f, 1f);
        float originalWidth = frame.RightCoordinate - frame.LeftCoordinate;

        PixelFrameEditor.SetX(frame, 20, 100);

        float newWidth = frame.RightCoordinate - frame.LeftCoordinate;
        Assert.Equal(originalWidth, newWidth, precision: 4);
    }

    // ── SetY ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SetY_MovesTopAndBottomByDelta_PreservesHeight()
    {
        // Frame starts at row 0–32 on a 128px texture
        var frame = MakeFrame(0f, 1f, 0f / 128f, 32f / 128f);

        PixelFrameEditor.SetY(frame, 64, 128);

        Assert.Equal(64f / 128f, frame.TopCoordinate,    precision: 4);
        Assert.Equal(96f / 128f, frame.BottomCoordinate, precision: 4);
    }

    [Fact]
    public void SetY_HeightPreservedAfterMove()
    {
        var frame = MakeFrame(0f, 1f, 16f / 128f, 48f / 128f);
        float originalH = frame.BottomCoordinate - frame.TopCoordinate;

        PixelFrameEditor.SetY(frame, 0, 128);

        float newH = frame.BottomCoordinate - frame.TopCoordinate;
        Assert.Equal(originalH, newH, precision: 4);
    }

    // ── SetWidth ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetWidth_AdjustsRightCoord_KeepsLeftFixed()
    {
        var frame = MakeFrame(32f / 256f, 64f / 256f, 0f, 1f);

        PixelFrameEditor.SetWidth(frame, 48, 256);

        Assert.Equal(32f / 256f, frame.LeftCoordinate,  precision: 4);  // unchanged
        Assert.Equal(80f / 256f, frame.RightCoordinate, precision: 4);  // 32 + 48 = 80
    }

    [Fact]
    public void SetWidth_FullTextureWidth_RightIsOne()
    {
        var frame = MakeFrame(0f, 0.5f, 0f, 1f);

        PixelFrameEditor.SetWidth(frame, 128, 128);

        Assert.Equal(0f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(1f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetWidth_SinglePixel_RightIsLeftPlusTiny()
    {
        var frame = MakeFrame(0f, 0.5f, 0f, 1f);

        PixelFrameEditor.SetWidth(frame, 1, 256);

        Assert.Equal(1f / 256f, frame.RightCoordinate, precision: 4);
    }

    // ── SetHeight ─────────────────────────────────────────────────────────────

    [Fact]
    public void SetHeight_AdjustsBottomCoord_KeepsTopFixed()
    {
        var frame = MakeFrame(0f, 1f, 16f / 128f, 48f / 128f);

        PixelFrameEditor.SetHeight(frame, 64, 128);

        Assert.Equal(16f / 128f, frame.TopCoordinate,    precision: 4);  // unchanged
        Assert.Equal(80f / 128f, frame.BottomCoordinate, precision: 4);  // 16 + 64 = 80
    }

    [Fact]
    public void SetHeight_FullTextureHeight_BottomIsOne()
    {
        var frame = MakeFrame(0f, 1f, 0f, 0.5f);

        PixelFrameEditor.SetHeight(frame, 256, 256);

        Assert.Equal(0f, frame.TopCoordinate,    precision: 4);
        Assert.Equal(1f, frame.BottomCoordinate, precision: 4);
    }

    // ── Round helper ──────────────────────────────────────────────────────────

    [Fact]
    public void Round_SnapsToNearestPixelBoundary()
    {
        // 0.1256 * 256 = 32.1536 → rounds to 32 → 32/256
        float coord = 0.1256f;
        float result = PixelFrameEditor.Round(coord, 256);
        Assert.Equal(32f / 256f, result, precision: 4);
    }

    // ── Guard conditions ──────────────────────────────────────────────────────

    [Fact]
    public void SetX_NullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PixelFrameEditor.SetX(null!, 0, 256));
    }

    [Fact]
    public void SetWidth_ZeroTextureWidth_Throws()
    {
        var frame = MakeFrame(0f, 1f, 0f, 1f);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PixelFrameEditor.SetWidth(frame, 32, 0));
    }

    [Fact]
    public void SetHeight_ZeroTextureHeight_Throws()
    {
        var frame = MakeFrame(0f, 1f, 0f, 1f);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PixelFrameEditor.SetHeight(frame, 32, 0));
    }

    // ── SetX boundary behaviour (frames may extend past texture edges) ───────

    [Fact]
    public void SetX_FullWidthFrame_CanExtendRightPastTexture()
    {
        // 1536px-wide frame on a 1536px texture (full width).  Setting X=100 must
        // shift the frame right; right edge legitimately extends to 1636 (> texture width).
        var frame = MakeFrame(0f, 1f, 0f, 1f);   // LeftCoord=0, RightCoord=1.0

        PixelFrameEditor.SetX(frame, 100, 1536);

        Assert.Equal(100f / 1536f,  frame.LeftCoordinate,  precision: 4);
        Assert.Equal(1636f / 1536f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_PastRightBoundary_AllowsExtension_PreservesWidth()
    {
        // 32px-wide frame on 256px texture; setX=250 → right lands at 282/256 (> 1.0).
        // Frames are allowed to extend past the texture edge.
        var frame = MakeFrame(0f, 32f / 256f, 0f, 1f);

        PixelFrameEditor.SetX(frame, 250, 256);

        Assert.Equal(250f / 256f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(282f / 256f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_Negative_AllowsNegativeLeft()
    {
        // 32px-wide frame; setX=-10 → left lands at -10/256 (< 0).
        // Frames are allowed to extend past the left texture edge.
        var frame = MakeFrame(128f / 256f, 160f / 256f, 0f, 1f);

        PixelFrameEditor.SetX(frame, -10, 256);

        Assert.Equal(-10f / 256f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(22f / 256f,  frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_PastRightBoundary_RightExceedsOne()
    {
        // Confirms RightCoordinate is NOT clamped to 1.0 when X pushes frame past texture.
        var frame = MakeFrame(0f, 32f / 256f, 0f, 1f);
        PixelFrameEditor.SetX(frame, 300, 256);
        Assert.True(frame.RightCoordinate > 1f,
            $"RightCoordinate={frame.RightCoordinate} should exceed 1.0 for frame at x=300");
    }

    // ── SetY boundary behaviour (frames may extend past texture edges) ───────

    [Fact]
    public void SetY_PastBottomBoundary_AllowsExtension()
    {
        // 32px-tall frame on 128px texture; setY=110 → bottom lands at 142/128 (> 1.0).
        var frame = MakeFrame(0f, 1f, 0f, 32f / 128f);

        PixelFrameEditor.SetY(frame, 110, 128);

        Assert.Equal(110f / 128f, frame.TopCoordinate,    precision: 4);
        Assert.Equal(142f / 128f, frame.BottomCoordinate, precision: 4);
    }

    [Fact]
    public void SetY_Negative_AllowsNegativeTop()
    {
        // 32px-tall frame; setY=-5 → top lands at -5/128 (< 0).
        var frame = MakeFrame(0f, 1f, 64f / 128f, 96f / 128f);

        PixelFrameEditor.SetY(frame, -5, 128);

        Assert.Equal(-5f / 128f,  frame.TopCoordinate,    precision: 4);
        Assert.Equal(27f / 128f,  frame.BottomCoordinate, precision: 4);
    }

    // ── SetWidth boundary clamping ────────────────────────────────────────────

    [Fact]
    public void SetWidth_Zero_ClampsToOnePixel()
    {
        var frame = MakeFrame(0f, 0.5f, 0f, 1f);
        PixelFrameEditor.SetWidth(frame, 0, 256);
        float width = frame.RightCoordinate - frame.LeftCoordinate;
        Assert.Equal(1f / 256f, width, precision: 4);
    }

    [Fact]
    public void SetWidth_Negative_ClampsToOnePixel()
    {
        var frame = MakeFrame(0f, 0.5f, 0f, 1f);
        PixelFrameEditor.SetWidth(frame, -10, 256);
        float width = frame.RightCoordinate - frame.LeftCoordinate;
        Assert.Equal(1f / 256f, width, precision: 4);
    }

    [Fact]
    public void SetWidth_OversizedWidth_AllowsExtensionPastTextureBoundary()
    {
        // Frame at left=64/256; width=300 → right lands at (64+300)/256 > 1.0.
        // Frames are allowed to have a right edge beyond the texture.
        var frame = MakeFrame(64f / 256f, 96f / 256f, 0f, 1f);
        PixelFrameEditor.SetWidth(frame, 300, 256);
        Assert.Equal((64f + 300f) / 256f, frame.RightCoordinate, precision: 4);
        Assert.Equal(64f / 256f,          frame.LeftCoordinate,  precision: 4);
    }

    // ── SetHeight boundary clamping ───────────────────────────────────────────

    [Fact]
    public void SetHeight_Zero_ClampsToOnePixel()
    {
        var frame = MakeFrame(0f, 1f, 0f, 0.5f);
        PixelFrameEditor.SetHeight(frame, 0, 128);
        float height = frame.BottomCoordinate - frame.TopCoordinate;
        Assert.Equal(1f / 128f, height, precision: 4);
    }

    [Fact]
    public void SetHeight_OversizedHeight_AllowsExtensionPastTextureBoundary()
    {
        // Frame at top=32/128; height=200 → bottom lands at (32+200)/128 > 1.0.
        var frame = MakeFrame(0f, 1f, 32f / 128f, 64f / 128f);
        PixelFrameEditor.SetHeight(frame, 200, 128);
        Assert.Equal((32f + 200f) / 128f, frame.BottomCoordinate, precision: 4);
        Assert.Equal(32f / 128f,          frame.TopCoordinate,    precision: 4);
    }
}
