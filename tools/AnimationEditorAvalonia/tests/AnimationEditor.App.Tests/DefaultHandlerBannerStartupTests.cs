using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Fake <see cref="IFileAssociationService"/> reporting "supported, not yet default" — the
/// state that used to trigger <see cref="DefaultHandlerPromptDecider.ShouldPrompt"/>.
/// </summary>
internal sealed class FakeNotDefaultFileAssociationService : IFileAssociationService
{
    public bool IsSupported => true;

    public bool IsDefault() => false;

    public AchxFileAssociationStatus GetStatus() => AchxFileAssociationStatus.NotAssociated;

    public void RegisterAsDefault() { }
}

/// <summary>
/// Issue #849: <c>RegisterAsDefault()</c> doesn't work for the current dev/portable
/// distribution (no installer — see #493), so the startup banner offering it should never
/// auto-show, even when <see cref="DefaultHandlerPromptDecider.ShouldPrompt"/>'s inputs
/// would otherwise say to.
/// </summary>
public class DefaultHandlerBannerStartupTests
{
    [AvaloniaFact]
    public void Startup_NeverAutoShowsDefaultHandlerBanner()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.FileAssociationService = new FakeNotDefaultFileAssociationService();
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var banner = window.FindControl<Border>("DefaultHandlerBanner");
        Assert.False(banner!.IsVisible);
    }
}
