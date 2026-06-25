using System.IO;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression guard for issue #438: headless App tests must never read or write the
/// developer's real %APPDATA%\AnimationEditor\AESettings.json. Each <see cref="TestServices"/>
/// owns a unique temp settings root, so constructing and closing a <see cref="MainWindow"/>
/// persists settings there — leaving the production location untouched.
/// </summary>
public class SettingsFileIsolationTests
{
    [AvaloniaFact]
    public void Close_PersistsSettingsUnderInjectedRoot_NotProductionAppData()
    {
        var ctx = TestHelpers.BuildServices();
        var expectedFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);

        var window = ctx.CreateMainWindow();
        window.Show();
        window.Close();   // triggers SaveTabsToSettings -> SaveSettingsFile

        // The save landed under the injected test root. If MainWindow still resolved the
        // production %APPDATA% path, nothing would appear here.
        Assert.True(File.Exists(expectedFile.FullPath),
            $"Expected settings to be saved under the injected test root at {expectedFile.FullPath}");
    }
}
