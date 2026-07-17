using System;
using System.Reflection;
using System.Threading.Tasks;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Update;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for the update-check wiring (issue #681): the silent startup check drives the shared
/// toast, and <c>GetUpdateCheckResultAsync</c> (reached via reflection — there's no non-modal
/// public trigger; the About dialog's forced recheck goes through <c>Window.ShowDialog</c>,
/// which deadlocks headless per the AvaloniaFact landmine) respects the 24h cache window and
/// persists results to <c>AppSettingsModel</c>.
/// </summary>
public class UpdateCheckStartupTests
{
    private static Task<UpdateCheckResult> InvokeGetUpdateCheckResultAsync(MainWindow window, bool forceRefresh)
    {
        var method = typeof(MainWindow).GetMethod("GetUpdateCheckResultAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task<UpdateCheckResult>)method.Invoke(window, new object[] { forceRefresh })!;
    }

    private static AppSettingsModel GetAppSettings(MainWindow window) =>
        (AppSettingsModel)typeof(MainWindow)
            .GetField("_appSettings", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    [AvaloniaFact]
    public void Startup_UpdateAvailable_ShowsToast()
    {
        var ctx = TestHelpers.BuildServices();
        var fake = new FakeUpdateChecker
        {
            Result = new UpdateCheckResult(true, new Version(2026, 7, 17), "https://example.com/latest")
        };
        ctx.UpdateChecker = fake;
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var toastPanel = window.FindControl<Border>("ToastPanel");
        Assert.True(toastPanel!.IsVisible);
    }

    [AvaloniaFact]
    public void Startup_NoUpdate_ToastStaysHidden()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.UpdateChecker = new FakeUpdateChecker { Result = UpdateCheckResult.NoUpdate };
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var toastPanel = window.FindControl<Border>("ToastPanel");
        Assert.False(toastPanel!.IsVisible);
    }

    [AvaloniaFact]
    public async Task GetUpdateCheckResultAsync_WithinCacheWindow_DoesNotRecheck()
    {
        var ctx = TestHelpers.BuildServices();
        var fake = new FakeUpdateChecker { Result = UpdateCheckResult.NoUpdate };
        ctx.UpdateChecker = fake;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var baseline = fake.CallCount; // OnOpened already ran the silent startup check once.

        await InvokeGetUpdateCheckResultAsync(window, forceRefresh: false);
        await InvokeGetUpdateCheckResultAsync(window, forceRefresh: false);

        Assert.Equal(baseline, fake.CallCount);
    }

    [AvaloniaFact]
    public async Task GetUpdateCheckResultAsync_ForceRefresh_AlwaysRechecks()
    {
        var ctx = TestHelpers.BuildServices();
        var fake = new FakeUpdateChecker { Result = UpdateCheckResult.NoUpdate };
        ctx.UpdateChecker = fake;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var baseline = fake.CallCount;

        await InvokeGetUpdateCheckResultAsync(window, forceRefresh: true);
        await InvokeGetUpdateCheckResultAsync(window, forceRefresh: true);

        Assert.Equal(baseline + 2, fake.CallCount);
    }

    [AvaloniaFact]
    public async Task GetUpdateCheckResultAsync_PersistsResultToAppSettings()
    {
        var ctx = TestHelpers.BuildServices();
        var fake = new FakeUpdateChecker
        {
            Result = new UpdateCheckResult(true, new Version(2026, 7, 17), "https://example.com/latest")
        };
        ctx.UpdateChecker = fake;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await InvokeGetUpdateCheckResultAsync(window, forceRefresh: true);

        var settings = GetAppSettings(window);
        Assert.NotNull(settings.LastUpdateCheckUtc);
        Assert.Equal("2026.7.17", settings.LatestKnownUpdateVersion);
        Assert.Equal("https://example.com/latest", settings.LatestKnownUpdateUrl);
    }
}
