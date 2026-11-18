using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #875: <c>ProjectManager.ProjectFolderPath</c> is the explicit "open project folder" the
/// Files/Textures panel's Project scope should browse -- not the achx-inferred
/// <c>ResolveFilesPanelRoot()</c>, which returns null with zero .achx tabs open.
/// </summary>
public class ProjectFolderFilesPanelScopeTests
{
    private static void WritePng(string dir, string fileName, SKColor color)
    {
        using var bm = new SKBitmap(8, 8);
        bm.Erase(color);
        using var img = SKImage.FromBitmap(bm);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(dir, fileName), data.ToArray());
    }

    [AvaloniaFact]
    public async Task OpeningProjectFolder_WithNoAchxTabOpen_ListsFolderPngsInFilesPanel()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WritePng(dir, "hero.png", SKColors.Red);

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(window.FilesPanel.TreeRoots);
            Assert.Equal("hero.png", window.FilesPanel.TreeRoots[0].Name);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void Startup_NothingOpen_ProjectScopeFilesPanelShowsOpenProjectFolderMessage()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(FilesPanelScope.Project, window.FilesPanel.Scope);
            Assert.True(window.FilesPanel.EmptyMessage.IsVisible);
            Assert.Equal("Open a project folder to browse its images.", window.FilesPanel.EmptyMessage.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Startup_NothingOpen_ThisFileScopeStillShowsSaveAchxMessage()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.FilesPanel.ScopeThisFileRadio.IsChecked = true;

            Assert.True(window.FilesPanel.EmptyMessage.IsVisible);
            Assert.Equal("Save the .achx to browse folder PNGs.", window.FilesPanel.EmptyMessage.Text);
        }
        finally { window.Close(); }
    }
}
