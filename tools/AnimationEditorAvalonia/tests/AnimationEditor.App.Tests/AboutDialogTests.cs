using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for the About dialog (issue #194): centered on owner,
/// non-resizable, correct title, version number from assembly, and GitHub link.
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
    public void BuildAboutContent_ContainsGitHubLinkButton()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent();

        var linkBtn = panel.Children
            .OfType<Button>()
            .FirstOrDefault(b => b.Content?.ToString()?.Contains("github.com/vchelaru/FlatRedBall2") == true);

        Assert.NotNull(linkBtn);
    }

    [AvaloniaFact]
    public void BuildAboutContent_ContainsReleasesLinkButton()
    {
        var panel = (StackPanel)MainWindow.BuildAboutContent();

        var releasesBtn = panel.Children
            .OfType<Button>()
            .FirstOrDefault(b => b.Content?.ToString()?.Contains("github.com/vchelaru/FlatRedBall2/releases") == true);

        Assert.NotNull(releasesBtn);
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
}
