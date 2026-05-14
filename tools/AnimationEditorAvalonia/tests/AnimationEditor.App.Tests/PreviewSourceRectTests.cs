using AnimationEditor.App.Controls;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for <see cref="PreviewControl.ComputeSourceRect"/> — the pixel source-rect
/// calculation that feeds <c>DrawFrameCore</c>.
///
/// Root cause of issue #260: the old inline code used <c>(int)</c> truncation on
/// UV-coordinate arithmetic. For non-power-of-2 textures, floating-point rounding
/// means <c>(RightUV - LeftUV) * texWidth</c> can be slightly below the integer
/// (e.g. 79.9999… instead of 80), causing sw to truncate to 79. That one-pixel
/// jitter in sw makes <c>dw = sw * zoom</c> jitter, which shifts the centred
/// destination rect by 0.5 px every time the region moves.
///
/// The fix: use <c>FrameDisplayValues</c> (Math.Round) for all four components
/// and derive sw/sh from rounded endpoints so width is always stable.
/// </summary>
public class PreviewSourceRectTests
{
    private static AnimationFrameSave Frame(float left, float right, float top, float bottom)
        => new()
        {
            LeftCoordinate   = left,
            RightCoordinate  = right,
            TopCoordinate    = top,
            BottomCoordinate = bottom,
        };

    // ── sx/sy — rounding, not truncation ─────────────────────────────────────

    [Fact]
    public void ComputeSourceRect_OriginFrame_SxSyAreZero()
    {
        var frame = Frame(0f, 0.5f, 0f, 0.5f);
        var (sx, sy, _, _) = PreviewControl.ComputeSourceRect(frame, 256, 256);
        Assert.Equal(0, sx);
        Assert.Equal(0, sy);
    }

    [Fact]
    public void ComputeSourceRect_Sx_RoundsNotTruncates()
    {
        // 0.1f * 100 = 10.0000001... → rounds to 10, not truncates to 10 (coincidentally fine)
        // More illustrative: a UV that is slightly below its integer on a 100px texture.
        // Left = 11/100 = 0.11f → float ≈ 0.10999999940... → * 100 ≈ 10.999...
        // (int) truncation → sx = 10  (WRONG)
        // Math.Round         → sx = 11 (correct)
        var frame = Frame(11f / 100f, 91f / 100f, 0f, 1f);
        var (sx, _, _, _) = PreviewControl.ComputeSourceRect(frame, 100, 100);
        Assert.Equal(11, sx);
    }

    // ── sw/sh — stable width across positions (the jitter bug) ───────────────

    [Fact]
    public void ComputeSourceRect_NonPowerOfTwoTexture_SwStableAcrossPositions()
    {
        // 80-pixel wide region on a 100-pixel wide texture at five positions.
        // With old (int) truncation: (0.9f - 0.1f) * 100 = 79.9999... → sw = 79 (jitter).
        // With Math.Round (correct): sw = 80 at every position.
        int[] lefts = { 0, 5, 10, 15, 20 };
        foreach (int left in lefts)
        {
            var frame = Frame(left / 100f, (left + 80) / 100f, 0f, 1f);
            var (_, _, sw, _) = PreviewControl.ComputeSourceRect(frame, 100, 100);
            Assert.True(sw == 80, $"sw should be 80 for left={left} but got {sw}");
        }
    }

    [Fact]
    public void ComputeSourceRect_NonPowerOfTwoTexture_ShStableAcrossPositions()
    {
        // Same test for height dimension.
        int[] tops = { 0, 3, 7, 12, 20 };
        foreach (int top in tops)
        {
            var frame = Frame(0f, 1f, top / 100f, (top + 60) / 100f);
            var (_, _, _, sh) = PreviewControl.ComputeSourceRect(frame, 100, 100);
            Assert.True(sh == 60, $"sh should be 60 for top={top} but got {sh}");
        }
    }

    [Fact]
    public void ComputeSourceRect_ZeroSizeRegion_ClampsToOne()
    {
        var frame = Frame(0.5f, 0.5f, 0.5f, 0.5f);
        var (_, _, sw, sh) = PreviewControl.ComputeSourceRect(frame, 256, 256);
        Assert.Equal(1, sw);
        Assert.Equal(1, sh);
    }

    // ── full round-trip on a power-of-2 texture ───────────────────────────────

    [Fact]
    public void ComputeSourceRect_PowerOfTwoTexture_ExactValues()
    {
        // 32×32 frame starting at (64, 48) on a 256×256 texture
        var frame = Frame(64f / 256f, 96f / 256f, 48f / 256f, 80f / 256f);
        var (sx, sy, sw, sh) = PreviewControl.ComputeSourceRect(frame, 256, 256);
        Assert.Equal(64, sx);
        Assert.Equal(48, sy);
        Assert.Equal(32, sw);
        Assert.Equal(32, sh);
    }
}
