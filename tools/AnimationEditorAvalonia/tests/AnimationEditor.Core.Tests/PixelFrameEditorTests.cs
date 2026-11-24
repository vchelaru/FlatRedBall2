using AnimationEditor.Core.Rendering;
using FlatRedBall2.AnimationEditorCommon;
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
        var frame = MakeFrame(16f, 48f, 0f, 256f);

        PixelFrameEditor.SetX(frame, 32);   // move to pixel 32

        Assert.Equal(32f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(64f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_SamePosition_NoChange()
    {
        var frame = MakeFrame(32f, 64f, 0f, 256f);

        PixelFrameEditor.SetX(frame, 32);

        Assert.Equal(32f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(64f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_MoveToZero_BothCoordsShiftLeft()
    {
        var frame = MakeFrame(64f, 96f, 0f, 256f);

        PixelFrameEditor.SetX(frame, 0);

        Assert.Equal(0f,        frame.LeftCoordinate,  precision: 4);
        Assert.Equal(32f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_WidthIsPreservedAfterMove()
    {
        var frame = MakeFrame(10f, 40f, 0f, 100f);
        float originalWidth = frame.RightCoordinate - frame.LeftCoordinate;

        PixelFrameEditor.SetX(frame, 20);

        float newWidth = frame.RightCoordinate - frame.LeftCoordinate;
        Assert.Equal(originalWidth, newWidth, precision: 4);
    }

    // ── SetY ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SetY_MovesTopAndBottomByDelta_PreservesHeight()
    {
        // Frame starts at row 0–32 on a 128px texture
        var frame = MakeFrame(0f, 128f, 0f, 32f);

        PixelFrameEditor.SetY(frame, 64);

        Assert.Equal(64f, frame.TopCoordinate,    precision: 4);
        Assert.Equal(96f, frame.BottomCoordinate, precision: 4);
    }

    [Fact]
    public void SetY_HeightPreservedAfterMove()
    {
        var frame = MakeFrame(0f, 128f, 16f, 48f);
        float originalH = frame.BottomCoordinate - frame.TopCoordinate;

        PixelFrameEditor.SetY(frame, 0);

        float newH = frame.BottomCoordinate - frame.TopCoordinate;
        Assert.Equal(originalH, newH, precision: 4);
    }

    // ── SetWidth ──────────────────────────────────────────────────────────────

    [Fact]
    public void SetWidth_AdjustsRightCoord_KeepsLeftFixed()
    {
        var frame = MakeFrame(32f, 64f, 0f, 256f);

        PixelFrameEditor.SetWidth(frame, 48);

        Assert.Equal(32f, frame.LeftCoordinate,  precision: 4);  // unchanged
        Assert.Equal(80f, frame.RightCoordinate, precision: 4);  // 32 + 48 = 80
    }

    [Fact]
    public void SetWidth_FullTextureWidth_RightIsOne()
    {
        var frame = MakeFrame(0f, 64f, 0f, 128f);

        PixelFrameEditor.SetWidth(frame, 128);

        Assert.Equal(0f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(128f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetWidth_SinglePixel_RightIsLeftPlusTiny()
    {
        var frame = MakeFrame(0f, 128f, 0f, 256f);

        PixelFrameEditor.SetWidth(frame, 1);

        Assert.Equal(1f, frame.RightCoordinate, precision: 4);
    }

    // ── SetHeight ─────────────────────────────────────────────────────────────

    [Fact]
    public void SetHeight_AdjustsBottomCoord_KeepsTopFixed()
    {
        var frame = MakeFrame(0f, 128f, 16f, 48f);

        PixelFrameEditor.SetHeight(frame, 64);

        Assert.Equal(16f, frame.TopCoordinate,    precision: 4);  // unchanged
        Assert.Equal(80f, frame.BottomCoordinate, precision: 4);  // 16 + 64 = 80
    }

    [Fact]
    public void SetHeight_FullTextureHeight_BottomIsOne()
    {
        var frame = MakeFrame(0f, 256f, 0f, 128f);

        PixelFrameEditor.SetHeight(frame, 256);

        Assert.Equal(0f, frame.TopCoordinate,    precision: 4);
        Assert.Equal(256f, frame.BottomCoordinate, precision: 4);
    }

    // ── Guard conditions ──────────────────────────────────────────────────────

    [Fact]
    public void SetX_NullFrame_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PixelFrameEditor.SetX(null!, 0));
    }

    // ── SetX boundary behaviour (frames may extend past texture edges) ───────

    [Fact]
    public void SetX_FullWidthFrame_CanExtendRightPastTexture()
    {
        // 1536px-wide frame on a 1536px texture (full width).  Setting X=100 must
        // shift the frame right; right edge legitimately extends to 1636 (> texture width).
        var frame = MakeFrame(0f, 1536f, 0f, 1536f);

        PixelFrameEditor.SetX(frame, 100);

        Assert.Equal(100f,  frame.LeftCoordinate,  precision: 4);
        Assert.Equal(1636f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_PastRightBoundary_AllowsExtension_PreservesWidth()
    {
        // 32px-wide frame on 256px texture; setX=250 → right lands at 282 (past the texture).
        // Frames are allowed to extend past the texture edge.
        var frame = MakeFrame(0f, 32f, 0f, 256f);

        PixelFrameEditor.SetX(frame, 250);

        Assert.Equal(250f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(282f, frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_Negative_AllowsNegativeLeft()
    {
        // 32px-wide frame; setX=-10 → left lands at -10 (< 0).
        // Frames are allowed to extend past the left texture edge.
        var frame = MakeFrame(128f, 160f, 0f, 256f);

        PixelFrameEditor.SetX(frame, -10);

        Assert.Equal(-10f, frame.LeftCoordinate,  precision: 4);
        Assert.Equal(22f,  frame.RightCoordinate, precision: 4);
    }

    [Fact]
    public void SetX_PastRightBoundary_RightExceedsOne()
    {
        // Confirms RightCoordinate is NOT clamped to the texture when X pushes frame past texture.
        var frame = MakeFrame(0f, 32f, 0f, 256f);
        PixelFrameEditor.SetX(frame, 300);
        Assert.True(frame.RightCoordinate > 256f,
            $"RightCoordinate={frame.RightCoordinate} should exceed 256 for frame at x=300");
    }

    // ── SetY boundary behaviour (frames may extend past texture edges) ───────

    [Fact]
    public void SetY_PastBottomBoundary_AllowsExtension()
    {
        // 32px-tall frame on 128px texture; setY=110 → bottom lands at 142 (past the texture).
        var frame = MakeFrame(0f, 128f, 0f, 32f);

        PixelFrameEditor.SetY(frame, 110);

        Assert.Equal(110f, frame.TopCoordinate,    precision: 4);
        Assert.Equal(142f, frame.BottomCoordinate, precision: 4);
    }

    [Fact]
    public void SetY_Negative_AllowsNegativeTop()
    {
        // 32px-tall frame; setY=-5 → top lands at -5 (< 0).
        var frame = MakeFrame(0f, 128f, 64f, 96f);

        PixelFrameEditor.SetY(frame, -5);

        Assert.Equal(-5f,  frame.TopCoordinate,    precision: 4);
        Assert.Equal(27f,  frame.BottomCoordinate, precision: 4);
    }

    // ── SetWidth boundary clamping ────────────────────────────────────────────

    [Fact]
    public void SetWidth_Zero_ClampsToOnePixel()
    {
        var frame = MakeFrame(0f, 128f, 0f, 256f);
        PixelFrameEditor.SetWidth(frame, 0);
        float width = frame.RightCoordinate - frame.LeftCoordinate;
        Assert.Equal(1f, width, precision: 4);
    }

    [Fact]
    public void SetWidth_Negative_ClampsToOnePixel()
    {
        var frame = MakeFrame(0f, 128f, 0f, 256f);
        PixelFrameEditor.SetWidth(frame, -10);
        float width = frame.RightCoordinate - frame.LeftCoordinate;
        Assert.Equal(1f, width, precision: 4);
    }

    [Fact]
    public void SetWidth_OversizedWidth_AllowsExtensionPastTextureBoundary()
    {
        // Frame at left=64; width=300 → right lands at 64+300 past the texture.
        // Frames are allowed to have a right edge beyond the texture.
        var frame = MakeFrame(64f, 96f, 0f, 256f);
        PixelFrameEditor.SetWidth(frame, 300);
        Assert.Equal((64f + 300f), frame.RightCoordinate, precision: 4);
        Assert.Equal(64f,          frame.LeftCoordinate,  precision: 4);
    }

    // ── SetHeight boundary clamping ───────────────────────────────────────────

    [Fact]
    public void SetHeight_Zero_ClampsToOnePixel()
    {
        var frame = MakeFrame(0f, 128f, 0f, 64f);
        PixelFrameEditor.SetHeight(frame, 0);
        float height = frame.BottomCoordinate - frame.TopCoordinate;
        Assert.Equal(1f, height, precision: 4);
    }

    [Fact]
    public void SetHeight_OversizedHeight_AllowsExtensionPastTextureBoundary()
    {
        // Frame at top=32; height=200 → bottom lands at 32+200 past the texture.
        var frame = MakeFrame(0f, 128f, 32f, 64f);
        PixelFrameEditor.SetHeight(frame, 200);
        Assert.Equal((32f + 200f), frame.BottomCoordinate, precision: 4);
        Assert.Equal(32f,          frame.TopCoordinate,    precision: 4);
    }
}
