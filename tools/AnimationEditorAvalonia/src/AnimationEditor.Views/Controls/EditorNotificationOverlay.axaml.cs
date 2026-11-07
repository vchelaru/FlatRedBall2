using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Phase 15: shared toast/banner overlays matching desktop <c>MainWindow</c>'s three notification
/// surfaces (item-deleted undo toast, generic dismissible/retryable toast, error banner).
/// </summary>
public partial class EditorNotificationOverlay : UserControl
{
    private DispatcherTimer? _toastTimer;
    private DispatcherTimer? _errorBannerTimer;
    private CancellationTokenSource? _itemDeletedCts;
    private Action? _toastRetryAction;
    private Action? _undoAction;

    /// <summary>True while the banner shows a warning (<see cref="ShowWarningBanner"/>) rather than an error.</summary>
    public bool IsBannerWarning { get; private set; }

    /// <summary>Internal seam so tests can drive hover state without synthesizing pointer events.</summary>
    internal bool IsToastHovered { get; set; }
    internal bool IsErrorBannerHovered { get; set; }
    internal bool IsItemDeletedHovered { get; set; }

    /// <summary>Writes text to the clipboard. Defaults to the host window's clipboard; tests replace it.</summary>
    internal Func<string, Task> ClipboardWriter { get; set; }

    public EditorNotificationOverlay()
    {
        InitializeComponent();
        ClipboardWriter = async text =>
        {
            IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(text);
        };
        ToastCopyBtn.Click += async (_, _) => await ClipboardWriter(ToastMessage.Text ?? string.Empty);
        ErrorBannerCopyBtn.Click += async (_, _) => await ClipboardWriter(ErrorBannerText.Text ?? string.Empty);
        ToastDismissBtn.Click += (_, _) => HideToast();
        ToastRetryBtn.Click += (_, _) =>
        {
            HideToast();
            _toastRetryAction?.Invoke();
        };
        ErrorBannerDismissBtn.Click += (_, _) => HideErrorBanner();
        ItemDeletedToastUndoBtn.Click += (_, _) =>
        {
            _itemDeletedCts?.Cancel();
            ItemDeletedToastPanel.IsVisible = false;
            _undoAction?.Invoke();
        };

        // #1130 follow-up: an auto-hide timer races a deliberate click toward that panel's own
        // dismiss/retry/undo button -- a slow click can land after the panel has vanished,
        // hitting whatever is underneath instead. Hovering a panel suspends its auto-hide for as
        // long as the pointer stays over it, so a click aimed at the panel can never miss it.
        ToastPanel.PointerEntered += (_, _) => IsToastHovered = true;
        ToastPanel.PointerExited += (_, _) => IsToastHovered = false;
        ErrorBanner.PointerEntered += (_, _) => IsErrorBannerHovered = true;
        ErrorBanner.PointerExited += (_, _) => IsErrorBannerHovered = false;
        ItemDeletedToastPanel.PointerEntered += (_, _) => IsItemDeletedHovered = true;
        ItemDeletedToastPanel.PointerExited += (_, _) => IsItemDeletedHovered = false;
    }

    public void WireUndo(Action undo) => _undoAction = undo;

    public void ShowItemDeleted(string label)
    {
        _itemDeletedCts?.Cancel();
        _itemDeletedCts = new CancellationTokenSource();
        CancellationToken token = _itemDeletedCts.Token;

        ItemDeletedToastLabel.Text = $"\"{label}\" deleted";
        ItemDeletedToastPanel.IsVisible = true;

        _ = AutoHideItemDeletedAsync(token);
    }

    public void ShowToast(string message, Action? retryAction = null)
    {
        _toastRetryAction = retryAction;
        ToastMessage.Text = message;
        ToastRetryBtn.IsVisible = retryAction is not null;
        ToastPanel.IsVisible = true;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _toastTimer.Tick += (_, _) => HideToastIfNotHovered();
        _toastTimer.Start();
    }

    public void ShowErrorBanner(string text)
    {
        StyleBanner(warning: false);
        ErrorBannerText.Text = text.TrimStart('⚠', ' ');
        ErrorBanner.IsVisible = true;

        _errorBannerTimer?.Stop();
        _errorBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _errorBannerTimer.Tick += (_, _) => HideErrorBannerIfNotHovered();
        _errorBannerTimer.Start();
    }

    /// <summary>
    /// Shows the top-centre banner in amber for something the user should know but did not do wrong. Stays until
    /// dismissed, so it cannot be missed.
    /// </summary>
    public void ShowWarningBanner(string text)
    {
        _errorBannerTimer?.Stop();
        StyleBanner(warning: true);
        ErrorBannerText.Text = text;
        ErrorBanner.IsVisible = true;
    }

    // The banner is one control in two looks; the amber one stands out from the dark theme where the red one is calm.
    private void StyleBanner(bool warning)
    {
        IsBannerWarning = warning;
        ErrorBanner.Background = Brush.Parse(warning ? "#5C4208" : "#2D2122");
        ErrorBanner.BorderBrush = Brush.Parse(warning ? "#FFB84D" : "#E0533F");
        ErrorBannerText.Foreground = Brush.Parse(warning ? "#FFF3D6" : "#F5E2DF");
        ErrorBannerText.FontSize = warning ? 13 : 12;
        ErrorBannerIcon.Foreground = Brush.Parse(warning ? "#FFD27A" : "#F2A03E");
        foreach (var glyph in ErrorBanner.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>())
            glyph.Fill = Brush.Parse(warning ? "#FFE3A8" : "#E8B0A8");
    }

    private async Task AutoHideItemDeletedAsync(CancellationToken token)
    {
        try
        {
            do
            {
                await Task.Delay(4000, token);
            } while (IsItemDeletedHovered);

            ItemDeletedToastPanel.IsVisible = false;
        }
        catch (TaskCanceledException) { }
    }

    /// <summary>Testable seam for the timer Tick: hovering keeps the toast up indefinitely.</summary>
    internal void HideToastIfNotHovered()
    {
        if (!IsToastHovered)
            HideToast();
    }

    /// <summary>Testable seam for the timer Tick: hovering keeps the banner up indefinitely.</summary>
    internal void HideErrorBannerIfNotHovered()
    {
        if (!IsErrorBannerHovered)
            HideErrorBanner();
    }

    private void HideToast()
    {
        _toastTimer?.Stop();
        ToastPanel.IsVisible = false;
    }

    private void HideErrorBanner()
    {
        _errorBannerTimer?.Stop();
        ErrorBanner.IsVisible = false;
        ErrorBannerText.Text = string.Empty;
    }
}
