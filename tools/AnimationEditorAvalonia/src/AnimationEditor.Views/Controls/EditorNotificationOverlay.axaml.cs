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
/// The editor's pop-up notifications, in two kinds. <see cref="ShowInfo"/> is a confirmation or FYI that needs nothing
/// from the user (top-right toast, about 6 s). <see cref="ShowProblem"/> is work that is not saved or an action the user
/// asked for that failed (top-centre banner, about 8 s, dismissable, optional Retry). <see cref="ShowItemDeleted"/> is
/// the info kind with an Undo button. The status bar message is a separate, ambient label and is not part of this.
/// </summary>
public partial class EditorNotificationOverlay : UserControl
{
    private DispatcherTimer? _toastTimer;
    private DispatcherTimer? _errorBannerTimer;
    private CancellationTokenSource? _itemDeletedCts;
    private Action? _bannerRetryAction;
    private Action? _undoAction;

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
        ErrorBannerRetryBtn.Click += (_, _) =>
        {
            HideErrorBanner();
            _bannerRetryAction?.Invoke();
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

    /// <summary>
    /// INFO: a confirmation or FYI that needs nothing from the user ("Reloaded x", "Exported x"). Shows the
    /// top-right toast for about 6 s; hovering it keeps it up.
    /// </summary>
    public void ShowInfo(string message)
    {
        ToastMessage.Text = message;
        ToastPanel.IsVisible = true;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _toastTimer.Tick += (_, _) => HideToastIfNotHovered();
        _toastTimer.Start();
    }

    /// <summary>
    /// PROBLEM: the user's work is not saved, or an action they asked for failed or was refused ("Auto save failed",
    /// "Could not open x", "Nothing to paste"). Shows the prominent top-centre banner for about 8 s; hovering it keeps
    /// it up and the dismiss button closes it. <paramref name="retry"/>, when given, adds a Retry button that closes
    /// the banner and runs it. <paramref name="sticky"/> keeps the banner up until the user dismisses it, for a problem
    /// that stays true after the 8 s (editing a file written by a newer version may lose data).
    /// </summary>
    public void ShowProblem(string message, Action? retry = null, bool sticky = false)
    {
        _bannerRetryAction = retry;
        ErrorBannerRetryBtn.IsVisible = retry is not null;
        ErrorBannerText.Text = message.TrimStart('⚠', ' ');
        ErrorBanner.IsVisible = true;

        _errorBannerTimer?.Stop();
        if (sticky) return;
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
