using AnimationEditor.App.Services;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #261: <see cref="ThumbnailService.RenderFrameThumbnail"/> crops a frame region out
/// of a sprite sheet and scales it for the tree preview icon. It must (a) bake at the
/// requested size so the icon is not upscaled and blurry, (b) isolate the region so the
/// sampler does not bleed in neighbouring frames, and (c) use nearest-neighbour ("point")
/// sampling so game art stays crisp.
///
/// These tests drive the pure SkiaSharp core directly with in-memory <see cref="SKBitmap"/>
/// sources — no file I/O, no PNG decode, no Avalonia bitmap wrapping — so they are
/// deterministic across platforms (the headless Linux CI runner included).
/// </summary>
public class ThumbnailServiceTests
{
    /// <summary>A frame whose UV region is the given rectangle of the source (defaults to the whole sheet).</summary>
    private static AnimationFrameSave Frame(float left = 0f, float top = 0f, float right = 1f, float bottom = 1f)
        => new()
        {
            LeftCoordinate  = left,  TopCoordinate    = top,
            RightCoordinate = right, BottomCoordinate = bottom,
        };

    /// <summary>A sprite sheet whose left half is <paramref name="left"/> and right half <paramref name="right"/>.</summary>
    private static SKBitmap SplitSheet(int width, int height, SKColor left, SKColor right)
    {
        var bm = new SKBitmap(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bm.SetPixel(x, y, x < width / 2 ? left : right);
        return bm;
    }

    [Fact]
    public void RenderFrameThumbnail_SquareSource_BakesAtTheRequestedSize()
    {
        // Regression (#261): the chain preview was baked at 14px then shown larger — blurry.
        // A square source asked for 56×56 must come back 56×56, never tiny.
        using var source = new SKBitmap(64, 64);
        source.Erase(SKColors.Red);

        using var thumb = ThumbnailService.RenderFrameThumbnail(source, Frame(), 56, 56);

        Assert.NotNull(thumb);
        Assert.Equal(56, thumb!.Width);
        Assert.Equal(56, thumb.Height);
    }

    [Fact]
    public void RenderFrameThumbnail_CroppingOneRegion_DoesNotBleedTheNeighbouringRegion()
    {
        // Left half red, right half blue. Cropping exactly the left half must yield pure red —
        // no blue pulled in from across the seam.
        using var source = SplitSheet(16, 8, SKColors.Red, SKColors.Blue);

        using var thumb = ThumbnailService.RenderFrameThumbnail(
            source, Frame(right: 0.5f), 56, 56);

        Assert.NotNull(thumb);
        for (int y = 0; y < thumb!.Height; y++)
            for (int x = 0; x < thumb.Width; x++)
            {
                var p = thumb.GetPixel(x, y);
                if (p.Alpha == 0) continue;
                Assert.True(p.Red >= p.Blue,
                    $"Pixel ({x},{y}) = {p} — blue from the neighbouring region bled into the red crop.");
            }
    }

    [Fact]
    public void RenderFrameThumbnail_UsesPointSampling_SoUpscaledArtStaysCrisp()
    {
        // A 2×1 red|blue source upscaled hugely must stay a hard seam — linear filtering
        // would smear a band of blended pixels across the middle.
        using var source = SplitSheet(2, 1, SKColors.Red, SKColors.Blue);

        using var thumb = ThumbnailService.RenderFrameThumbnail(source, Frame(), 40, 40);

        Assert.NotNull(thumb);
        for (int y = 0; y < thumb!.Height; y++)
            for (int x = 0; x < thumb.Width; x++)
            {
                var p = thumb.GetPixel(x, y);
                if (p.Alpha == 0) continue;
                bool pureRed  = p is { Red: > 200, Blue: < 55 };
                bool pureBlue = p is { Blue: > 200, Red: < 55 };
                Assert.True(pureRed || pureBlue,
                    $"Pixel ({x},{y}) = {p} — point sampling should leave a hard seam, not a blended pixel.");
            }
    }

    [Fact]
    public void RenderFrameThumbnail_WithFlipHorizontal_MirrorsLeftAndRight()
    {
        // Left half red, right half green. With FlipHorizontal the left side must
        // become green and the right side must become red.
        using var source = SplitSheet(16, 16, SKColors.Red, SKColors.Green);
        var frame = Frame();
        frame.FlipHorizontal = true;

        using var thumb = ThumbnailService.RenderFrameThumbnail(source, frame, 16, 16);

        Assert.NotNull(thumb);
        var pxLeft  = thumb!.GetPixel(4,  8);
        var pxRight = thumb.GetPixel(12,  8);
        Assert.True(pxLeft.Green  > pxLeft.Red,
            $"After H-flip: left expected green; R={pxLeft.Red} G={pxLeft.Green}");
        Assert.True(pxRight.Red  > pxRight.Green,
            $"After H-flip: right expected red; R={pxRight.Red} G={pxRight.Green}");
    }

    [Fact]
    public void RenderFrameThumbnail_WithFlipVertical_MirrorsTopAndBottom()
    {
        // Top half red, bottom half blue. With FlipVertical the top must become
        // blue and the bottom must become red.
        using var source = TopBottomSheet(16, 16, SKColors.Red, SKColors.Blue);
        var frame = Frame();
        frame.FlipVertical = true;

        using var thumb = ThumbnailService.RenderFrameThumbnail(source, frame, 16, 16);

        Assert.NotNull(thumb);
        var pxTop    = thumb!.GetPixel(8, 4);
        var pxBottom = thumb.GetPixel(8, 12);
        Assert.True(pxTop.Blue  > pxTop.Red,
            $"After V-flip: top expected blue; R={pxTop.Red} B={pxTop.Blue}");
        Assert.True(pxBottom.Red > pxBottom.Blue,
            $"After V-flip: bottom expected red; R={pxBottom.Red} B={pxBottom.Blue}");
    }

    /// <summary>A sprite sheet whose top half is <paramref name="top"/> and bottom half <paramref name="bottom"/>.</summary>
    private static SKBitmap TopBottomSheet(int width, int height, SKColor top, SKColor bottom)
    {
        var bm = new SKBitmap(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                bm.SetPixel(x, y, y < height / 2 ? top : bottom);
        return bm;
    }
}
