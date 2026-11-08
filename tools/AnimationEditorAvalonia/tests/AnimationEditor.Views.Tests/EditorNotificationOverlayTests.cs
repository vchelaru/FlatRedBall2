using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.Views.Tests;

// Toast/banner overlay visibility, tested on the control alone.
public class EditorNotificationOverlayTests
{
    [AvaloniaFact]
    public void ShowProblem_MakesBannerVisibleWithText()
    {
        var overlay = new EditorNotificationOverlay();

        overlay.ShowProblem("Save failed");

        Assert.True(overlay.ErrorBanner.IsVisible);
        Assert.Equal("Save failed", overlay.ErrorBannerText.Text);
    }

    // #1549: error text must be copyable so reporting one does not need a screenshot.
    [AvaloniaFact]
    public void ToastCopyButton_CopiesToastMessage()
    {
        var overlay = new EditorNotificationOverlay();
        string? copied = null;
        overlay.ClipboardWriter = text => { copied = text; return System.Threading.Tasks.Task.CompletedTask; };
        overlay.ShowInfo("Could not load 'X.achx': bad xml");

        overlay.ToastCopyBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("Could not load 'X.achx': bad xml", copied);
    }

    [AvaloniaFact]
    public void ErrorBannerCopyButton_CopiesBannerText()
    {
        var overlay = new EditorNotificationOverlay();
        string? copied = null;
        overlay.ClipboardWriter = text => { copied = text; return System.Threading.Tasks.Task.CompletedTask; };
        overlay.ShowProblem("Save failed: disk full");

        overlay.ErrorBannerCopyBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("Save failed: disk full", copied);
    }

    [AvaloniaFact]
    public void ShowItemDeleted_MakesUndoToastVisibleWithLabel()
    {
        var overlay = new EditorNotificationOverlay();

        overlay.ShowItemDeleted("Walk frame 2");

        Assert.True(overlay.ItemDeletedToastPanel.IsVisible);
        Assert.Equal("\"Walk frame 2\" deleted", overlay.ItemDeletedToastLabel.Text);
    }

    [AvaloniaFact]
    public void ShowInfo_MakesToastPanelVisibleWithMessage()
    {
        var overlay = new EditorNotificationOverlay();

        overlay.ShowInfo("Exported spritesheet.json");

        Assert.True(overlay.ToastPanel.IsVisible);
        Assert.Equal("Exported spritesheet.json", overlay.ToastMessage.Text);
        Assert.False(overlay.ErrorBanner.IsVisible);
    }

    [AvaloniaFact]
    public void ShowProblem_WithoutRetry_HidesRetryButton()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowProblem("Copy failed", () => { });

        overlay.ShowProblem("Another failure");

        Assert.False(overlay.ErrorBannerRetryBtn.IsVisible);
    }

    [AvaloniaFact]
    public void ShowProblem_WithRetry_ShowsRetryButton_AndClickingItRunsRetryAndClosesTheBanner()
    {
        var overlay = new EditorNotificationOverlay();
        int retried = 0;

        overlay.ShowProblem("Copy failed", () => retried++);
        Assert.True(overlay.ErrorBannerRetryBtn.IsVisible);
        overlay.ErrorBannerRetryBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(1, retried);
        Assert.False(overlay.ErrorBanner.IsVisible);
    }

    [AvaloniaFact]
    public void ShowInfo_AfterAProblem_LeavesTheProblemBannerUp()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowProblem("Save failed");

        overlay.ShowInfo("Reloaded x.achx");

        Assert.True(overlay.ErrorBanner.IsVisible);
        Assert.True(overlay.ToastPanel.IsVisible);
    }

    // #1130 follow-up: the auto-hide timer used to be able to fire while the user was moving the
    // pointer toward the toast's own dismiss button, hiding it out from under the click. These
    // drive the timer's Tick logic directly (HideToastIfNotHovered/HideErrorBannerIfNotHovered)
    // rather than waiting on the real DispatcherTimer interval.
    [AvaloniaFact]
    public void HideToastIfNotHovered_StaysVisibleWhileHovered()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowInfo("Exported spritesheet.json");
        overlay.IsToastHovered = true;

        overlay.HideToastIfNotHovered();

        Assert.True(overlay.ToastPanel.IsVisible);
    }

    [AvaloniaFact]
    public void HideToastIfNotHovered_HidesWhenNotHovered()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowInfo("Exported spritesheet.json");
        overlay.IsToastHovered = false;

        overlay.HideToastIfNotHovered();

        Assert.False(overlay.ToastPanel.IsVisible);
    }

    [AvaloniaFact]
    public void HideErrorBannerIfNotHovered_StaysVisibleWhileHovered()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowProblem("Save failed");
        overlay.IsErrorBannerHovered = true;

        overlay.HideErrorBannerIfNotHovered();

        Assert.True(overlay.ErrorBanner.IsVisible);
    }

    [AvaloniaFact]
    public void HideErrorBannerIfNotHovered_HidesWhenNotHovered()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowProblem("Save failed");
        overlay.IsErrorBannerHovered = false;

        overlay.HideErrorBannerIfNotHovered();

        Assert.False(overlay.ErrorBanner.IsVisible);
    }
}
