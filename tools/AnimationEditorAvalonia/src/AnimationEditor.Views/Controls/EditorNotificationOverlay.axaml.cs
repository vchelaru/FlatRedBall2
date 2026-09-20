using Avalonia.Controls;
using Avalonia.Threading;
using System;
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

    /// <summary>Internal seam so tests can drive hover state without synthesizing pointer events.</summary>
    internal bool IsToastHovered { get; set; }
    internal bool IsErrorBannerHovered { get; set; }
    internal bool IsItemDeletedHovered { get; set; }

    public EditorNotificationOverlay()
    {
        InitializeComponent();
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
        ErrorBannerText.Text = text.TrimStart('⚠', ' ');
        ErrorBanner.IsVisible = true;

        _errorBannerTimer?.Stop();
        _errorBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _errorBannerTimer.Tick += (_, _) => HideErrorBannerIfNotHovered();
        _errorBannerTimer.Start();
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
