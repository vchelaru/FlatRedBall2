using System;
using System.Linq;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using Path = Avalonia.Controls.Shapes.Path;

namespace AnimationEditor.Views.Tests;

/// <summary>
/// #1130 follow-up: ToastDismissBtn/ErrorBannerDismissBtn used to render their close glyph as
/// Content="✕" (Unicode MULTIPLICATION X). It rendered visibly left of center in these buttons'
/// small, tight boxes even though the TextBlock's own layout bounds (and
/// HorizontalContentAlignment/VerticalContentAlignment="Center") were correct -- this app's
/// "Inter" font has no glyph for U+2715, so only that character silently falls back to the system
/// font, and the fallback glyph's advance width doesn't match its visible ink width. Confirmed by
/// comparing a headless screenshot against MainWindow's CloseBtn, which uses the same glyph and
/// hits the same font fallback but hides the same absolute pixel offset in a much bigger box.
/// Fixed by replacing the glyph with a Path icon (no font involved, so its bounds are exactly its
/// visible ink). These assert the Path is centered in each button.
/// </summary>
public class NotificationDismissGlyphCenteredTests
{
    [AvaloniaFact]
    public void ToastDismissBtn_IconIsCentered()
    {
        var overlay = new EditorNotificationOverlay();
        var window = new Window { Width = 800, Height = 600, Content = overlay };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            overlay.ShowToast("Exported spritesheet.json");
            Dispatcher.UIThread.RunJobs();

            AssertIconCentered("ToastDismissBtn", overlay.ToastDismissBtn);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ErrorBannerDismissBtn_IconIsCentered()
    {
        var overlay = new EditorNotificationOverlay();
        var window = new Window { Width = 800, Height = 600, Content = overlay };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            overlay.ShowErrorBanner("Save failed");
            Dispatcher.UIThread.RunJobs();

            AssertIconCentered("ErrorBannerDismissBtn", overlay.ErrorBannerDismissBtn);
        }
        finally { window.Close(); }
    }

    private static void AssertIconCentered(string buttonName, Button button)
    {
        var icon = button.GetVisualDescendants().OfType<Path>().FirstOrDefault()
            ?? throw new InvalidOperationException($"{buttonName}: Path icon not found");

        Point iconTopLeft = icon.TranslatePoint(new Point(0, 0), button)!.Value;
        double leftGap = iconTopLeft.X;
        double rightGap = button.Bounds.Width - (iconTopLeft.X + icon.Bounds.Width);
        double topGap = iconTopLeft.Y;
        double bottomGap = button.Bounds.Height - (iconTopLeft.Y + icon.Bounds.Height);

        const double eps = 1.5;
        Assert.True(Math.Abs(leftGap - rightGap) <= eps,
            $"{buttonName}: icon not horizontally centered (leftGap={leftGap}, rightGap={rightGap}, " +
            $"buttonWidth={button.Bounds.Width})");
        Assert.True(Math.Abs(topGap - bottomGap) <= eps,
            $"{buttonName}: icon not vertically centered (topGap={topGap}, bottomGap={bottomGap}, " +
            $"buttonHeight={button.Bounds.Height})");
    }
}
