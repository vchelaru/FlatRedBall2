using AnimationEditor.App.Controls;
using Shouldly;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

public class GrabCursorArtTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    public void Render_OpenAndClosedHands_HaveADarkFillInsideAWhiteOutline_AndClearCorners(int size)
    {
        foreach (bool closed in new[] { false, true })
        {
            using var bitmap = GrabCursorArt.Render(closed, size);
            bitmap.Width.ShouldBe(size);
            bitmap.Height.ShouldBe(size);
            bitmap.GetPixel(0, 0).Alpha.ShouldBe((byte)0);
            bitmap.GetPixel(size - 1, size - 1).Alpha.ShouldBe((byte)0);

            int dark = 0, white = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    if (c.Alpha < 250) continue;
                    if (c.Red < 80 && c.Green < 80 && c.Blue < 80) dark++;
                    else if (c.Red > 230 && c.Green > 230 && c.Blue > 230) white++;
                }
            dark.ShouldBeGreaterThan(size * size / 8);
            white.ShouldBeGreaterThan(size);
        }
    }

    [Fact]
    public void Render_OpenAndClosedHandsDiffer()
    {
        using var open = GrabCursorArt.Render(false, 32);
        using var closed = GrabCursorArt.Render(true, 32);
        open.Bytes.AsSpan().SequenceEqual(closed.Bytes).ShouldBeFalse();
    }

    [Fact]
    public void Hotspot_IsTheMiddleOfTheCursor()
    {
        GrabCursorArt.HotspotFor(32).ShouldBe((16, 16));
        GrabCursorArt.HotspotFor(64).ShouldBe((32, 32));
    }

    // Not an assertion: writes the cursors on light and dark backgrounds at 100/150/200% scaling for a human to look at.
    [Fact]
    public void WritePreviewPng()
    {
        int[] sizes = { 32, 48, 64 };
        const int cell = 96;
        using var sheet = new SKBitmap(cell * 4, cell * sizes.Length);
        using var canvas = new SKCanvas(sheet);
        for (int row = 0; row < sizes.Length; row++)
            for (int col = 0; col < 4; col++)
            {
                canvas.DrawRect(col * cell, row * cell, cell, cell, new SKPaint { Color = col < 2 ? new SKColor(245, 245, 245) : new SKColor(30, 30, 34) });
                using var cursor = GrabCursorArt.Render(col % 2 == 1, sizes[row]);
                canvas.DrawImage(SKImage.FromBitmap(cursor), col * cell + (cell - sizes[row]) / 2f, row * cell + (cell - sizes[row]) / 2f, new SKSamplingOptions(), null);
            }
        using var image = SKImage.FromBitmap(sheet);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "grab-cursor-preview.png"), data.ToArray());
    }
}
