using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Multi-track group-preview timeline UI (#576 scope items 5–6): TimelineScrubSurface (single-row)
/// is swapped for GroupTimelineTracks (one row per selected chain) once 2+ chains are selected.
/// </summary>
public class GroupTimelineUiTests
{
    private static AnimationChainSave MakeChain(string name, int frameCount)
    {
        var chain = new AnimationChainSave { Name = name };
        for (int i = 0; i < frameCount; i++)
            chain.Frames.Add(new AnimationFrameSave { TextureName = $"{name}_{i}.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() });
        return chain;
    }

    private static void LoadProjectIntoWindow(TestServices ctx, MainWindow window, AnimationChainListSave acls)
    {
        ctx.ProjectManager.AnimationChainListSave = acls;
        typeof(MainWindow)
            .GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, new object[] { Array.Empty<string>() });
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void RefreshTimelineStrip_TwoChainsSelected_ShowsOneTrackRowPerChain()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 2);
        var b = MakeChain("B", 3);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(a);
        acls.AnimationChains.Add(b);

        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            LoadProjectIntoWindow(ctx, window, acls);

            ctx.SelectedState.SelectedNodes = new List<object> { a, b };
            Dispatcher.UIThread.RunJobs();

            var singleStrip = window.FindControl<Border>("TimelineScrubSurface")!;
            var groupHost = window.FindControl<Border>("GroupTimelineScrubHost")!;
            Assert.False(singleStrip.IsVisible);
            Assert.True(groupHost.IsVisible);

            var tracks = window.FindControl<ItemsControl>("GroupTimelineTracks")!;
            var items = Assert.IsType<ObservableCollection<ChainTimelineTrackVm>>(tracks.ItemsSource);
            Assert.Equal(2, items.Count);
            Assert.Equal("A", items[0].ChainName);
            Assert.Equal("B", items[1].ChainName);
            Assert.Equal(2, items[0].Frames.Count);
            Assert.Equal(3, items[1].Frames.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RefreshTimelineStrip_DropBackToSingleSelection_RestoresSingleRowStrip()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 2);
        var b = MakeChain("B", 2);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(a);
        acls.AnimationChains.Add(b);

        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            LoadProjectIntoWindow(ctx, window, acls);

            ctx.SelectedState.SelectedNodes = new List<object> { a, b };
            Dispatcher.UIThread.RunJobs();

            ctx.SelectedState.SelectedNodes = new List<object>();
            ctx.SelectedState.SelectedChain = a;
            Dispatcher.UIThread.RunJobs();

            var singleStrip = window.FindControl<Border>("TimelineScrubSurface")!;
            var groupHost = window.FindControl<Border>("GroupTimelineScrubHost")!;
            Assert.True(singleStrip.IsVisible);
            Assert.False(groupHost.IsVisible);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Clicking a frame cell in one track's row scrubs only that chain's PlaybackController and
    /// pauses every track, without touching the singular SelectedFrame (#576 scope item 6).
    /// </summary>
    [AvaloniaFact]
    public void ClickingSecondTrackFrameCell_ScrubsOnlyThatChainAndPausesAll()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 2);
        var b = MakeChain("B", 3);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(a);
        acls.AnimationChains.Add(b);

        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            LoadProjectIntoWindow(ctx, window, acls);

            ctx.SelectedState.SelectedNodes = new List<object> { a, b };
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            foreach (var (_, playback) in window.FindControl<AnimationEditor.App.Controls.PreviewControl>("PreviewCtrl")!.GroupTracks) playback.Play();

            var tracks = window.FindControl<ItemsControl>("GroupTimelineTracks")!;
            // Row 1 (chain B)'s frames list, third frame cell (index 2).
            var framesLists = tracks.GetVisualDescendants().OfType<ItemsControl>()
                .Where(ic => ic.Name == "TrackFramesList").ToList();
            Assert.Equal(2, framesLists.Count);
            var bFramesList = framesLists[1];

            var frameCells = bFramesList.GetVisualDescendants().OfType<Grid>()
                .Where(g => g.DataContext is TimelineFrameVm).ToList();
            var thirdCell = frameCells.First(g => ((TimelineFrameVm)g.DataContext!).Index == 2);

            var centre = new Point(thirdCell.Bounds.Width / 2, thirdCell.Bounds.Height / 2);
            var pointInWindow = thirdCell.TranslatePoint(centre, window)!.Value;
            window.MouseDown(pointInWindow, MouseButton.Left);
            window.MouseUp(pointInWindow, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            var trackA = window.FindControl<AnimationEditor.App.Controls.PreviewControl>("PreviewCtrl")!.GroupTracks.First(t => t.Chain == a);
            var trackB = window.FindControl<AnimationEditor.App.Controls.PreviewControl>("PreviewCtrl")!.GroupTracks.First(t => t.Chain == b);
            Assert.Equal(2, trackB.Playback.CurrentFrameIndex);
            Assert.False(trackB.Playback.IsPlaying);
            Assert.False(trackA.Playback.IsPlaying); // scrubbing pauses every track
            Assert.Null(ctx.SelectedState.SelectedFrame); // singular selection untouched
        }
        finally { window.Close(); }
    }
}
