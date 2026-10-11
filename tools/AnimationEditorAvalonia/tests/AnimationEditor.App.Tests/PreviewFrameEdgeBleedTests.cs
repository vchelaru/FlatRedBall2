using AnimationEditor.App.Controls;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Downscaled (zoom &lt; 1) preview frames use bilinear sampling, which by default may read pixels
/// outside the source rect, so a neighbouring frame on the sheet bleeds into the edge of the drawn
/// frame. The preview must clamp to the frame's own edge pixels instead.
/// </summary>
public class PreviewFrameEdgeBleedTests
{
    // 20x20 sheet: the top-left 10x10 frame is pure red, everything else around it is pure blue.
    private static SKImage RedFrameInBlueSheet()
    {
        using var bitmap = new SKBitmap(20, 20);
        bitmap.Erase(SKColors.Blue);
        using (var canvas = new SKCanvas(bitmap))
        using (var red = new SKPaint { Color = SKColors.Red })
            canvas.DrawRect(0, 0, 10, 10, red);
        return SKImage.FromBitmap(bitmap);
    }

    private static AnimationFrameSave TopLeftFrame() => new()
    {
        LeftCoordinate = 0f, RightCoordinate = 0.5f,
        TopCoordinate = 0f, BottomCoordinate = 0.5f,
    };

    private static SKBitmap Draw(SKImage sheet, float zoom)
    {
        var target = new SKBitmap(100, 100);
        target.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(target);
        PreviewControl.DrawFrameCore(
            canvas, TopLeftFrame(), default, sheet, cx: 50f, cy: 50f, zoom, alpha: 1f, showOutline: false);
        return target;
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(0.5f)]
    [InlineData(0.33f)]
    public void DrawFrameCore_Downscaled_NeighbouringFrameDoesNotBleedIn(float zoom)
    {
        using var sheet = RedFrameInBlueSheet();
        using var drawn = Draw(sheet, zoom);

        int drawnPixels = 0;
        for (int y = 0; y < drawn.Height; y++)
        for (int x = 0; x < drawn.Width; x++)
        {
            var px = drawn.GetPixel(x, y);
            if (px.Alpha == 0) continue;
            drawnPixels++;
            Assert.True(px.Blue <= 2, $"pixel ({x},{y}) picked up blue from the neighbouring frame: {px}");
        }

        Assert.True(drawnPixels > 0, "the frame should have been drawn");
    }

    [Fact]
    public void DrawFrameCore_Downscaled_FrameInteriorStillDrawsAndEdgesStayOpaque()
    {
        using var sheet = RedFrameInBlueSheet();
        using var drawn = Draw(sheet, 0.75f);

        // 10 px * 0.75 = 7.5 px square centred on (50,50): its centre must be solid red.
        Assert.Equal(new SKColor(255, 0, 0, 255), drawn.GetPixel(50, 50));
    }
}
