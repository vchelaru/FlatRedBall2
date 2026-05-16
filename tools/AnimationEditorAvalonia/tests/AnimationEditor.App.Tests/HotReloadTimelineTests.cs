using System.Collections.ObjectModel;
using System.Reflection;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that when <c>OnPngChangedOnDisk</c> fires the timeline strip
/// thumbnails are refreshed from the newly-written PNG, not left stale.
/// </summary>
public class HotReloadTimelineTests
{
    private static string WriteSolidPng(string dir, string name, SKColor color, int size = 16)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static void InvokeOnPngChangedOnDisk(MainWindow window, string path)
    {
        var method = typeof(MainWindow).GetMethod(
            "OnPngChangedOnDisk", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("OnPngChangedOnDisk not found");
        method.Invoke(window, [path]);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void OnPngChangedOnDisk_RefreshesTimelineStrip_WithUpdatedThumbnails()
    {
        // Arrange — a chain with two frames pointing at the same PNG on disk.
        var ctx = TestHelpers.BuildServices();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var pngPath = WriteSolidPng(dir, "sprite.png", SKColors.Red);

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName    = pngPath,
                FrameLength    = 0.1f,
                LeftCoordinate = 0f, TopCoordinate    = 0f,
                RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave     = new ShapesSave(),
            });
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName    = pngPath,
                FrameLength    = 0.1f,
                LeftCoordinate = 0f, TopCoordinate    = 0f,
                RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave     = new ShapesSave(),
            });

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chain);
            ctx.ProjectManager.AnimationChainListSave = acls;
            ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

            var window = ctx.CreateMainWindow();
            window.Show();

            // Select the chain so the timeline is populated
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var timelineStrip = window.FindControl<ItemsControl>("TimelineStrip")
                ?? throw new InvalidOperationException("TimelineStrip not found");
            var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timelineStrip.ItemsSource);

            Assert.Equal(2, items.Count);

            // Capture the original thumbnails
            var originalThumb0 = items[0].Thumbnail;
            var originalThumb1 = items[1].Thumbnail;

            // Act — write a different PNG to disk, then fire the hot-reload handler
            WriteSolidPng(dir, "sprite.png", SKColors.Blue);
            InvokeOnPngChangedOnDisk(window, pngPath);

            // Assert — timeline still has same frame count (not wiped) and thumbnails were regenerated
            Assert.Equal(2, items.Count);
            Assert.NotNull(items[0].Thumbnail);
            Assert.NotNull(items[1].Thumbnail);
            // The old Bitmap objects should have been disposed and replaced
            Assert.NotSame(originalThumb0, items[0].Thumbnail);
            Assert.NotSame(originalThumb1, items[1].Thumbnail);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [AvaloniaFact]
    public void OnPngChangedOnDisk_TimelineNotEmpty_WhenChainHasFrames()
    {
        // Simpler check: the timeline must not be empty after hot-reload, even if no
        // previous thumbnail existed for the new texture.
        var ctx = TestHelpers.BuildServices();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var pngPath = WriteSolidPng(dir, "anim.png", SKColors.Green);

            var chain = new AnimationChainSave { Name = "Idle" };
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName     = pngPath,
                FrameLength     = 0.1f,
                LeftCoordinate  = 0f, TopCoordinate    = 0f,
                RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave      = new ShapesSave(),
            });

            var acls = new AnimationChainListSave();
            acls.AnimationChains.Add(chain);
            ctx.ProjectManager.AnimationChainListSave = acls;
            ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

            var window = ctx.CreateMainWindow();
            window.Show();

            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var timelineStrip = window.FindControl<ItemsControl>("TimelineStrip")
                ?? throw new InvalidOperationException("TimelineStrip not found");
            var items = Assert.IsType<ObservableCollection<TimelineFrameVm>>(timelineStrip.ItemsSource);
            Assert.Single(items);

            // Replace PNG and fire hot-reload
            WriteSolidPng(dir, "anim.png", SKColors.Yellow);
            InvokeOnPngChangedOnDisk(window, pngPath);

            Assert.Single(items); // must not become empty
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
