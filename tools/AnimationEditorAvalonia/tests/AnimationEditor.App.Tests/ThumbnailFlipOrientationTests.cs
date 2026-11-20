using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1849: the animation-tree thumbnail must be oriented exactly like the preview canvas for
/// every H/V/diagonal flip combination (a diagonal flip used to be ignored by the thumbnail).
/// </summary>
public class ThumbnailFlipOrientationTests
{
    private const int SheetW = 40, SheetH = 20;

    // Four distinct quadrants on a non-square sheet, so any transposition or mirror is visible.
    private static SKBitmap Sheet()
    {
        var bm = new SKBitmap(SheetW, SheetH);
        var colors = new[] { SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Yellow };
        for (int y = 0; y < SheetH; y++)
            for (int x = 0; x < SheetW; x++)
                bm.SetPixel(x, y, colors[(y < SheetH / 2 ? 0 : 2) + (x < SheetW / 2 ? 0 : 1)]);
        return bm;
    }

    public static TheoryData<bool, bool, bool> AllEightFlips()
    {
        var data = new TheoryData<bool, bool, bool>();
        foreach (var h in new[] { false, true })
            foreach (var v in new[] { false, true })
                foreach (var d in new[] { false, true })
                    data.Add(h, v, d);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllEightFlips))]
    public void Thumbnail_MatchesPreviewOrientation(bool h, bool v, bool d)
    {
        using var sheet = Sheet();
        var frame = new AnimationFrameSave
        {
            LeftCoordinate = 0f, RightCoordinate = 1f, TopCoordinate = 0f, BottomCoordinate = 1f,
            FlipHorizontal = h, FlipVertical = v, FlipDiagonal = d,
        };

        using var thumb = ThumbnailService.RenderFrameThumbnail(sheet, frame, default, 40, 40)!;

        using var image = SKImage.FromBitmap(sheet);
        using var preview = new SKBitmap(100, 100);
        preview.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(preview))
            PreviewControl.DrawFrameCore(canvas, frame, default, image, 50f, 50f, 1f, 1f, showOutline: false);

        // At 1x the preview footprint is the thumbnail, centred on (50, 50).
        Assert.Equal(d ? SheetH : SheetW, thumb.Width);
        Assert.Equal(d ? SheetW : SheetH, thumb.Height);
        int ox = 50 - thumb.Width / 2, oy = 50 - thumb.Height / 2;
        for (int y = 0; y < thumb.Height; y++)
            for (int x = 0; x < thumb.Width; x++)
                Assert.Equal(preview.GetPixel(ox + x, oy + y), thumb.GetPixel(x, y));
    }
}
