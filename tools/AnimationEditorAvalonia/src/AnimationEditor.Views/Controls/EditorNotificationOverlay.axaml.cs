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
        _toastTimer.Tick += (_, _) => HideToast();
        _toastTimer.Start();
    }

    public void ShowErrorBanner(string text)
    {
        ErrorBannerText.Text = text.TrimStart('⚠', ' ');
        ErrorBanner.IsVisible = true;

        _errorBannerTimer?.Stop();
        _errorBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _errorBannerTimer.Tick += (_, _) => HideErrorBanner();
        _errorBannerTimer.Start();
    }

    private async Task AutoHideItemDeletedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(4000, token);
            ItemDeletedToastPanel.IsVisible = false;
        }
        catch (TaskCanceledException) { }
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
