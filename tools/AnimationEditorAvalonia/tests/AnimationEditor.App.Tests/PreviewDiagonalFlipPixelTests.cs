using AnimationEditor.App.Controls;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

public class PreviewDiagonalFlipPixelTests
{
    // 20x10 sheet: left half red, right half blue.
    private static SKBitmap Draw(bool h, bool v, bool d, (float a, float b, float c, float d)? flipMatrix = null)
    {
        using var sheetBmp = new SKBitmap(20, 10);
        using (var c = new SKCanvas(sheetBmp))
        {
            c.Clear(SKColors.Blue);
            using var red = new SKPaint { Color = SKColors.Red };
            c.DrawRect(0, 0, 10, 10, red);
        }
        using var sheet = SKImage.FromBitmap(sheetBmp);
        var frame = new AnimationFrameSave
        {
            LeftCoordinate = 0f, RightCoordinate = 1f, TopCoordinate = 0f, BottomCoordinate = 1f,
            FlipHorizontal = h, FlipVertical = v, FlipDiagonal = d,
        };
        var target = new SKBitmap(60, 60);
        target.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(target);
        PreviewControl.DrawFrameCore(canvas, frame, default, sheet, 30f, 30f, 1f, 1f, showOutline: false, flipMatrixOverride: flipMatrix);
        return target;
    }

    [Fact]
    public void Diagonal_TransposesFootprint_RedOnTop()
    {
        using var drawn = Draw(false, false, true);
        // transposed footprint is 10 wide x 20 tall centred on (30,30): red top half, blue bottom half
        Assert.Equal(SKColors.Red.WithAlpha(255), drawn.GetPixel(30, 22));
        Assert.Equal(SKColors.Blue.WithAlpha(255), drawn.GetPixel(30, 38));
        Assert.Equal(0, drawn.GetPixel(18, 30).Alpha);
    }

    [Fact]
    public void FlipMatrixOverride_ReplacesTheFlagsMatrix()
    {
        // Flags say "no flip", but the override is the diagonal swap, as mid-animation would be.
        using var drawn = Draw(false, false, false, flipMatrix: (0f, 1f, 1f, 0f));

        Assert.Equal(SKColors.Red.WithAlpha(255), drawn.GetPixel(30, 22));
        Assert.Equal(SKColors.Blue.WithAlpha(255), drawn.GetPixel(30, 38));
    }

    [Fact]
    public void FlipMatrixOverride_Collapsed_DrawsNothing()
    {
        using var drawn = Draw(false, false, false, flipMatrix: (0f, 0f, 0f, 1f));

        for (int y = 0; y < drawn.Height; y++)
            for (int x = 0; x < drawn.Width; x++)
                Assert.Equal(0, drawn.GetPixel(x, y).Alpha);
    }
}
