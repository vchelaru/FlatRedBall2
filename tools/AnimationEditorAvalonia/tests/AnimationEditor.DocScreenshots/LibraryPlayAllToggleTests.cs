using System;
using System.IO;
using System.Linq;
using AnimationEditor.App.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// The library header's "play all" toggle draws its label centered in the button. Lives here, not in App.Tests,
/// because it measures real pixels (see <see cref="TestAppBuilder"/>): the ▶ glyph falls back to another font whose
/// taller line box once pushed the whole label up.
/// </summary>
public class LibraryPlayAllToggleTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Label_IsVerticallyCenteredInTheButton(bool isChecked)
    {
        var library = new SequenceLibraryControl { AutoplayAll = isChecked };
        var window = new Window { Width = 330, Height = 120, Content = library };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var toggle = library.GetVisualDescendants().OfType<ToggleButton>().Single();
        var origin = toggle.TranslatePoint(new Point(0, 0), window)!.Value;

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()!;
        string outDir = ScreenshotOutput.ResolveFeatureDir("issue-1622-autoplay");
        frame.Save(Path.Combine(outDir, isChecked ? "toggle-on.png" : "toggle-off.png"));
        var pixel = Reader(frame);

        int left = (int)Math.Round(origin.X);
        int top = (int)Math.Round(origin.Y);
        int width = (int)Math.Round(toggle.Bounds.Width);
        int height = (int)Math.Round(toggle.Bounds.Height);
        // The fill just inside the left edge, at mid height, is the button's own background.
        var background = pixel(left + 4, top + height / 2);
        int inkTop = int.MaxValue, inkBottom = int.MinValue;
        for (int y = top + 3; y < top + height - 3; y++)
        for (int x = left + 6; x < left + width - 6; x++)
        {
            var p = pixel(x, y);
            if (Math.Abs(p.r - background.r) + Math.Abs(p.g - background.g) + Math.Abs(p.b - background.b) < 120) continue;
            inkTop = Math.Min(inkTop, y);
            inkBottom = Math.Max(inkBottom, y);
        }

        Assert.True(inkTop <= inkBottom, "no label ink found in the toggle");
        int gapAbove = inkTop - top;
        int gapBelow = top + height - 1 - inkBottom;
        Assert.True(Math.Abs(gapAbove - gapBelow) <= 1,
            $"label ink spans rows {inkTop - top}..{inkBottom - top} of a {height}px button: {gapAbove}px above, {gapBelow}px below");
    }

    private static Func<int, int, (int r, int g, int b)> Reader(WriteableBitmap frame)
    {
        var buffer = new byte[frame.PixelSize.Width * frame.PixelSize.Height * 4];
        using (var locked = frame.Lock())
        {
            int row = frame.PixelSize.Width * 4;
            for (int y = 0; y < frame.PixelSize.Height; y++)
                System.Runtime.InteropServices.Marshal.Copy(locked.Address + y * locked.RowBytes, buffer, y * row, row);
        }
        int width = frame.PixelSize.Width;
        return (x, y) =>
        {
            int i = (y * width + x) * 4;
            return (buffer[i], buffer[i + 1], buffer[i + 2]);
        };
    }
}
