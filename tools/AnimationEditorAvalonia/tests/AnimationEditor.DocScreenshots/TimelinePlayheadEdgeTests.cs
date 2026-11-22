using System;
using System.IO;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// The playhead at 0 s sits on the left edge of a block that starts at 0. It is translucent so that edge still shows through;
/// the translucency is only pixels, so it is read off a rendered frame (see <see cref="TestAppBuilder"/>).
/// </summary>
public class TimelinePlayheadEdgeTests
{
    [AvaloniaFact]
    public void ThePlayheadOverABlockEdge_IsDrawnDifferentlyThanOverEmptyLane()
    {
        string dir = Path.Combine(Path.GetTempPath(), "AEPlayheadEdge", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        for (int i = 0; i < 4; i++) chain.Frames.Add(new AnimationFrameSave { TextureName = "x.png", FrameLength = 0.25f });
        list.AnimationChains.Add(chain);
        list.Save(Path.Combine(dir, "hero.achx"));
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        var sequence = new SequenceSave { Name = "Idle" };
        foreach (var (name, start) in new[] { ("Body", 0.0), ("Flame", 0.5) })
        {
            var track = new SequenceTrackSave { Name = name };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = start });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir, "boss.seqj");
        save.Save(path);

        var timeline = new SequenceTimelineControl();
        var window = new Window { Width = 420, Height = 200, Content = timeline };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        timeline.Show(SequenceDocument.Load(path), 0, new UndoManager());
        timeline.Playhead = 0;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()!;

        int x = (int)Math.Round(timeline.Viewport.XAt(0));
        int overBlockEdge = Pixel(frame, x, (int)(timeline.Viewport.LaneTop(0) + TimelineLayout.BodyTop + 20));
        int overEmpty = Pixel(frame, x, (int)(timeline.Viewport.LaneTop(1) + TimelineLayout.BodyTop + 20));
        window.Close();

        Assert.NotEqual(overBlockEdge, overEmpty);
    }

    private static int Pixel(Avalonia.Media.Imaging.Bitmap frame, int x, int y)
    {
        using var buffer = ((Avalonia.Media.Imaging.WriteableBitmap)frame).Lock();
        var data = new byte[buffer.RowBytes * buffer.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, data, 0, data.Length);
        return BitConverter.ToInt32(data, y * buffer.RowBytes + x * 4);
    }
}
