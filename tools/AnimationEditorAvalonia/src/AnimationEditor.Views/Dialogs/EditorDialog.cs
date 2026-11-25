using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace AnimationEditor.Views.Dialogs;

public sealed record EditorDialogOptions(
    string Title,
    double Width,
    double? Height = null,
    bool SizeToContentHeight = false)
{
    /// <summary>The tallest a dialog sized to its content gets, which is never taller than the window it belongs to.</summary>
    public double? MaxHeight { get; init; }

    /// <summary>Whether Enter confirms wherever the focus is. Without it Enter only presses the focused button.</summary>
    public bool EnterConfirms { get; init; } = true;
}

public sealed class EditorDialog<T>
{
    private readonly TaskCompletionSource<T> _completion = new();

    public EditorDialog(EditorDialogOptions options, Control content, T cancelResult)
    {
        Options = options;
        Content = content;
        CancelResult = cancelResult;
    }

    public EditorDialogOptions Options { get; }
    public Control Content { get; }
    public T CancelResult { get; }
    public Action Confirm { get; set; } = () => { };
    public Action Cancel { get; set; } = () => { };

    /// <summary>While set, the dialog's window does not close.</summary>
    public bool IsBusy { get; set; }

    internal event Action? CloseRequested;
    internal Task<T> Result => _completion.Task;

    public void Complete(T result)
    {
        if (_completion.TrySetResult(result))
            CloseRequested?.Invoke();
    }

    internal void CloseWithoutResult() => Complete(CancelResult);
}

public interface IEditorDialogHost
{
    Task<T> ShowAsync<T>(EditorDialog<T> dialog);
}

public sealed class WindowEditorDialogHost(Window owner) : IEditorDialogHost
{
    public async Task<T> ShowAsync<T>(EditorDialog<T> dialog)
    {
        var options = dialog.Options;
        var window = new Window
        {
            Title = options.Title,
            Width = options.Width,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = dialog.Content,
        };
        if (options.Height is { } height)
            window.Height = height;
        if (options.SizeToContentHeight)
            window.SizeToContent = SizeToContent.Height;
        if (options.MaxHeight is { } maxHeight)
            window.MaxHeight = Math.Min(maxHeight, owner.ClientSize.Height);

        dialog.CloseRequested += window.Close;
        window.Closing += (_, e) => e.Cancel |= dialog.IsBusy;
        window.Closed += (_, _) => dialog.CloseWithoutResult();
        DialogKeyboard.Wire(window, options.EnterConfirms ? dialog.Confirm : () => { }, dialog.Cancel);

        await window.ShowDialog(owner);
        return await dialog.Result;
    }
}

internal static class DialogKeyboard
{
    public static void Wire(Control dialog, Action onConfirm, Action onCancel)
    {
        dialog.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                onConfirm();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                onCancel();
                e.Handled = true;
            }
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        if (dialog is Window window)
            window.Opened += (_, _) => FocusFirst(window);
    }

    public static void FocusFirst(Visual root) =>
        root.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(x => x is
                { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
            ?.Focus();
}
