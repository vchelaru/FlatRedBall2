using AnimationEditor.Views.Dialogs;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace AnimationEditor.Views.Tests;

public class EditorDialogOverlayTests
{
    [AvaloniaFact]
    public async Task ConfirmAsync_ConfirmButton_ReturnsTrueAndHidesOverlay()
    {
        var overlay = new EditorDialogOverlay();
        var window = new Window { Content = overlay, Width = 800, Height = 600 };
        window.Show();
        try
        {
            var resultTask = EditorDialogs.ConfirmAsync(overlay, "Continue?", "Confirm");
            Dispatcher.UIThread.RunJobs();

            Assert.Contains(overlay.GetVisualDescendants().OfType<TextBlock>(),
                text => text.Text == "Confirm");
            var confirm = overlay.GetVisualDescendants()
                .OfType<Button>()
                .First(button => button.Content?.ToString() == "Yes");
            confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.True(await resultTask);
            Assert.False(overlay.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PromptStringAsync_Escape_ReturnsNull()
    {
        var overlay = new EditorDialogOverlay();
        var window = new Window { Content = overlay, Width = 800, Height = 600 };
        window.Show();
        try
        {
            var resultTask = EditorDialogs.PromptStringAsync(
                overlay, "Rename", "New name:", "Walk");
            Dispatcher.UIThread.RunJobs();

            overlay.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Source = overlay,
                Key = Key.Escape,
            });
            Dispatcher.UIThread.RunJobs();

            Assert.Null(await resultTask);
            Assert.False(overlay.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }
}
