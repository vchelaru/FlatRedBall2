using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Startup behavior of the Tiled-install banner (#1128). The three-button decision itself
/// (detect/suppress/status) is covered headlessly by <c>TiledExtensionInstallPromptDeciderTests</c>
/// in Core.Tests; these tests cover the wiring that decides *whether* the banner is shown at
/// startup and what "Install" does once clicked.
/// </summary>
public class TiledInstallBannerTests
{
    [AvaloniaFact]
    public void InstallButton_Clicked_InstallsAndHidesBanner()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.TiledExtensionInstaller.DetectedFolder = @"C:\Fake\Tiled\extensions";
        ctx.TiledExtensionInstaller.Status = TiledExtensionInstallStatus.NotInstalled;
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var installButton = window.FindControl<Button>("InstallTiledExtensionBtn");
        installButton!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, ctx.TiledExtensionInstaller.InstallCount);
        Assert.Equal(@"C:\Fake\Tiled\extensions", ctx.TiledExtensionInstaller.LastInstalledFolder);
        var banner = window.FindControl<Border>("TiledInstallBanner");
        Assert.False(banner!.IsVisible);
    }

    [AvaloniaFact]
    public void Startup_NotDetected_BannerStaysHidden()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.TiledExtensionInstaller.DetectedFolder = null;
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var banner = window.FindControl<Border>("TiledInstallBanner");
        Assert.False(banner!.IsVisible);
    }

    [AvaloniaFact]
    public void Startup_TiledDetectedAndNotInstalled_ShowsBanner()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.TiledExtensionInstaller.DetectedFolder = @"C:\Fake\Tiled\extensions";
        ctx.TiledExtensionInstaller.Status = TiledExtensionInstallStatus.NotInstalled;
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var banner = window.FindControl<Border>("TiledInstallBanner");
        Assert.True(banner!.IsVisible);
    }

    [AvaloniaFact]
    public void Startup_TiledDetectedButAlreadyUpToDate_BannerStaysHidden()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.TiledExtensionInstaller.DetectedFolder = @"C:\Fake\Tiled\extensions";
        ctx.TiledExtensionInstaller.Status = TiledExtensionInstallStatus.UpToDate;
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var banner = window.FindControl<Border>("TiledInstallBanner");
        Assert.False(banner!.IsVisible);
    }

    [AvaloniaFact]
    public void ManualPick_ValidationFails_DoesNotInstallOrRememberFolder()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.TiledExtensionInstaller.DetectedFolder = null;
        ctx.TiledExtensionInstaller.ValidateError = "That folder isn't writable.";
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.InstallTiledExtensionToPickedFolder(@"C:\SomePickedFolder");

        Assert.Equal(0, ctx.TiledExtensionInstaller.InstallCount);
    }

    [AvaloniaFact]
    public void ManualPick_ValidFolder_InstallsAndRemembersFolder()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.TiledExtensionInstaller.DetectedFolder = null;
        var window = ctx.CreateMainWindow();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.InstallTiledExtensionToPickedFolder(@"C:\SomePickedFolder");

        Assert.Equal(1, ctx.TiledExtensionInstaller.InstallCount);
        Assert.Equal(@"C:\SomePickedFolder", ctx.TiledExtensionInstaller.LastInstalledFolder);
    }
}
