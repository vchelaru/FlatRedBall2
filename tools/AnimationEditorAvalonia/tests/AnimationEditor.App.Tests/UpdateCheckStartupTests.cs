using System;
using AnimationEditor.App.Services;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for the automatic update flow (issue #982). Desktop startup performs the download in
/// the background, then exposes either a retry-safe failure state or an explicit restart action.
/// </summary>
public class UpdateCheckStartupTests
{
    [AvaloniaFact]
    public void RestartUpdateButton_Clicked_AppliesDownloadedUpdate()
    {
        var ctx = TestHelpers.BuildServices();
        var fake = new FakeApplicationUpdater
        {
            Result = ApplicationUpdateResult.ReadyToRestart(new Version(2026, 7, 17))
        };
        ctx.ApplicationUpdater = fake;
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var restartButton = window.FindControl<Button>("RestartForUpdateBtn");
        restartButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, fake.RestartCount);
    }

    [AvaloniaFact]
    public void Startup_UpdateDownloadFails_ShowsRetryAction()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ApplicationUpdater = new FakeApplicationUpdater
        {
            Result = ApplicationUpdateResult.Failed("The update could not be downloaded.")
        };
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var banner = window.FindControl<Border>("UpdateAvailableBanner");
        var retryButton = window.FindControl<Button>("RetryUpdateBtn");
        Assert.True(banner!.IsVisible);
        Assert.True(retryButton!.IsVisible);
    }

    [AvaloniaFact]
    public void Startup_UpdateDownloadInProgress_ShowsDownloadProgress()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ApplicationUpdater = new FakeApplicationUpdater
        {
            ProgressToReport = 42,
            PendingResult = new TaskCompletionSource<ApplicationUpdateResult>()
        };
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var progressText = window.FindControl<TextBlock>("UpdateAvailableBannerText");
        Assert.Equal("Downloading Animation Editor update (42%)…", progressText!.Text);
    }
}
