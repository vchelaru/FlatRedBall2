using System.Linq;
using System.Text.RegularExpressions;
using AnimationEditor.Core.Update;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
}
