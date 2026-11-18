using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #843: a <c>.achx</c> sitting in the Project tree that was never opened as a tab had zero
/// watch coverage before this -- only the currently active tab's <c>HotReloadWatcher</c> caught
/// external changes. These prove <c>MainWindow</c>'s new <c>_projectFolderWatcher</c> covers
/// everything else still visible in the tree: an unopened tracked file's thumbnail, a new file
/// appearing, and a tracked file disappearing.
/// </summary>
public class ProjectFolderExternalWatchTests
{
    private static void WritePng(string dir, string fileName, SKColor color)
    {
        using var bm = new SKBitmap(8, 8);
        bm.Erase(color);
        using var img = SKImage.FromBitmap(bm);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(dir, fileName), data.ToArray());
    }

    private static void WriteAchxWithFrame(string path, string textureName)
    {
        var acls = new FlatRedBall2.AnimationEditorCommon.AnimationChainListSave();
        acls.AnimationChains.Add(new FlatRedBall2.AnimationEditorCommon.AnimationChainSave
        {
            Name = "Walk",
            Frames = { new FlatRedBall2.AnimationEditorCommon.AnimationFrameSave { TextureName = textureName } },
        });
        acls.Save(path);
    }

    /// <summary>
    /// The real trigger here is a background <c>FileSystemWatcher</c> + its debounce timer, not a
    /// synchronous in-process call -- unlike e.g. <c>ProjectFolderPersistenceTests</c>'s save test,
    /// a single <c>RunJobs()</c> right after the write can't drain anything because nothing has
    /// been queued to the UI dispatcher yet. Poll: give the real watcher time to fire, then drain
    /// whatever it queued, until <paramref name="condition"/> holds or <paramref name="timeout"/>
    /// elapses.
    /// </summary>
    private static async Task PumpUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(50);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task ExternalCreate_NewAchxInWatchedFolder_AppearsInProjectTree()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WriteAchxWithFrame(Path.Combine(dir, "hero.achx"), "hero.png");

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(window.ProjectPanel.TreeRoots);

            WriteAchxWithFrame(Path.Combine(dir, "enemy.achx"), "hero.png");

            await PumpUntilAsync(() => window.ProjectPanel.TreeRoots.Count == 2, TimeSpan.FromSeconds(5));

            Assert.Equal(2, window.ProjectPanel.TreeRoots.Count);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ExternalDelete_TrackedAchx_RemovedFromProjectTree()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achxPath = Path.Combine(dir, "hero.achx");
        WriteAchxWithFrame(achxPath, "hero.png");

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(window.ProjectPanel.TreeRoots);

            File.Delete(achxPath);

            await PumpUntilAsync(() => window.ProjectPanel.TreeRoots.Count == 0, TimeSpan.FromSeconds(5));

            Assert.Empty(window.ProjectPanel.TreeRoots);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ExternalEdit_ToUnopenedTrackedFile_RefreshesItsProjectTreeThumbnail()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WritePng(dir, "hero.png", SKColors.Red);
        WritePng(dir, "other.png", SKColors.Blue);
        var achxPath = Path.Combine(dir, "hero.achx");
        WriteAchxWithFrame(achxPath, "hero.png");

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            Dispatcher.UIThread.RunJobs();
            await window.ProjectPanel.ThumbnailLoadTask;
            var beforeEdit = window.ProjectPanel.TreeRoots[0].Thumbnail;
            Assert.NotNull(beforeEdit);

            // Rewrite the file externally -- never loaded into ProjectManager, never opened as a
            // tab. Only _projectFolderWatcher can see this.
            WriteAchxWithFrame(achxPath, "other.png");

            await PumpUntilAsync(
                () => !ReferenceEquals(window.ProjectPanel.TreeRoots[0].Thumbnail, beforeEdit),
                TimeSpan.FromSeconds(5));

            var afterEdit = window.ProjectPanel.TreeRoots[0].Thumbnail;
            Assert.NotNull(afterEdit);
            Assert.NotSame(beforeEdit, afterEdit);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
