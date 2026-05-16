using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for <see cref="MainWindow.BuildDeleteConfirmDialog"/>:
/// keyboard navigation, button clicks, close-without-choosing, and UI shape.
/// </summary>
public class DeletePopupDialogTests
{
    private static void PressKey(Window dialog, Key key) =>
        dialog.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);

    [AvaloniaFact]
    public void BuildDeleteConfirmDialog_EnterKey_ResolvesTrue()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = MainWindow.BuildDeleteConfirmDialog("Delete animation \"Walk\"?", "Delete Animation", tcs);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        PressKey(dialog, Key.Enter);
        Dispatcher.UIThread.RunJobs();

        Assert.True(tcs.Task.IsCompletedSuccessfully);
        Assert.True(tcs.Task.Result, "ENTER should confirm deletion");
    }

    [AvaloniaFact]
    public void BuildDeleteConfirmDialog_EscapeKey_ResolvesFalse()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = MainWindow.BuildDeleteConfirmDialog("Delete animation \"Walk\"?", "Delete Animation", tcs);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        PressKey(dialog, Key.Escape);
        Dispatcher.UIThread.RunJobs();

        Assert.True(tcs.Task.IsCompletedSuccessfully);
        Assert.False(tcs.Task.Result, "ESC should cancel");
    }

    [AvaloniaFact]
    public void BuildDeleteConfirmDialog_ClosedWithoutChoice_ResolvesFalse()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = MainWindow.BuildDeleteConfirmDialog("Delete 3 animations?", "Delete Animation", tcs);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        dialog.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.True(tcs.Task.IsCompletedSuccessfully);
        Assert.False(tcs.Task.Result, "Closing without choosing must not delete");
    }

    [AvaloniaFact]
    public void BuildDeleteConfirmDialog_DeleteButtonClick_ResolvesTrue()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = MainWindow.BuildDeleteConfirmDialog("Delete shape \"Hitbox\"?", "Delete Shape", tcs);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        var deleteBtn = dialog.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Content?.ToString() == "Delete");
        deleteBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(tcs.Task.IsCompletedSuccessfully);
        Assert.True(tcs.Task.Result);
    }

    [AvaloniaFact]
    public void BuildDeleteConfirmDialog_CancelButtonClick_ResolvesFalse()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = MainWindow.BuildDeleteConfirmDialog("Delete shape \"Hitbox\"?", "Delete Shape", tcs);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        var cancelBtn = dialog.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Content?.ToString() == "Cancel");
        cancelBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.True(tcs.Task.IsCompletedSuccessfully);
        Assert.False(tcs.Task.Result);
    }

    [AvaloniaFact]
    public void BuildDeleteConfirmDialog_DeleteButton_HasRedBackground()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = MainWindow.BuildDeleteConfirmDialog("Delete animation \"Walk\"?", "Delete Animation", tcs);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        var deleteBtn = dialog.GetVisualDescendants()
            .OfType<Button>()
            .First(b => b.Content?.ToString() == "Delete");

        var brush = deleteBtn.Background as SolidColorBrush;
        Assert.NotNull(brush);
        Assert.Equal(Color.Parse("#d83a3a"), brush!.Color);

        dialog.Close();
    }

    [AvaloniaFact]
    public void BuildDeleteConfirmDialog_ContainsCannotUndoSubtitle()
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = MainWindow.BuildDeleteConfirmDialog("Delete 2 shape(s)?", "Delete Shape", tcs);
        dialog.Show();
        Dispatcher.UIThread.RunJobs();

        var subtitleBlock = dialog.GetVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(tb => tb.Text == "This action cannot be undone.");

        Assert.NotNull(subtitleBlock);
        dialog.Close();
    }
}
