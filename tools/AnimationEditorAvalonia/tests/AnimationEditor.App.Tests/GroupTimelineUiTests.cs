using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
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

    /// <summary>
    /// Live bug found while investigating the multi-select freeze (issue: selecting many chains
    /// stayed slow for ~1-2s even after the tree-selection burst itself was coalesced): this
    /// method's own doc comment says it should only run when the group's chain membership
    /// actually changes, but <c>RefreshTimelineStrip</c> calls it on EVERY refresh regardless --
    /// any unrelated selection-changed dispatch while a large group is active re-rebuilds every
    /// row's frame VMs and thumbnails for nothing (measured: 40 chains, several ~20-35ms redundant
    /// rebuilds back to back).
    /// </summary>
    [AvaloniaFact]
    public void RefreshTimelineStrip_CalledAgainWithUnchangedGroupMembership_DoesNotRebuildTrackRows()
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

            var tracks = window.FindControl<ItemsControl>("GroupTimelineTracks")!;
            var items = Assert.IsType<ObservableCollection<ChainTimelineTrackVm>>(tracks.ItemsSource);
            Assert.Equal(2, items.Count);
            var firstBuildRowA = items[0];
            var firstBuildRowB = items[1];

            // Re-trigger the refresh path (mirrors an unrelated selection-changed dispatch, e.g.
            // RouteNodeSelection's second SelectionChanged fire) without the group's chain
            // membership actually changing.
            typeof(MainWindow).GetMethod("RefreshTimelineStrip", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, null);

            Assert.Equal(2, items.Count);
            Assert.Same(firstBuildRowA, items[0]); // not rebuilt -- same row VM instance
            Assert.Same(firstBuildRowB, items[1]);
        }
        finally { window.Close(); }
    }

    /// <summary>The skip-when-unchanged guard above must not suppress a REAL membership change --
    /// growing the group from 2 to 3 chains must still rebuild with the new row.</summary>
    [AvaloniaFact]
    public void RefreshTimelineStrip_GroupMembershipGrows_StillRebuildsWithNewRow()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A", 2);
        var b = MakeChain("B", 3);
        var c = MakeChain("C", 1);
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(a);
        acls.AnimationChains.Add(b);
        acls.AnimationChains.Add(c);

        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            LoadProjectIntoWindow(ctx, window, acls);

            ctx.SelectedState.SelectedNodes = new List<object> { a, b };
            Dispatcher.UIThread.RunJobs();

            ctx.SelectedState.SelectedNodes = new List<object> { a, b, c };
            Dispatcher.UIThread.RunJobs();

            var tracks = window.FindControl<ItemsControl>("GroupTimelineTracks")!;
            var items = Assert.IsType<ObservableCollection<ChainTimelineTrackVm>>(tracks.ItemsSource);
            Assert.Equal(3, items.Count);
            Assert.Equal("C", items[2].ChainName);
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
    /// Growing the timeline dock for a 2+ chain group selection must never resize or reposition
    /// the preview canvas — the dock is an overlay on top of the canvas, not a sibling row that
    /// competes with it for space (reported: selecting a 2nd animation visibly shifted the
    /// centered preview).
    /// </summary>
    [AvaloniaFact]
    public void RefreshTimelineStrip_TimelineDockGrows_PreviewCanvasBoundsUnchanged()
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

            ctx.SelectedState.SelectedChain = a;
            Dispatcher.UIThread.RunJobs();

            var previewCtrl = window.FindControl<AnimationEditor.App.Controls.PreviewControl>("PreviewCtrl")!;
            var timelineDock = window.FindControl<Grid>("TimelineDockGrid")!;
            var singleHeight = previewCtrl.Bounds.Height;
            var singleDockHeight = timelineDock.Height;

            ctx.SelectedState.SelectedNodes = new List<object> { a, b };
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(singleHeight, previewCtrl.Bounds.Height);
            Assert.NotEqual(singleDockHeight, timelineDock.Height);
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
