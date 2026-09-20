using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #1130 follow-up: the generic toast's floating top-right position must never overlap the
/// window's own custom-drawn Minimize/Maximize/Close buttons (MainWindow uses
/// <c>WindowDecorations="None"</c> and draws its own title bar, so these are ordinary
/// in-tree controls the overlay can legitimately cover). The toast auto-hides on a timer, so a
/// user aiming a click at its dismiss button can have it vanish mid-click; if the toast sits
/// over CloseBtn, that stray click closes the app instead of landing on empty space.
/// </summary>
public class ToastCloseButtonOverlapTests
{
    [AvaloniaFact]
    public void ToastPanel_DoesNotOverlap_CloseButton()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Width = 1400;
        window.Height = 900;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            window.Notifications.ShowToast("Exported spritesheet.json");
            Dispatcher.UIThread.RunJobs();

            var closeBtn = window.FindControl<Button>("CloseBtn")
                ?? throw new InvalidOperationException("CloseBtn not found");
            var toastPanel = window.Notifications.ToastPanel;

            Rect closeBounds = new Rect(
                closeBtn.TranslatePoint(new Point(0, 0), window)!.Value,
                closeBtn.Bounds.Size);
            Rect toastBounds = new Rect(
                toastPanel.TranslatePoint(new Point(0, 0), window)!.Value,
                toastPanel.Bounds.Size);

            Assert.False(closeBounds.Intersects(toastBounds),
                $"ToastPanel {toastBounds} overlaps CloseBtn {closeBounds} -- an in-flight " +
                "auto-hide can turn a click meant for the toast's dismiss button into a click " +
                "on the window's own close button.");
        }
        finally { window.Close(); }
    }
}
