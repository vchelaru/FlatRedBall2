using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Startup warm-up work runs on a thread pool thread while Avalonia is still initializing, so it
/// must work without a UI thread, and the window must pick up what it preloaded.
/// </summary>
public class StartupWarmupTests
{
    [Fact]
    public async Task WarmSvg_OnAThreadPoolThread_LoadsTheSvgStack()
    {
        // The warm-up swallows failures so a broken SVG can't stop startup; this is what stops
        // it from silently turning into a no-op.
        Assert.True(await Task.Run(StartupWarmup.WarmSvg));
    }

    [AvaloniaFact]
    public void Startup_UsesSettingsPreloadedForItsPath()
    {
        var ctx = TestHelpers.BuildServices();
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(ctx.SettingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { PreviewPaneHeight = 410.0 }));
        StartupWarmup.PreloadSettings(settingsFile).Completion.GetAwaiter().GetResult();

        // Changed after the preload read it: only the preloaded copy can still say 410.
        File.WriteAllText(settingsFile.FullPath,
            JsonSerializer.Serialize(new AppSettingsModel { PreviewPaneHeight = 300.0 }));

        var window = ctx.CreateMainWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(410.0, window.AchxEditorPane.RowDefinitions[3].Height.Value);
        }
        finally { window.Close(); }
    }
}
