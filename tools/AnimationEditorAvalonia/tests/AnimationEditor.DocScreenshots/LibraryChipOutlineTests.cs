using System;
using System.IO;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// A hovered library chip draws its accent outline. The thumbnail inside is square, so it must not paint over the
/// outline's rounded top corners the way the unpainted name row never does at the bottom. Lives here, not in
/// App.Tests, because it needs real pixels (see <see cref="TestAppBuilder"/>).
/// </summary>
public class LibraryChipOutlineTests
{
    private const int Margin = 12;
    private const int CornerPixels = 6;

    [AvaloniaFact]
    public void HoveredChip_OutlineCornersAreTheSameAtTopAndBottom()
    {
        string dir = Path.Combine(Path.GetTempPath(), "AEChipOutline", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "x.png", FrameLength = 0.1f });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "x.png", FrameLength = 0.1f });
        list.AnimationChains.Add(chain);
        list.Save(Path.Combine(dir, "hero.achx"));
        var seq = new SequenceListSave();
        seq.Sources.Add("hero.achx");
        string seqPath = Path.Combine(dir, "boss.seqj");
        seq.Save(seqPath);
        var document = SequenceDocument.Load(seqPath);

        var library = new SequenceLibraryControl();
        library.Show(document);
        var chip = library.ChipFor("hero.achx", "Walk")!;
        var host = new Border { Padding = new Thickness(Margin), Child = library, Width = 400, Height = 300 };
        var window = new Window { Width = 400, Height = 300, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var origin = chip.TranslatePoint(new Point(0, 0), window)!.Value;
        window.MouseMove(new Point(origin.X + 40, origin.Y + 30));
        Dispatcher.UIThread.RunJobs();
        Assert.True(chip.IsPlaying);

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()!;
        string outDir = ScreenshotOutput.ResolveFeatureDir("chip-outline-clip");
        frame.Save(Path.Combine(outDir, Environment.GetEnvironmentVariable("CHIP_SHOT") ?? "chip.png"));

        int left = (int)Math.Round(origin.X);
        int top = (int)Math.Round(origin.Y);
        int bottom = top + (int)Math.Round(chip.Bounds.Height) - 1;
        int right = left + (int)Math.Round(chip.Bounds.Width) - 1;
        var pixels = Read(frame);

        // Redness is high where the accent outline shows and ~0 on the grey fills. The unfixed corner differs by ~140, the antialiased seam by ~40.
        for (int dy = 0; dy < CornerPixels; dy++)
        for (int dx = 0; dx < CornerPixels; dx++)
        {
            AssertSame(pixels, left + dx, top + dy, left + dx, bottom - dy, "left", dx, dy);
            AssertSame(pixels, right - dx, top + dy, right - dx, bottom - dy, "right", dx, dy);
        }
    }

    private static void AssertSame(Func<int, int, int> redness, int x1, int y1, int x2, int y2, string side, int dx, int dy)
    {
        int upper = redness(x1, y1);
        int lower = redness(x2, y2);
        Assert.True(Math.Abs(upper - lower) <= 60,
            $"{side} corner pixel ({dx},{dy}) from the edge: top outline {upper} vs bottom outline {lower}");
    }

    private static Func<int, int, int> Read(WriteableBitmap frame)
    {
        var buffer = new byte[frame.PixelSize.Width * frame.PixelSize.Height * 4];
        using (var locked = frame.Lock())
        {
            int row = frame.PixelSize.Width * 4;
            for (int y = 0; y < frame.PixelSize.Height; y++)
                System.Runtime.InteropServices.Marshal.Copy(locked.Address + y * locked.RowBytes, buffer, y * row, row);
        }
        int width = frame.PixelSize.Width;
        // Frame is BGRA or RGBA; either way R-B magnitude is what matters and only its sign flips.
        return (x, y) =>
        {
            int i = (y * width + x) * 4;
            return Math.Abs(buffer[i + 2] - buffer[i]);
        };
    }
}
