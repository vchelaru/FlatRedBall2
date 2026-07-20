using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #772 follow-up: the folder picked via File → Open Project Folder (#770) is remembered
/// and rescanned on the next launch, so the Project tab doesn't start empty every time.
/// </summary>
public class ProjectFolderPersistenceTests
{
    private static void WriteAchx(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new FlatRedBall2.Animation.Content.AnimationChainListSave();
        acls.Save(path);
    }

    private static AppSettingsModel ReadPersistedSettings(TestServices ctx)
    {
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Assert.True(File.Exists(settingsFile.FullPath),
            $"Expected settings to be persisted at {settingsFile.FullPath}.");
        return JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
    }

    [AvaloniaFact]
    public async Task OpeningProjectFolder_PersistsPathWithoutWindowClose()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WriteAchx(dir, "hero.achx");
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            await window.OpenProjectFolderForTestAsync(dir);
            Dispatcher.UIThread.RunJobs();

            var settings = ReadPersistedSettings(ctx);
            Assert.Equal(dir, settings.LastProjectFolderPath);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task Startup_LastProjectFolderPresent_RepopulatesProjectTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WriteAchx(dir, "hero.achx");
        WriteAchx(dir, "enemy.achx");

        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { LastProjectFolderPath = dir }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, window.ProjectPanel.TreeRoots.Count);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void Startup_LastProjectFolderMissing_LeavesProjectTabEmpty()
    {
        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel
            {
                LastProjectFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
            }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(window.ProjectPanel.TreeRoots);
        }
        finally { window.Close(); }
    }
}
