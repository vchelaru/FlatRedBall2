using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AnimationEditor.App.Tests;

/// <summary>Issue #1332: the Project tab's "Show all folders" toggle.</summary>
public class ProjectTabShowAllFoldersTests
{
    [AvaloniaFact]
    public async Task ShowAllFoldersCheck_OpenProjectFolder_ListsFolderWithoutAnimationFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Audio"));
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            window.ProjectPanel.TreeRoots.ShouldBeEmpty();

            window.ProjectPanel.ShowAllFoldersCheck.IsChecked = true;

            window.ProjectPanel.TreeRoots.Select(n => n.Name).ShouldBe(new[] { "Audio" });
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void Startup_PersistedShowAllFoldersOn_ChecksTheToggle()
    {
        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { ShowAllProjectFolders = true }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            window.ProjectPanel.ShowAllFoldersCheck.IsChecked.ShouldBe(true);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Closing_AfterCheckingShowAllFolders_PersistsIt()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.ProjectPanel.ShowAllFoldersCheck.IsChecked.ShouldBe(false);

        window.ProjectPanel.ShowAllFoldersCheck.IsChecked = true;
        window.Close();

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        var settings = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
        settings.ShowAllProjectFolders.ShouldBeTrue();
    }
}
