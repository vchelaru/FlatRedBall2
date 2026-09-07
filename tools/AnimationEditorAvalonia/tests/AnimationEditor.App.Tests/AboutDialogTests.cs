using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AnimationEditor.App.Services;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for the About dialog (issue #194): centered on owner,
/// non-resizable, correct title, version number from assembly, and Releases link.
/// </summary>
public class AboutDialogTests
{
    [AvaloniaFact]
    public void BuildAboutContent_ContainsVersionTextBlock()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent();

        var versionBlock = panel.Children
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text?.StartsWith("Version") == true);

        Assert.NotNull(versionBlock);
    }

    [AvaloniaFact]
    public void BuildAboutContent_VersionText_MatchesThreePartFormat()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent();

        var text = panel.Children
            .OfType<TextBlock>()
            .Select(tb => tb.Text)
            .FirstOrDefault(t => t?.StartsWith("Version") == true);

        Assert.Matches(new Regex(@"^Version \d+\.\d+\.\d+$"), text!);
    }

    [AvaloniaFact]
    public void BuildAboutContent_ContainsReleasesLinkButton()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent();

        var releasesBtn = panel.Children
            .OfType<Button>()
            .FirstOrDefault(b => b.Tag?.ToString() == "https://github.com/vchelaru/FlatRedBall2/releases");

        Assert.NotNull(releasesBtn);
    }

    [AvaloniaFact]
    public void BuildAboutContent_NotYetChecked_ShowsCheckForUpdatesPrompt()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent(updateStatus: null);

        var promptBlock = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text == "Check for updates:");

        Assert.NotNull(promptBlock);
    }

    [AvaloniaFact]
    public void BuildAboutWindowWithLiveRefresh_HasCenterOwnerStartupLocation()
    {
        var window = MainWindow.BuildAboutWindowWithLiveRefresh(NeverCompletingRefresh, NoOpRestart);

        Assert.Equal(WindowStartupLocation.CenterOwner, window.WindowStartupLocation);
    }

    [AvaloniaFact]
    public void BuildAboutWindowWithLiveRefresh_IsNotResizable()
    {
        var window = MainWindow.BuildAboutWindowWithLiveRefresh(NeverCompletingRefresh, NoOpRestart);

        Assert.False(window.CanResize);
    }

    [AvaloniaFact]
    public void BuildAboutWindowWithLiveRefresh_HasCorrectTitle()
    {
        var window = MainWindow.BuildAboutWindowWithLiveRefresh(NeverCompletingRefresh, NoOpRestart);

        Assert.Equal("About AnimationEditor", window.Title);
    }

    // ── Real update mechanism, shared with the startup banner (issue #1033) ────────────────────
    // About used to run its own separate GitHub-release comparison whose "Get Update" button only
    // opened a browser tab. It now drives the exact same ApplicationUpdateResult/IApplicationUpdater
    // flow (and the exact same restart action) the persistent startup banner uses.

    [AvaloniaFact]
    public void BuildAboutContent_NoUpdate_ShowsUpToDateMessage()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent(ApplicationUpdateResult.NoUpdate);

        var upToDateBlock = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text == "You're up to date.");

        Assert.NotNull(upToDateBlock);
    }

    [AvaloniaFact]
    public void BuildAboutContent_ReadyToRestart_ShowsVersionAndRestartButton()
    {
        var result = ApplicationUpdateResult.ReadyToRestart(new Version(2026, 9, 6));
        var panel = (StackPanel)MainWindow.BuildAboutContent(result, onRestart: () => { });

        var statusBlock = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text?.Contains("2026.9.6") == true);
        var restartBtn = panel.Children.OfType<Button>().FirstOrDefault(b => b.Name == "AboutRestartBtn");

        Assert.NotNull(statusBlock);
        Assert.NotNull(restartBtn);
    }

    [AvaloniaFact]
    public void BuildAboutContent_ReadyToRestart_RestartButtonClicked_InvokesOnRestart()
    {
        var restartCount = 0;
        var result = ApplicationUpdateResult.ReadyToRestart(new Version(2026, 9, 6));
        var panel = (StackPanel)MainWindow.BuildAboutContent(result, onRestart: () => restartCount++);
        var restartBtn = panel.Children.OfType<Button>().First(b => b.Name == "AboutRestartBtn");

        restartBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, restartCount);
    }

    [AvaloniaFact]
    public void BuildAboutContent_Failed_ShowsFailureMessageAndRetryButton()
    {
        var result = ApplicationUpdateResult.Failed("The update could not be downloaded.");
        var panel = (StackPanel)MainWindow.BuildAboutContent(result, onRefresh: () => Task.CompletedTask);

        var failureBlock = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text == "The update could not be downloaded.");
        var retryBtn = panel.Children.OfType<Button>()
            .FirstOrDefault(b => b.Name == "AboutRefreshBtn" && b.Content as string == "Retry");

        Assert.NotNull(failureBlock);
        Assert.NotNull(retryBtn);
    }

    // ── Manual refresh (issue #1033) ────────────────────────────────────────────

    [AvaloniaFact]
    public void BuildAboutContent_NoRefreshCallback_HasNoRefreshButton()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent();

        var refreshBtn = panel.Children.OfType<Button>().FirstOrDefault(b => b.Name == "AboutRefreshBtn");

        Assert.Null(refreshBtn);
    }

    [AvaloniaFact]
    public void BuildAboutContent_WithRefreshCallback_ContainsRefreshButton()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent(onRefresh: () => Task.CompletedTask);

        var refreshBtn = panel.Children.OfType<Button>().FirstOrDefault(b => b.Name == "AboutRefreshBtn");

        Assert.NotNull(refreshBtn);
    }

    [AvaloniaFact]
    public void BuildAboutContent_RefreshButtonClicked_InvokesCallback()
    {
        var callCount = 0;
        var panel = (StackPanel)MainWindow.BuildAboutContent(onRefresh: () => { callCount++; return Task.CompletedTask; });
        var refreshBtn = panel.Children.OfType<Button>().First(b => b.Name == "AboutRefreshBtn");

        refreshBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, callCount);
    }

    [AvaloniaFact]
    public void BuildAboutContent_IsChecking_HidesButtonAndShowsSpinner()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent(onRefresh: () => Task.CompletedTask, isChecking: true);

        var refreshBtn = panel.Children.OfType<Button>().FirstOrDefault(b => b.Name == "AboutRefreshBtn");
        var spinner = panel.Children.OfType<Control>().FirstOrDefault(c => c.Name == "AboutRefreshSpinner");

        Assert.Null(refreshBtn);
        Assert.NotNull(spinner);
    }

    // ── Auto-check + spinner on open (issue #1033) ──────────────────────────────
    // Opening About behaves as if its "Check for Updates" button were already clicked: the
    // check starts immediately and the button is replaced by a spinner until it resolves.

    [AvaloniaFact]
    public void BuildAboutWindowWithLiveRefresh_OnOpen_StartsCheckingImmediately()
    {
        var pending = new TaskCompletionSource<ApplicationUpdateResult>();

        var window = MainWindow.BuildAboutWindowWithLiveRefresh(refresh: () => pending.Task, onRestart: NoOpRestart);

        var panel = (StackPanel)window.Content!;
        Assert.Null(panel.Children.OfType<Button>().FirstOrDefault(b => b.Name == "AboutRefreshBtn"));
        Assert.NotNull(panel.Children.OfType<Control>().FirstOrDefault(c => c.Name == "AboutRefreshSpinner"));
    }

    [AvaloniaFact]
    public void BuildAboutWindowWithLiveRefresh_CheckCompletesReadyToRestart_ShowsRestartButton()
    {
        var pending = new TaskCompletionSource<ApplicationUpdateResult>();
        var window = MainWindow.BuildAboutWindowWithLiveRefresh(refresh: () => pending.Task, onRestart: NoOpRestart);

        pending.SetResult(ApplicationUpdateResult.ReadyToRestart(new Version(2026, 9, 6)));
        Dispatcher.UIThread.RunJobs();

        var panel = (StackPanel)window.Content!;
        Assert.Null(panel.Children.OfType<Control>().FirstOrDefault(c => c.Name == "AboutRefreshSpinner"));
        Assert.NotNull(panel.Children.OfType<Button>().FirstOrDefault(b => b.Name == "AboutRestartBtn"));
        Assert.NotNull(panel.Children.OfType<TextBlock>().FirstOrDefault(tb => tb.Text?.Contains("2026.9.6") == true));
    }

    [AvaloniaFact]
    public void BuildAboutWindowWithLiveRefresh_RestartButtonClicked_InvokesOnRestart()
    {
        var restartCount = 0;
        var pending = new TaskCompletionSource<ApplicationUpdateResult>();
        var window = MainWindow.BuildAboutWindowWithLiveRefresh(refresh: () => pending.Task, onRestart: () => restartCount++);
        pending.SetResult(ApplicationUpdateResult.ReadyToRestart(new Version(2026, 9, 6)));
        Dispatcher.UIThread.RunJobs();

        var restartBtn = ((StackPanel)window.Content!).Children.OfType<Button>().First(b => b.Name == "AboutRestartBtn");
        restartBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, restartCount);
    }

    [AvaloniaFact]
    public void BuildAboutWindowWithLiveRefresh_RefreshButtonClickedAfterCheck_ReturnsToCheckingState()
    {
        var firstCheck = new TaskCompletionSource<ApplicationUpdateResult>();
        var secondCheck = new TaskCompletionSource<ApplicationUpdateResult>();
        var callCount = 0;
        Func<Task<ApplicationUpdateResult>> refresh = () => ++callCount == 1 ? firstCheck.Task : secondCheck.Task;

        var window = MainWindow.BuildAboutWindowWithLiveRefresh(refresh, NoOpRestart);
        firstCheck.SetResult(ApplicationUpdateResult.NoUpdate);
        Dispatcher.UIThread.RunJobs();

        var refreshBtn = ((StackPanel)window.Content!).Children.OfType<Button>().First(b => b.Name == "AboutRefreshBtn");
        refreshBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var panel = (StackPanel)window.Content!;
        Assert.Null(panel.Children.OfType<Button>().FirstOrDefault(b => b.Name == "AboutRefreshBtn"));
        Assert.NotNull(panel.Children.OfType<Control>().FirstOrDefault(c => c.Name == "AboutRefreshSpinner"));
    }

    private static Task<ApplicationUpdateResult> NeverCompletingRefresh() => new TaskCompletionSource<ApplicationUpdateResult>().Task;
    private static void NoOpRestart() { }
}
