using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Covers the #452 optimization: a selection change that does not alter the timeline's frame
/// structure must NOT clear-and-rebuild the strip (which would regenerate every thumbnail and
/// reset the playhead VM), while a structural change still rebuilds.
/// </summary>
public class TimelineStripRebuildTests
{
    private static string WritePng(string dir, string name)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(32, 32);
        bm.Erase(SKColors.Green);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static AnimationFrameSave Frame(string texture) => new()
    {
        TextureName      = texture,
        FrameLength      = 0.1f,
        LeftCoordinate   = 0f, TopCoordinate    = 0f,
        RightCoordinate  = 1f, BottomCoordinate = 1f,
        ShapesSave       = new ShapesSave(),
    };

    /// <summary>
    /// The MainWindow constructor restores a blank untitled session, wiping any project assigned
    /// before <c>Show()</c>. Assign the live project after the window is up, then rebuild the tree
    /// from it so selection routing (which checks chain-list membership) sees the chain.
    /// </summary>
    private static void LoadProjectIntoWindow(TestServices ctx, MainWindow window, AnimationChainListSave acls)
    {
        ctx.ProjectManager.AnimationChainListSave = acls;
        typeof(MainWindow)
            .GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void RefreshTimelineStrip_FrameSelectionChangedWithinChain_KeepsCellVmsAndThumbnails()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = WritePng(dir, "sheet.png");
        try
        {
            var ctx = TestHelpers.BuildServices();
            var chain = new AnimationChainSave { Name = "Run" };
            chain.Frames.Add(Frame(png));
            chain.Frames.Add(Frame(png));
            chain.Frames.Add(Frame(png));
            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chain);

            var window = ctx.CreateMainWindow();
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                LoadProjectIntoWindow(ctx, window, acls);

                ctx.SelectedState.SelectedChain = chain;
                Dispatcher.UIThread.RunJobs();

                var timeline = window.FindControl<ItemsControl>("TimelineStrip")!;
                var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timeline.ItemsSource);
                Assert.Equal(3, items.Count);

                // Capture identities before the scrub-like selection change.
                var vm0 = items[0]; var vm1 = items[1]; var vm2 = items[2];
                var thumb0 = items[0].Thumbnail;
                Assert.IsAssignableFrom<Bitmap>(thumb0); // textures resolve, so thumbnails are real bitmaps (WriteableBitmap)

                // Crossing a frame boundary while scrubbing sets SelectedFrame — a non-structural
                // selection change.
                ctx.SelectedState.SelectedFrame = chain.Frames[2];
                Dispatcher.UIThread.RunJobs();

                // Same collection, same VM instances, same thumbnail bitmaps — no rebuild.
                Assert.Same(items, timeline.ItemsSource);
                Assert.Same(vm0, items[0]);
                Assert.Same(vm1, items[1]);
                Assert.Same(vm2, items[2]);
                Assert.Same(thumb0, items[0].Thumbnail);

                // Highlight moved to the newly selected frame.
                Assert.False(items[0].IsCurrent);
                Assert.True(items[2].IsCurrent);
            }
            finally { window.Close(); }
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [AvaloniaFact]
    public void RefreshTimelineStrip_FrameDurationChanged_RebuildsCellVms()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(Frame("a.png"));
        chain.Frames.Add(Frame("b.png"));
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);

        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            LoadProjectIntoWindow(ctx, window, acls);

            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var timeline = window.FindControl<ItemsControl>("TimelineStrip")!;
            var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timeline.ItemsSource);
            var vm0 = items[0];

            // A duration edit changes cell width — a structural change that must rebuild.
            chain.Frames[0].FrameLength = 0.5f;
            ctx.ApplicationEvents.RaiseAnimationChainsChanged();
            Dispatcher.UIThread.RunJobs();

            Assert.NotSame(vm0, items[0]);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectedChainSet_SyncingTreeSelection_RaisesSelectionChangedOnce()
    {
        // Setting the tree's SelectedItem during SyncTreeSelection must not feed back through
        // OnTreeSelectionChanged and re-raise the whole selection cascade (#452 secondary fix).
        var ctx = TestHelpers.BuildServices();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(Frame("a.png"));
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);

        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            LoadProjectIntoWindow(ctx, window, acls);

            int raised = 0;
            ctx.SelectedState.SelectionChanged += () => raised++;

            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, raised);
        }
        finally { window.Close(); }
    }
}
