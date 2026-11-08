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
/// The hover card and the eye tooltip are the topmost thing on the timeline: the selection layer's outline and
/// the playhead layer all draw beneath them. Real pixels, so it lives here (see <see cref="TestAppBuilder"/>).
/// </summary>
public class TimelinePopupLayerTests
{
    private sealed class Rig
    {
        public required SequenceTimelineControl Timeline { get; init; }
        public required Window Window { get; init; }
    }

    // Two tracks, each holding "Walk" (five 0.1 s frames) from 0 s. Hosted like MainWindow does (the playhead and popup layers sit
    // over the timeline in one grid cell), or, with hostLayers false, as a timeline on its own.
    private static Rig Host(bool hostLayers = true)
    {
        string dir = Path.Combine(Path.GetTempPath(), "AEPopupLayer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        for (int i = 0; i < 5; i++) chain.Frames.Add(new AnimationFrameSave { TextureName = "x.png", FrameLength = 0.1f });
        list.AnimationChains.Add(chain);
        list.Save(Path.Combine(dir, "hero.achx"));
        var save = new SequenceListSave();
        save.Sources.Add("hero.achx");
        var sequence = new SequenceSave { Name = "Idle" };
        foreach (string name in new[] { "Top", "Bottom" })
        {
            var track = new SequenceTrackSave { Name = name };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0 });
            sequence.Tracks.Add(track);
        }
        save.Sequences.Add(sequence);
        string path = Path.Combine(dir, "boss.seqj");
        save.Save(path);

        var timeline = new SequenceTimelineControl();
        var playhead = new TimelinePlayheadOverlay { IsHitTestVisible = false };
        var popups = new TimelinePopupOverlay { IsHitTestVisible = false };
        var grid = new Grid();
        grid.Children.Add(timeline);
        if (hostLayers)
        {
            grid.Children.Add(playhead);
            grid.Children.Add(popups);
        }
        var window = new Window { Width = 600, Height = 200, Content = grid };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        timeline.Show(SequenceDocument.Load(path), 0, new UndoManager());
        if (hostLayers)
        {
            playhead.Attach(timeline);
            popups.Attach(timeline);
        }
        Dispatcher.UIThread.RunJobs();
        return new Rig { Timeline = timeline, Window = window };
    }

    // Hover over the top chain's filmstrip at 0.25 s. The card opens below and right of the pointer, over the bottom lane.
    private static Point HoverTopFilmstrip(Rig rig)
    {
        var viewport = rig.Timeline.Viewport;
        var point = new Point(viewport.XAt(0.25), viewport.LaneTop(0) + TimelineLayout.FilmTop + TimelineLayout.FilmHeight / 2);
        rig.Window.MouseMove(point);
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(rig.Timeline.HoverCard);
        return point;
    }

    private static WriteableBitmap Frame(Rig rig, string name)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = rig.Window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(ScreenshotOutput.ResolveFeatureDir("popup-layer"), name + ".png"));
        return frame;
    }

    [AvaloniaFact]
    public void TheHoverCard_DrawsOverASelectedChainsOutline() => AssertCardOverOutline(Host());

    [AvaloniaFact]
    public void TheHoverCard_DrawsOverASelectedChainsOutline_OnATimelineWithNoHostedLayers() => AssertCardOverOutline(Host(hostLayers: false));

    private static void AssertCardOverOutline(Rig rig)
    {
        var point = HoverTopFilmstrip(rig);
        // Where the bottom chain's selected outline (2 px, its top edge) runs under the card's left margin.
        int x = (int)Math.Round(point.X + 14 + 3);
        int y = (int)Math.Round(rig.Timeline.Viewport.LaneTop(1) + TimelineLayout.BodyTop);

        using var without = Frame(rig, "card-unselected");
        int expected = Read(without)(x, y);
        rig.Timeline.Select(new TimelineChainId(1, 0), null);
        Dispatcher.UIThread.RunJobs();
        using var with = Frame(rig, "card-over-selected");
        int actual = Read(with)(x, y);

        Assert.True(expected == actual, $"the selected chain's outline paints over the hover card at ({x}, {y}): {actual:X8} instead of the card's {expected:X8}");
    }

    [AvaloniaFact]
    public void TheHoverCard_DrawsOverThePlayhead()
    {
        var rig = Host();
        var point = HoverTopFilmstrip(rig);
        int x = (int)Math.Round(point.X + 14 + 3);
        int y = (int)Math.Round(rig.Timeline.Viewport.LaneTop(1) + 20);

        using var without = Frame(rig, "card-playhead-away");
        int expected = Read(without)(x, y);
        rig.Timeline.Playhead = rig.Timeline.Viewport.TimeAt(x);
        Dispatcher.UIThread.RunJobs();
        using var with = Frame(rig, "card-over-playhead");
        int actual = Read(with)(x, y);

        Assert.True(expected == actual, $"the playhead paints over the hover card at ({x}, {y}): {actual:X8} instead of the card's {expected:X8}");
    }

    [AvaloniaFact]
    public void TheEyeTooltip_DrawsOverThePlayheadAndASelectedChainsOutline()
    {
        var rig = Host();
        var viewport = rig.Timeline.Viewport;
        rig.Window.MouseMove(new Point(TimelineLayout.EyeCenterX, viewport.LaneTop(0) + TimelineLayout.EyeCenterY));
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(rig.Timeline.EyeTooltip);
        // The pill opens below and right of the pointer, over the bottom lane's chain, from x = 23 to about 190.
        int x = 150;
        int y = (int)Math.Round(viewport.LaneTop(1) + TimelineLayout.BodyTop);

        using var without = Frame(rig, "tooltip-plain");
        int expected = Read(without)(x, y);
        rig.Timeline.Select(new TimelineChainId(1, 0), null);
        rig.Timeline.Playhead = viewport.TimeAt(x);
        Dispatcher.UIThread.RunJobs();
        using var with = Frame(rig, "tooltip-over-selected-and-playhead");
        int actual = Read(with)(x, y);

        Assert.True(expected == actual, $"the outline or the playhead paints over the eye tooltip at ({x}, {y}): {actual:X8} instead of the tooltip's {expected:X8}");
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
