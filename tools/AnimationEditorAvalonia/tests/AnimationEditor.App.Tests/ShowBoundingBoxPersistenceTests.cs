using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System.IO;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1282: the frame preview's Bounding Box toggle must survive a restart instead of
/// always resetting to checked.
/// </summary>
public class ShowBoundingBoxPersistenceTests
{
    [AvaloniaFact]
    public void Startup_NoSettingsFile_BoundingBoxIsOn()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.True(window.ShowBoundingBoxCheck.IsChecked);
            Assert.True(window.PreviewCtrl.ShowBoundingBox);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Startup_PersistedBoundingBoxOff_AppliesToToggleAndPreview()
    {
        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { ShowBoundingBox = false }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.False(window.ShowBoundingBoxCheck.IsChecked);
            Assert.False(window.PreviewCtrl.ShowBoundingBox);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Closing_AfterTogglingBoundingBoxOff_PersistsIt()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.ShowBoundingBoxCheck.IsChecked = false;
        window.Close();

        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        var settings = JsonSerializer.Deserialize<AppSettingsModel>(File.ReadAllText(settingsFile.FullPath))!;
        Assert.False(settings.ShowBoundingBox);
    }
}
