using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.Views.Tests;

// Toast/banner overlay visibility, tested on the control alone.
public class EditorNotificationOverlayTests
{
    [AvaloniaFact]
    public void ShowErrorBanner_MakesBannerVisibleWithText()
    {
        var overlay = new EditorNotificationOverlay();

        overlay.ShowErrorBanner("Save failed");

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
        overlay.ShowToast("Could not load 'X.achx': bad xml");

        overlay.ToastCopyBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("Could not load 'X.achx': bad xml", copied);
    }

    [AvaloniaFact]
    public void ErrorBannerCopyButton_CopiesBannerText()
    {
        var overlay = new EditorNotificationOverlay();
        string? copied = null;
        overlay.ClipboardWriter = text => { copied = text; return System.Threading.Tasks.Task.CompletedTask; };
        overlay.ShowErrorBanner("Save failed: disk full");

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
    public void ShowToast_MakesToastPanelVisibleWithMessage()
    {
        var overlay = new EditorNotificationOverlay();

        overlay.ShowToast("Exported spritesheet.json");

        Assert.True(overlay.ToastPanel.IsVisible);
        Assert.Equal("Exported spritesheet.json", overlay.ToastMessage.Text);
        Assert.False(overlay.ToastRetryBtn.IsVisible);
    }

    [AvaloniaFact]
    public void ShowToast_WithRetry_ShowsRetryButton()
    {
        var overlay = new EditorNotificationOverlay();

        overlay.ShowToast("Copy failed", () => { });

        Assert.True(overlay.ToastRetryBtn.IsVisible);
    }

    // #1130 follow-up: the auto-hide timer used to be able to fire while the user was moving the
    // pointer toward the toast's own dismiss button, hiding it out from under the click. These
    // drive the timer's Tick logic directly (HideToastIfNotHovered/HideErrorBannerIfNotHovered)
    // rather than waiting on the real DispatcherTimer interval.
    [AvaloniaFact]
    public void HideToastIfNotHovered_StaysVisibleWhileHovered()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowToast("Exported spritesheet.json");
        overlay.IsToastHovered = true;

        overlay.HideToastIfNotHovered();

        Assert.True(overlay.ToastPanel.IsVisible);
    }

    [AvaloniaFact]
    public void HideToastIfNotHovered_HidesWhenNotHovered()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowToast("Exported spritesheet.json");
        overlay.IsToastHovered = false;

        overlay.HideToastIfNotHovered();

        Assert.False(overlay.ToastPanel.IsVisible);
    }

    [AvaloniaFact]
    public void HideErrorBannerIfNotHovered_StaysVisibleWhileHovered()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowErrorBanner("Save failed");
        overlay.IsErrorBannerHovered = true;

        overlay.HideErrorBannerIfNotHovered();

        Assert.True(overlay.ErrorBanner.IsVisible);
    }

    [AvaloniaFact]
    public void HideErrorBannerIfNotHovered_HidesWhenNotHovered()
    {
        var overlay = new EditorNotificationOverlay();
        overlay.ShowErrorBanner("Save failed");
        overlay.IsErrorBannerHovered = false;

        overlay.HideErrorBannerIfNotHovered();

        Assert.False(overlay.ErrorBanner.IsVisible);
    }
}
