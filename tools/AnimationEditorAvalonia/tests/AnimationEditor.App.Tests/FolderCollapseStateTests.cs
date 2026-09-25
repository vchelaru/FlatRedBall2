using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issues #1207 (Animations tab) and #1209 (Images tab): a folder the user collapsed stays
/// collapsed when the tree rebuilds after a disk change, and across closing and reopening the editor.
/// </summary>
public class FolderCollapseStateTests
{
    private static void WritePng(string path)
    {
        using var bm = new SKBitmap(8, 8);
        bm.Erase(SKColors.Red);
        using var img = SKImage.FromBitmap(bm);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private static void WriteAchx(string path) =>
        new FlatRedBall2.AnimationEditorCommon.AnimationChainListSave().Save(path);

    // The rebuild is driven by a real FileSystemWatcher + debounce, so poll instead of one RunJobs.
    private static async Task PumpUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(50);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static string CreateProjectWithSubfolder(string subfolderName)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, subfolderName));
        WriteAchx(Path.Combine(dir, subfolderName, "hero.achx"));
        WritePng(Path.Combine(dir, subfolderName, "hero.png"));
        return dir;
    }

    [AvaloniaFact]
    public async Task AnimationsTab_FileAddedOnDisk_CollapsedFolderStaysCollapsed()
    {
        var dir = CreateProjectWithSubfolder("enemies");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();
            window.ProjectPanel.TreeRoots.Single().IsExpanded = false;

            WriteAchx(Path.Combine(dir, "enemies", "goblin.achx"));
            await PumpUntilAsync(() => window.ProjectPanel.TreeRoots.Single().Children.Count == 2);

            var folder = window.ProjectPanel.TreeRoots.Single();
            folder.Children.Count.ShouldBe(2);
            folder.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ImagesTab_PngAddedOnDisk_CollapsedFolderStaysCollapsed()
    {
        var dir = CreateProjectWithSubfolder("enemies");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();
            window.FilesPanel.TreeRoots.Single().IsExpanded = false;

            WritePng(Path.Combine(dir, "enemies", "goblin.png"));
            await PumpUntilAsync(() => window.FilesPanel.TreeRoots.Single().Children.Count == 2);

            var folder = window.FilesPanel.TreeRoots.Single();
            folder.Children.Count.ShouldBe(2);
            folder.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task Reopen_CollapsedFoldersInBothTabs_RestoredCollapsed()
    {
        var dir = CreateProjectWithSubfolder("enemies");
        var ctx = TestHelpers.BuildServices();
        var first = ctx.CreateMainWindow();
        first.Show();
        await first.OpenProjectFolderForTestAsync(dir);
        Dispatcher.UIThread.RunJobs();
        first.ProjectPanel.TreeRoots.Single().IsExpanded = false;
        first.FilesPanel.TreeRoots.Single().IsExpanded = false;
        first.Close();

        var second = ctx.CreateMainWindow();
        try
        {
            second.Show();
            Dispatcher.UIThread.RunJobs();

            second.ProjectPanel.TreeRoots.Single().IsExpanded.ShouldBeFalse();
            second.FilesPanel.TreeRoots.Single().IsExpanded.ShouldBeFalse();
        }
        finally
        {
            second.Close();
            Directory.Delete(dir, true);
        }
    }
}
