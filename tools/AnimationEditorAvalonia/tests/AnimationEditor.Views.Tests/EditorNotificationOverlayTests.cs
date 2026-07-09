using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.Views.Tests;

// Phase 15: portable toast/banner overlay -- visibility before browser wiring.
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
}
