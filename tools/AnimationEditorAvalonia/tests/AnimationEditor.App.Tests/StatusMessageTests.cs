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

            var banner = window.FindControl<Border>("ErrorBanner")!;
            var text   = window.FindControl<TextBlock>("ErrorBannerText")!;
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

            var banner = window.FindControl<Border>("ErrorBanner")!;
            var text   = window.FindControl<TextBlock>("ErrorBannerText")!;
            Assert.True(banner.IsVisible);
            Assert.Contains("hero.achx", text.Text);
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

            var banner  = window.FindControl<Border>("ErrorBanner")!;
            Assert.True(banner.IsVisible);

            var dismiss = window.FindControl<Button>("ErrorBannerDismissBtn")!;
            RaiseClick(dismiss);   // Click handler is wired in InitErrorBanner
            Dispatcher.UIThread.RunJobs();

            Assert.False(banner.IsVisible);
        }
        finally { window.Close(); }
    }

    private static void RaiseClick(Button button) =>
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
}
