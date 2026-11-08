using System;
using System.IO;
using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using AnimationEditor.App.Services;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// A chain whose own Loop flag is off plays once in a sequence block, then its last frame is held for the rest of the
/// block's length, so the filmstrip must not start a second cycle. Lives here, not in App.Tests, because it needs real
/// pixels (see <see cref="TestAppBuilder"/>).
/// </summary>
public class SequenceHoldTailTests
{
    [AvaloniaFact]
    public void ANonLoopingChainBlock_ShowsItsLastFrameWhereASecondCycleWouldStart()
    {
        using var frame = Capture(loop: false, "held-tail.png", out var timeline);
        var pixels = Read(frame);
        var (x, y) = SecondCyclePoint(timeline);

        var (r, g, b) = pixels(x, y);

        Assert.True(b > r + 100 && b > g + 100, $"the second cycle's first frame position shows ({r},{g},{b}), not the held last (blue) frame");
    }

    [AvaloniaFact]
    public void ALoopingChainBlock_StillStartsASecondCycleWithItsFirstFrame()
    {
        using var frame = Capture(loop: true, "looping-tail.png", out var timeline);
        var pixels = Read(frame);
        var (x, y) = SecondCyclePoint(timeline);

        var (r, g, b) = pixels(x, y);

        Assert.True(r > g + 100 && r > b + 100, $"the second cycle's first frame position shows ({r},{g},{b}), not the first (red) frame");
    }

    // 0.3 s is where a second 0.3 s cycle would begin; a few pixels in, inside the cell's sprite.
    private static (int X, int Y) SecondCyclePoint(SequenceTimelineControl timeline) =>
        ((int)Math.Round(timeline.Viewport.XAt(0.3) + 4),
         (int)Math.Round(timeline.Viewport.LaneTop(0) + TimelineLayout.FilmTop + TimelineLayout.FilmHeight / 2));

    // Three 0.1 s frames, a red, a green and a blue third of one sheet, played for three cycles (0.9 s).
    private static WriteableBitmap Capture(bool loop, string fileName, out SequenceTimelineControl timeline)
    {
        string dir = Path.Combine(Path.GetTempPath(), "AEHoldTail", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        using (var sheet = new SKBitmap(96, 32))
        {
            using var canvas = new SKCanvas(sheet);
            canvas.DrawRect(0, 0, 32, 32, new SKPaint { Color = SKColors.Red });
            canvas.DrawRect(32, 0, 32, 32, new SKPaint { Color = SKColors.Lime });
            canvas.DrawRect(64, 0, 32, 32, new SKPaint { Color = SKColors.Blue });
            using var image = SKImage.FromBitmap(sheet);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(dir, "sheet.png"), data.ToArray());
        }
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Once", Loop = loop };
        for (int i = 0; i < 3; i++)
        {
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName = "sheet.png", FrameLength = 0.1f,
                LeftCoordinate = i / 3f, RightCoordinate = (i + 1) / 3f, TopCoordinate = 0, BottomCoordinate = 1,
            });
        }
        list.AnimationChains.Add(chain);
        list.Save(Path.Combine(dir, "hero.achx"));
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        var sequence = new SequenceSave { Name = "Idle" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Once", Start = 0, Loops = 3 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir, "boss.seqj");
        save.Save(path);

        timeline = new SequenceTimelineControl { Thumbnails = new ThumbnailService(new ProjectManager()) };
        var window = new Window { Width = 700, Height = 140, Content = timeline };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        timeline.Show(SequenceDocument.Load(path), 0, new UndoManager());
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(ScreenshotOutput.ResolveFeatureDir("sequence-hold-tail"), fileName));
        return frame;
    }

    // The (R, G, B) of the pixel at (x, y), whichever byte order the frame uses.
    private static Func<int, int, (int R, int G, int B)> Read(WriteableBitmap bitmap)
    {
        var buffer = bitmap.Lock();
        var data = new byte[buffer.RowBytes * buffer.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, data, 0, data.Length);
        int stride = buffer.RowBytes;
        bool bgra = buffer.Format == Avalonia.Platform.PixelFormat.Bgra8888;
        buffer.Dispose();
        return (x, y) =>
        {
            int at = y * stride + x * 4;
            return bgra ? (data[at + 2], data[at + 1], data[at]) : (data[at], data[at + 1], data[at + 2]);
        };
    }
}
