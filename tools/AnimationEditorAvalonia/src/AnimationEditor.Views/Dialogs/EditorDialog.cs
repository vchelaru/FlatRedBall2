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
    bool SizeToContentHeight = false);

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

        dialog.CloseRequested += window.Close;
        window.Closed += (_, _) => dialog.CloseWithoutResult();
        DialogKeyboard.Wire(window, dialog.Confirm, dialog.Cancel);

        await window.ShowDialog(owner);
        return await dialog.Result;
    }
}

public sealed class EditorDialogOverlay : Grid, IEditorDialogHost
{
    private Action? _confirm;
    private Action? _cancel;

    public EditorDialogOverlay()
    {
        IsVisible = false;
        ZIndex = int.MaxValue;
        AddHandler(InputElement.KeyDownEvent, OnKeyDown,
            RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public async Task<T> ShowAsync<T>(EditorDialog<T> dialog)
    {
        if (IsVisible)
            throw new InvalidOperationException("A modal dialog is already open.");

        var scrim = new Border { Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)) };
        var title = new TextBlock
        {
            Text = dialog.Options.Title,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(14, 12, 14, 0),
        };
        title.Bind(TextBlock.ForegroundProperty, title.GetResourceObservable("Ink"));
        var cardContent = new StackPanel();
        cardContent.Children.Add(title);
        cardContent.Children.Add(dialog.Content);

        var card = new Border
        {
            Width = dialog.Options.Width,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BorderThickness = new Thickness(1),
            Child = cardContent,
        };
        card.Bind(Border.BackgroundProperty, card.GetResourceObservable("BgPanel"));
        card.Bind(Border.BorderBrushProperty, card.GetResourceObservable("LineStrong"));
        if (dialog.Options.Height is { } height)
            card.Height = height;

        Children.Clear();
        Children.Add(scrim);
        Children.Add(card);
        _confirm = dialog.Confirm;
        _cancel = dialog.Cancel;
        dialog.CloseRequested += Hide;
        IsVisible = true;

        Dispatcher.UIThread.Post(() => DialogKeyboard.FocusFirst(dialog.Content),
            DispatcherPriority.Input);

        return await dialog.Result;
    }

    private void Hide()
    {
        IsVisible = false;
        _confirm = null;
        _cancel = null;
        Children.Clear();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsVisible) return;
        if (e.Key == Key.Enter)
        {
            _confirm?.Invoke();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _cancel?.Invoke();
            e.Handled = true;
        }
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
