using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System.IO;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1045: the window's maximized state must survive a restart. Previously nothing
/// persisted <see cref="WindowState"/> at all, so un-maximizing before close (or closing
/// while maximized) both reopened the window in its default restored size.
/// </summary>
public class WindowMaximizedPersistenceTests
{
    [AvaloniaFact]
    public void Startup_PersistedWindowMaximized_AppliesMaximizedState()
    {
        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { WindowMaximized = true }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(WindowState.Maximized, window.WindowState);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Closing_WhileMaximized_PersistsWindowMaximizedTrue()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.WindowState = WindowState.Maximized;
        window.Close();

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        var settings = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
        Assert.True(settings.WindowMaximized);
    }

    [AvaloniaFact]
    public void Closing_AfterUnMaximizing_PersistsWindowMaximizedFalse()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.WindowState = WindowState.Maximized;
        window.WindowState = WindowState.Normal;
        window.Close();

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        var settings = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
        Assert.False(settings.WindowMaximized);
    }
}
