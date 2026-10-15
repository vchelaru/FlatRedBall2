using AnimationEditor.App;
using FlatRedBall2.Animation;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

// FrameColorFilter maps a frame's ColorOperation + RGB to the SkiaSharp SKColorFilter the preview applies.
// These tests run the resulting filter through Skia's real raster pipeline and read the pixel back, so they
// verify the *rendered* effect — a filter that builds without error but is a visual no-op (the Add bug:
// Plus blend on a premultiplied alpha-0 color) still fails here. Pixels are fully opaque so premultiplied
// readback equals straight color (no rounding), and no color space is set so blends use the raw byte values.
public class FrameColorFilterTests
{
    private static SKColor Apply(SKColorFilter? filter, SKColor input)
    {
        var info = new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        using var paint = new SKPaint { Color = input, ColorFilter = filter, IsAntialias = false };
        canvas.DrawRect(new SKRect(0, 0, 1, 1), paint);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var bmp = SKBitmap.FromImage(image);
        return bmp.GetPixel(0, 0);
    }

    [Fact]
    public void Create_NullOperation_ReturnsNull()
    {
        Assert.Null(FrameColorFilter.Create(null, 255, 0, 0));
    }

    private static void AssertNear(int expected, byte actual) =>
        Assert.InRange((int)actual, expected - 1, expected + 1);

    [Fact]
    public void Create_Multiply_ScalesEachChannelAndLeavesAlpha()
    {
        // 255 is the multiply identity, so an unset channel (green) is left untouched.
        using var filter = FrameColorFilter.Create(ColorOperation.Multiply, 128, null, 64);
        var result = Apply(filter, new SKColor(128, 128, 128, 255));

        AssertNear(64, result.Red);    // 128 * 128/255
        AssertNear(128, result.Green); // 128 * 255/255 (unset = identity)
        AssertNear(32, result.Blue);   // 128 * 64/255
        Assert.Equal(255, result.Alpha);
    }

    [Fact]
    public void Create_Add_BrightensAndClampsWithoutTouchingAlpha()
    {
        // 0 is the add identity, so an unset channel (green/blue) adds nothing.
        using var filter = FrameColorFilter.Create(ColorOperation.Add, 200, null, null);
        var result = Apply(filter, new SKColor(100, 100, 100, 255));

        AssertNear(255, result.Red);   // 100 + 200 clamps to 255
        AssertNear(100, result.Green); // unset = +0
        AssertNear(100, result.Blue);  // unset = +0
        Assert.Equal(255, result.Alpha); // Add must never change alpha
    }

    [Fact]
    public void Create_Add_AddsExactValueWhenNoClamp()
    {
        // Non-clamping case pins the additive math: 50 + 100 = 150, proving the offset is the literal
        // channel value (not zeroed by premultiplication, the original Plus-blend bug).
        using var filter = FrameColorFilter.Create(ColorOperation.Add, 100, 0, 0);
        var result = Apply(filter, new SKColor(50, 50, 50, 255));

        AssertNear(150, result.Red);
        AssertNear(50, result.Green);
        AssertNear(50, result.Blue);
        Assert.Equal(255, result.Alpha);
    }

    [Fact]
    public void Create_Add_PreservesPartialAlpha()
    {
        // A semi-transparent sprite stays semi-transparent under an additive flash.
        using var filter = FrameColorFilter.Create(ColorOperation.Add, 200, 0, 0);
        var result = Apply(filter, new SKColor(100, 100, 100, 128));

        AssertNear(128, result.Alpha);
    }

    [Fact]
    public void Create_Add_NegativeValueDarkensAndClampsAtZero()
    {
        // Add's color-matrix offset is signed math, so a negative value should subtract.
        using var filter = FrameColorFilter.Create(ColorOperation.Add, -60, -200, 0);
        var result = Apply(filter, new SKColor(100, 100, 100, 255));

        AssertNear(40, result.Red);   // 100 - 60
        AssertNear(0, result.Green);  // 100 - 200 clamps to 0
        AssertNear(100, result.Blue); // +0
    }

    [Fact]
    public void Create_Multiply_NegativeValueClampsToZeroInsteadOfWrapping()
    {
        // The channel feeds an SKColor byte; an unclamped negative int would wrap (e.g. -10 -> 246).
        using var filter = FrameColorFilter.Create(ColorOperation.Multiply, -10, null, null);
        var result = Apply(filter, new SKColor(200, 200, 200, 255));

        AssertNear(0, result.Red);
    }

    [Fact]
    public void Create_Multiply_ValueAbove255ClampsInsteadOfWrapping()
    {
        // A Back curve can overshoot a channel past 255; an unclamped byte cast would wrap 300 to 44.
        using var filter = FrameColorFilter.Create(ColorOperation.Multiply, 300, null, null);
        var result = Apply(filter, new SKColor(200, 200, 200, 255));

        AssertNear(200, result.Red);
    }

    [Fact]
    public void Create_Add_ValueBeyondRangeClampsAtThePixel()
    {
        using var filter = FrameColorFilter.Create(ColorOperation.Add, 400, -400, null);
        var result = Apply(filter, new SKColor(100, 100, 100, 255));

        AssertNear(255, result.Red);
        AssertNear(0, result.Green);
        AssertNear(100, result.Blue);
    }
}
