using AnimationEditor.App.Controls;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #1241: the add-frame ghost's white dash must sit centered in its dark outline on every side.
/// Off-center, the outline shows only outside the top/left edges and only inside the
/// bottom/right ones, which reads as a lopsided shadow on the light canvas.
/// </summary>
public class WireframeAddFrameGhostRenderTests
{
    // Mid-gray so dark outline and white dash pixels are both unambiguous.
    private static readonly SKColor Background = new(128, 128, 128);

    private static bool IsDark(SKColor c) => c.Red < 96;
    private static bool IsLight(SKColor c) => c.Red > 192;

    // Across one edge, on every scanline crossing a dash: dark pixels outside vs inside the dash.
    private static List<(int outer, int inner)> DarkFlanks(SKBitmap bm, int edge, bool vertical, bool outerIsLow)
    {
        var result = new List<(int, int)>();
        for (int s = 14; s <= 26; s++)
        {
            var line = Enumerable.Range(edge - 5, 11)
                .Select(i => vertical ? bm.GetPixel(i, s) : bm.GetPixel(s, i)).ToList();
            int firstLight = line.FindIndex(IsLight);
            if (firstLight < 0) continue;
            int lastLight = line.FindLastIndex(IsLight);
            int low = line.Take(firstLight).Count(IsDark);
            int high = line.Skip(lastLight + 1).Count(IsDark);
            result.Add(outerIsLow ? (low, high) : (high, low));
        }
        return result;
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.75f)]
    public void DrawAddFrameGhost_AnySubpixelOffset_DashCenteredInOutlineOnAllSides(float offset)
    {
        using var bm = new SKBitmap(40, 40);
        using (var canvas = new SKCanvas(bm))
        {
            canvas.Clear(Background);
            WireframeControl.DrawAddFrameGhost(canvas, new SKRect(10 + offset, 10 + offset, 30 + offset, 30 + offset));
        }

        var flanks = DarkFlanks(bm, 10, vertical: true, outerIsLow: true)
            .Concat(DarkFlanks(bm, 30, vertical: true, outerIsLow: false))
            .Concat(DarkFlanks(bm, 10, vertical: false, outerIsLow: true))
            .Concat(DarkFlanks(bm, 30, vertical: false, outerIsLow: false))
            .ToList();

        Assert.NotEmpty(flanks);
        Assert.All(flanks, f =>
        {
            Assert.True(f.outer > 0, $"no outline outside the dash: {f}");
            Assert.Equal(f.outer, f.inner);
        });
    }
}
