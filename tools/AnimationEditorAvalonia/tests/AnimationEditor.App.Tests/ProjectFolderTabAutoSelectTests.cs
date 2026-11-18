using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Small QoL follow-up to #875: when File → Open Project Folder finds no .achx files, landing on
/// the (now-empty) Animations tab is a dead end. The Images tab is useful immediately in that case
/// since its Project scope (#875) lists PNGs under the folder with zero .achx files needed.
/// </summary>
public class ProjectFolderTabAutoSelectTests
{
    private static void WriteAchx(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new FlatRedBall2.AnimationEditorCommon.AnimationChainListSave();
        acls.Save(path);
    }

    [AvaloniaFact]
    public async Task OpeningProjectFolder_NoAchxFiles_SelectsImagesTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            Dispatcher.UIThread.RunJobs();

            var tabs = window.FindControl<TabControl>("SidebarTabs");
            Assert.NotNull(tabs);
            Assert.Equal("Images", ((TabItem)tabs!.SelectedItem!).Header);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task OpeningProjectFolder_HasAchxFiles_SelectsAnimationsTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WriteAchx(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await ctx.OpenProjectFolderAsync(window, dir);
            Dispatcher.UIThread.RunJobs();

            var tabs = window.FindControl<TabControl>("SidebarTabs");
            Assert.NotNull(tabs);
            Assert.Equal("Animations", ((TabItem)tabs!.SelectedItem!).Header);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
