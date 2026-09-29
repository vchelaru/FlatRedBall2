using System.IO;
using System.Reflection;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies the Open Recent submenu shows abbreviated file names with full-path
/// tooltips and limits to five items.
/// </summary>
public class RecentFilesMenuTests
{
    private static (MainWindow Window, AppSettingsModel AppSettings) CreateWindowWithSettings()
    {
        var ctx    = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        var appSettings = (AppSettingsModel)typeof(MainWindow)
            .GetField("_appSettings", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;
        return (window, appSettings);
    }

    private static void CallRefreshRecentFiles(MainWindow window)
        => typeof(MainWindow)
            .GetMethod("RefreshRecentFiles", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(window, null);

    [AvaloniaFact]
    public void RecentFiles_ShowsAbbreviatedFileNameAsHeader()
    {
        var fullPath = Path.Combine(Path.GetTempPath(), "Games", "MyProject", "player.achx");
        var (window, appSettings) = CreateWindowWithSettings();
        try
        {
            appSettings.RecentFiles.Clear();
            appSettings.RecentFiles.Add(fullPath);
            CallRefreshRecentFiles(window);

            var menuLoadRecent = window.FindControl<MenuItem>("MenuLoadRecent")!;
            var item = (MenuItem)menuLoadRecent.Items[0]!;

            Assert.Equal("player.achx", item.Header?.ToString());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecentFiles_ShowsFullPathAsToolTip()
    {
        var fullPath = Path.Combine(Path.GetTempPath(), "Games", "MyProject", "player.achx");
        var (window, appSettings) = CreateWindowWithSettings();
        try
        {
            appSettings.RecentFiles.Clear();
            appSettings.RecentFiles.Add(fullPath);
            CallRefreshRecentFiles(window);

            var menuLoadRecent = window.FindControl<MenuItem>("MenuLoadRecent")!;
            var item = (MenuItem)menuLoadRecent.Items[0]!;

            Assert.Equal(fullPath, ToolTip.GetTip(item)?.ToString());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecentFiles_LimitsToFiveItems_WhenMoreExist()
    {
        var (window, appSettings) = CreateWindowWithSettings();
        try
        {
            appSettings.RecentFiles.Clear();
            for (int i = 0; i < 10; i++)
                appSettings.RecentFiles.Add(Path.Combine(Path.GetTempPath(), $"file{i}.achx"));
            CallRefreshRecentFiles(window);

            var menuLoadRecent = window.FindControl<MenuItem>("MenuLoadRecent")!;

            Assert.Equal(5, menuLoadRecent.Items.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecentFiles_FewerThanFive_ShowsAllItems()
    {
        var (window, appSettings) = CreateWindowWithSettings();
        try
        {
            appSettings.RecentFiles.Clear();
            appSettings.RecentFiles.Add(Path.Combine(Path.GetTempPath(), "a.achx"));
            appSettings.RecentFiles.Add(Path.Combine(Path.GetTempPath(), "b.achx"));
            CallRefreshRecentFiles(window);

            var menuLoadRecent = window.FindControl<MenuItem>("MenuLoadRecent")!;

            Assert.Equal(2, menuLoadRecent.Items.Count);
        }
        finally { window.Close(); }
    }
}
