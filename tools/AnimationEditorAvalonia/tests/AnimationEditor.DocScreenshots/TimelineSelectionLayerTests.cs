using System;
using System.IO;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// The selection is drawn by its own layer above the timeline's content. It must still show: a selected chain gets the
/// thick outline, an unselected one the thin one. Lives here, not in App.Tests, because it needs real pixels (see <see cref="TestAppBuilder"/>).
/// </summary>
public class TimelineSelectionLayerTests
{
    [AvaloniaFact]
    public void ASelectedChain_IsOutlinedByTheSelectionLayer_AndAnUnselectedOneIsNot()
    {
        string dir = Path.Combine(Path.GetTempPath(), "AESelectionLayer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        for (int i = 0; i < 5; i++) chain.Frames.Add(new AnimationFrameSave { TextureName = "x.png", FrameLength = 0.1f });
        list.AnimationChains.Add(chain);
        list.Save(Path.Combine(dir, "hero.achx"));
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        var sequence = new SequenceSave { Name = "Idle" };
        var track = new SequenceTrackSave { Name = "Body" };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0.5 });
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 2.0 });
        sequence.Tracks.Add(track);
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir, "boss.seqj");
        save.Save(path);

        var timeline = new SequenceTimelineControl();
        var window = new Window { Width = 600, Height = 140, Content = timeline };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        timeline.Show(SequenceDocument.Load(path), 0, new UndoManager());
        Dispatcher.UIThread.RunJobs();
        timeline.Select(new TimelineChainId(0, 0), null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(ScreenshotOutput.ResolveFeatureDir("selection-layer"), "selected-and-not.png"));

        // The top edge of each body, in the middle: the selected chain's 2 px outline is the Ink color, the other's 1 px one is not.
        double top = timeline.Viewport.LaneTop(0) + TimelineLayout.BodyTop;
        int selectedX = (int)Math.Round(timeline.Viewport.XAt(0.5) + 0.1 * timeline.Viewport.PixelsPerSecond * 2.5);
        int otherX = (int)Math.Round(timeline.Viewport.XAt(2.0) + 0.1 * timeline.Viewport.PixelsPerSecond * 2.5);
        var pixels = Read(frame);
        int selectedEdge = pixels(selectedX, (int)Math.Round(top));
        int otherEdge = pixels(otherX, (int)Math.Round(top));

        Assert.True(selectedEdge != otherEdge, $"the selected and the unselected chain have the same outline pixel ({selectedEdge}); the selection layer drew nothing");
    }

    // Packed pixel at (x, y).
    private static Func<int, int, int> Read(WriteableBitmap bitmap)
    {
        var buffer = bitmap.Lock();
        var data = new byte[buffer.RowBytes * buffer.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, data, 0, data.Length);
        int stride = buffer.RowBytes;
        buffer.Dispose();
        return (x, y) => BitConverter.ToInt32(data, y * stride + x * 4);
    }
}
