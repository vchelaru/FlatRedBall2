using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AnimationEditor.Core.Update;
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
    public void BuildAboutContent_ContainsUpdatesPrompt()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent();

        var promptBlock = panel.Children
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text?.Contains("updates", System.StringComparison.OrdinalIgnoreCase) == true);

        Assert.NotNull(promptBlock);
    }

    [AvaloniaFact]
    public void BuildAboutWindow_HasCenterOwnerStartupLocation()
    {
        var window = MainWindow.BuildAboutWindow();

        Assert.Equal(WindowStartupLocation.CenterOwner, window.WindowStartupLocation);
    }

    [AvaloniaFact]
    public void BuildAboutWindow_IsNotResizable()
    {
        var window = MainWindow.BuildAboutWindow();

        Assert.False(window.CanResize);
    }

    [AvaloniaFact]
    public void BuildAboutWindow_HasCorrectTitle()
    {
        var window = MainWindow.BuildAboutWindow();

        Assert.Equal("About AnimationEditor", window.Title);
    }

    // ── Update-check surface (issue #681) ─────────────────────────────────────

    [AvaloniaFact]
    public void BuildAboutContent_NoUpdateAvailable_KeepsDefaultReleasesPromptAndButton()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent(UpdateCheckResult.NoUpdate);

        var promptBlock = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text?.Contains("updates", System.StringComparison.OrdinalIgnoreCase) == true);
        var releasesBtn = panel.Children.OfType<Button>()
            .FirstOrDefault(b => b.Tag?.ToString() == "https://github.com/vchelaru/FlatRedBall2/releases");

        Assert.NotNull(promptBlock);
        Assert.NotNull(releasesBtn);
    }

    [AvaloniaFact]
    public void BuildAboutContent_UpdateAvailable_ShowsLatestVersionText()
    {
        var result = new UpdateCheckResult(true, new System.Version(2026, 7, 17), "https://example.com/latest");
        var panel = (StackPanel)MainWindow.BuildAboutContent(result);

        var updateBlock = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text?.Contains("2026.7.17") == true);

        Assert.NotNull(updateBlock);
    }

    [AvaloniaFact]
    public void BuildAboutContent_UpdateAvailable_ButtonPointsAtReleaseUrl()
    {
        var result = new UpdateCheckResult(true, new System.Version(2026, 7, 17), "https://example.com/latest");
        var panel = (StackPanel)MainWindow.BuildAboutContent(result);

        var downloadBtn = panel.Children.OfType<Button>()
            .FirstOrDefault(b => b.Tag?.ToString() == "https://example.com/latest");

        Assert.NotNull(downloadBtn);
    }

    // Issue #845: a completed, successful check that found no newer release must say so —
    // it was previously indistinguishable from "never checked" (both showed "Check here for updates").
    [AvaloniaFact]
    public void BuildAboutContent_CheckedAndUpToDate_ShowsUpToDateMessage()
    {
        var result = new UpdateCheckResult(false, new System.Version(2026, 8, 11), "https://example.com/releases");
        var panel = (StackPanel)MainWindow.BuildAboutContent(result);

        var upToDateBlock = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text?.Contains("up to date", System.StringComparison.OrdinalIgnoreCase) == true);

        Assert.NotNull(upToDateBlock);
    }

    [AvaloniaFact]
    public void BuildAboutContent_NotYetChecked_KeepsDefaultReleasesPrompt()
    {
        // updateCheck is null: no check has run (never contacted GitHub, no cached version) —
        // distinct from a completed check that found no update.
        var panel = (StackPanel)MainWindow.BuildAboutContent(updateCheck: null);

        var promptBlock = panel.Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text?.Contains("Check here for updates", System.StringComparison.OrdinalIgnoreCase) == true);

        Assert.NotNull(promptBlock);
    }

    // ── Manual refresh (issue #1033) ────────────────────────────────────────────
    // Opening About already forces a fresh check (issue #681), but a check that fails
    // silently (offline, GitHub rate limit) leaves the dialog showing a stale result until
    // the user closes and reopens it. A visible refresh button lets them retry in place.

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
    public void BuildAboutWindowWithLiveRefresh_RefreshButtonClicked_ReplacesContentWithNewResult()
    {
        var refreshedResult = new UpdateCheckResult(true, new System.Version(2026, 9, 6), "https://example.com/latest");
        var window = MainWindow.BuildAboutWindowWithLiveRefresh(
            initial: UpdateCheckResult.NoUpdate,
            refresh: () => Task.FromResult(refreshedResult));

        var refreshBtn = ((StackPanel)window.Content!).Children.OfType<Button>().First(b => b.Name == "AboutRefreshBtn");
        refreshBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        var updateBlock = ((StackPanel)window.Content!).Children.OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text?.Contains("2026.9.6") == true);
        Assert.NotNull(updateBlock);
    }
}
