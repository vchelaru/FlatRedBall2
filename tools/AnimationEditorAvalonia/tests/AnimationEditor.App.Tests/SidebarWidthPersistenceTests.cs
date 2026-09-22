using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1178: the left sidebar's dragged column width must survive a restart instead of
/// always resetting to the fixed 300px default.
/// </summary>
public class SidebarWidthPersistenceTests
{
    [AvaloniaFact]
    public void Startup_PersistedSidebarWidth_AppliesToMainContentGridColumn()
    {
        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { SidebarWidth = 420.0 }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(420.0, window.MainContentGrid.ColumnDefinitions[0].Width.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Closing_AfterResizingSidebar_PersistsColumnWidth()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Stand in for a real GridSplitter drag, which just changes the column's Width.
        window.MainContentGrid.ColumnDefinitions[0].Width = new GridLength(410.0, GridUnitType.Pixel);
        window.Close();

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        var settings = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
        Assert.Equal(410.0, settings.SidebarWidth);
    }
}
