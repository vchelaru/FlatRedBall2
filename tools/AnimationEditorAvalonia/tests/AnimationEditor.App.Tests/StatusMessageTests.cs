using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

public class StatusMessageTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    // Errors route to the prominent top-centre ErrorBanner, not the thin bottom status bar,
    // so failures can't be missed (#479 follow-up). The bottom StatusMessage stays for info.
    //
    // The banner lives inside the shared EditorNotificationOverlay (#695 A5), which has its own
    // namescope — reach it through the hosted control, not window.FindControl by name.

    [AvaloniaFact]
    public void CommitInlineRename_EmptyName_ShowsErrorBanner()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = ctx.AppCommands.AddAnimationChainWithName("Walk")!;
            Dispatcher.UIThread.RunJobs();

            var vm = window.GetTreeRoots().First(v => v.Data == chain);
            window.CommitInlineRenamePublic(vm, "");
            Dispatcher.UIThread.RunJobs();

            var banner = window.Notifications.ErrorBanner;
            var text   = window.Notifications.ErrorBannerText;
            Assert.True(banner.IsVisible);
            Assert.Contains("cannot be empty", text.Text);
            Assert.Equal("Walk", chain.Name);

            // The informational bottom bar is not used for errors.
            Assert.False(window.FindControl<TextBlock>("StatusMessage")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LoadFailed_Event_ShowsErrorBanner()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.LoadAnimationChain("hero.achx");
            Dispatcher.UIThread.RunJobs();

            var banner = window.Notifications.ErrorBanner;
            var text   = window.Notifications.ErrorBannerText;
            Assert.True(banner.IsVisible);
            Assert.Contains("hero.achx", text.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ErrorBanner_StripsLeadingWarningGlyph_NoDoubleIcon()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            // LoadAnimationChain failure reports "⚠ Could not load '<file>': ...".
            ctx.AppCommands.LoadAnimationChain("hero.achx");
            Dispatcher.UIThread.RunJobs();

            // The banner renders its own ⚠ icon, so the text must not also start with one.
            var text = window.Notifications.ErrorBannerText;
            Assert.DoesNotContain("⚠", text.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ErrorBanner_Dismiss_HidesBanner()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.LoadAnimationChain("hero.achx");
            Dispatcher.UIThread.RunJobs();

            var banner  = window.Notifications.ErrorBanner;
            Assert.True(banner.IsVisible);

            var dismiss = window.Notifications.ErrorBannerDismissBtn;
            RaiseClick(dismiss);   // Click handler is wired in the overlay's constructor
            Dispatcher.UIThread.RunJobs();

            Assert.False(banner.IsVisible);
        }
        finally { window.Close(); }
    }

    private static void RaiseClick(Button button) =>
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
}
