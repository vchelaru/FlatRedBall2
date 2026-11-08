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
/// A hidden track keeps its lane and blocks but draws them dimmed (#1643, #1674). The dimming is only pixels, so it is read
/// off a rendered frame (see <see cref="TestAppBuilder"/>): the same block, hidden and not.
/// </summary>
public class TimelineHiddenTrackDimTests
{
    // The pixel of the one block: inside its body just under the top edge, where the block's own fill is; or, selected, on the
    // top edge itself, where the selection layer's 2 px outline is.
    private static int BlockPixel(bool visible, bool selected)
    {
        string dir = Path.Combine(Path.GetTempPath(), "AEHiddenDim", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        for (int i = 0; i < 5; i++) chain.Frames.Add(new AnimationFrameSave { TextureName = "x.png", FrameLength = 0.1f });
        list.AnimationChains.Add(chain);
        list.Save(Path.Combine(dir, "hero.achx"));
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        var sequence = new SequenceSave { Name = "Idle" };
        var track = new SequenceTrackSave { Name = "Body", Visible = visible };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0.5 });
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
        if (selected) timeline.Select(new TimelineChainId(0, 0), null);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()!;

        int x = (int)Math.Round(timeline.Viewport.XAt(0.5) + 0.1 * timeline.Viewport.PixelsPerSecond * 2.5);
        int y = (int)Math.Round(timeline.Viewport.LaneTop(0) + TimelineLayout.BodyTop + (selected ? 0 : 3));
        var buffer = frame.Lock();
        var data = new byte[buffer.RowBytes * buffer.Size.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, data, 0, data.Length);
        int pixel = BitConverter.ToInt32(data, y * buffer.RowBytes + x * 4);
        buffer.Dispose();
        window.Close();
        return pixel;
    }

    [AvaloniaFact]
    public void ABlockOnAHiddenTrack_IsDrawnDimmerThanTheSameBlockOnAVisibleOne()
    {
        int shown = BlockPixel(visible: true, selected: false);
        int hidden = BlockPixel(visible: false, selected: false);

        Assert.NotEqual(shown, hidden);
    }

    [AvaloniaFact]
    public void TheSelectedOutlineOfAHiddenTracksBlock_IsDimmedToo()
    {
        int shown = BlockPixel(visible: true, selected: true);
        int hidden = BlockPixel(visible: false, selected: true);

        Assert.NotEqual(shown, hidden);
    }
}
