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
    public void RecentProjectFolders_ListedAfterFilesWithSeparator_AndOmittedWhenEmpty()
    {
        var (window, appSettings) = CreateWindowWithSettings();
        try
        {
            appSettings.RecentFiles.Clear();
            appSettings.RecentProjectFolders.Clear();
            appSettings.RecentFiles.Add(Path.Combine(Path.GetTempPath(), "a.achx"));
            CallRefreshRecentFiles(window);
            var menu = window.FindControl<MenuItem>("MenuLoadRecent")!;
            Assert.Single(menu.Items);

            appSettings.RecentProjectFolders.Add(Path.Combine(Path.GetTempPath(), "ProjA"));
            appSettings.RecentProjectFolders.Add(Path.Combine(Path.GetTempPath(), "ProjB"));
            CallRefreshRecentFiles(window);

            Assert.Equal(4, menu.Items.Count);
            Assert.IsType<MenuItem>(menu.Items[0]);
            Assert.IsType<Separator>(menu.Items[1]);
            Assert.Equal("ProjA", ((MenuItem)menu.Items[2]!).Header?.ToString());
            Assert.Equal(appSettings.RecentProjectFolders[1], ToolTip.GetTip((MenuItem)menu.Items[3]!)?.ToString());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RecentProjectFolders_OnlyFolders_HasNoLeadingSeparator()
    {
        var (window, appSettings) = CreateWindowWithSettings();
        try
        {
            appSettings.RecentFiles.Clear();
            appSettings.RecentProjectFolders.Clear();
            appSettings.RecentProjectFolders.Add(Path.Combine(Path.GetTempPath(), "ProjA"));
            CallRefreshRecentFiles(window);

            var menu = window.FindControl<MenuItem>("MenuLoadRecent")!;
            Assert.Single(menu.Items);
            Assert.IsType<MenuItem>(menu.Items[0]);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async System.Threading.Tasks.Task RecentProjectFolder_Click_OpensFolderAndMovesItToFront()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, appSettings) = CreateWindowWithSettings();
        try
        {
            appSettings.RecentFiles.Clear();
            appSettings.RecentProjectFolders.Clear();
            appSettings.RecentProjectFolders.Add(Path.Combine(Path.GetTempPath(), "other"));
            appSettings.RecentProjectFolders.Add(dir);
            CallRefreshRecentFiles(window);

            var menu = window.FindControl<MenuItem>("MenuLoadRecent")!;
            ((MenuItem)menu.Items[1]!).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            for (int i = 0; i < 100 && appSettings.LastProjectFolderPath != dir; i++)
            {
                await System.Threading.Tasks.Task.Delay(50);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }

            Assert.Equal(dir, appSettings.RecentProjectFolders[0]);
            Assert.Equal(dir, appSettings.LastProjectFolderPath);
            Assert.Equal(dir, ToolTip.GetTip((MenuItem)menu.Items[0]!)?.ToString());
        }
        finally { window.Close(); Directory.Delete(dir, true); }
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
