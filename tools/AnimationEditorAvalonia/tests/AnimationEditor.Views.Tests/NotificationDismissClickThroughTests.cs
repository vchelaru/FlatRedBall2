using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.Views.Tests;

/// <summary>
/// #1130 follow-up: EditorNotificationOverlay's root used to carry IsHitTestVisible="False",
/// which prunes the whole subtree from Avalonia's hit-test walk, so none of the panels' own
/// buttons were reachable by a real pointer click -- only by a synthetic
/// RaiseEvent(Button.ClickEvent), which bypasses hit-testing entirely and so never caught this.
/// These drive an actual pointer click at each button's on-screen bounds, the way a user's mouse
/// does, through a real hosting Window.
/// </summary>
public class NotificationDismissClickThroughTests
{
    private static (Window Window, EditorNotificationOverlay Overlay) CreateHostedOverlay()
    {
        var overlay = new EditorNotificationOverlay();
        var window = new Window { Width = 800, Height = 600, Content = overlay };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, overlay);
    }

    private static void ClickAt(Window window, Control control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), window)!.Value;
        Point center = new Point(
            topLeft.X + control.Bounds.Width / 2,
            topLeft.Y + control.Bounds.Height / 2);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void ClickingToastDismissBtn_HidesToast()
    {
        var (window, overlay) = CreateHostedOverlay();
        try
        {
            overlay.ShowToast("Exported spritesheet.json");
            Dispatcher.UIThread.RunJobs();

            ClickAt(window, overlay.ToastDismissBtn);

            Assert.False(overlay.ToastPanel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingErrorBannerDismissBtn_HidesBanner()
    {
        var (window, overlay) = CreateHostedOverlay();
        try
        {
            overlay.ShowErrorBanner("Save failed");
            Dispatcher.UIThread.RunJobs();

            ClickAt(window, overlay.ErrorBannerDismissBtn);

            Assert.False(overlay.ErrorBanner.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ClickingItemDeletedUndoBtn_InvokesUndo()
    {
        var (window, overlay) = CreateHostedOverlay();
        try
        {
            bool undoInvoked = false;
            overlay.WireUndo(() => undoInvoked = true);
            overlay.ShowItemDeleted("Walk frame 2");
            Dispatcher.UIThread.RunJobs();

            ClickAt(window, overlay.ItemDeletedToastUndoBtn);

            Assert.True(undoInvoked);
            Assert.False(overlay.ItemDeletedToastPanel.IsVisible);
        }
        finally { window.Close(); }
    }
}
