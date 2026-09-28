using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AnimationEditor.App.Services;

/// <summary>
/// Minimal top-level window shown when a second launch can't reach the primary instance over
/// IPC. Built entirely in code (no XAML, no DI) since <c>App.OnFrameworkInitializationCompleted</c>
/// may need to show this before the full <c>MainWindow</c>/service graph is built at all.
/// </summary>
internal sealed class SingleInstanceRecoveryWindow : Window
{
    /// <summary>True once the user has clicked "Restart AnimationEditor".</summary>
    public bool RestartRequested { get; private set; }

    private SingleInstanceRecoveryWindow(string message, bool offerRestart)
    {
        Title = "AnimationEditor";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildContent(message, offerRestart);
    }

    public static SingleInstanceRecoveryWindow CreateHungDialog() =>
        new("A previous instance of AnimationEditor is frozen.", offerRestart: true);

    public static SingleInstanceRecoveryWindow CreateBusyDialog() =>
        new("AnimationEditor appears to be busy. Please wait and try again.", offerRestart: false);

    private Control BuildContent(string message, bool offerRestart)
    {
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });

        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        if (offerRestart)
        {
            var restart = new Button { Content = "Restart AnimationEditor" };
            restart.Click += (_, _) => ConfirmRestart();
            buttonRow.Children.Add(restart);
        }

        var dismiss = new Button { Content = offerRestart ? "Exit" : "OK" };
        dismiss.Click += (_, _) => Dismiss();
        buttonRow.Children.Add(dismiss);

        panel.Children.Add(buttonRow);
        return panel;
    }

    /// <summary>Handles "Restart AnimationEditor". Internal (not private) so tests can invoke it
    /// directly instead of simulating a real pointer click on the button.</summary>
    internal void ConfirmRestart()
    {
        RestartRequested = true;
        Close();
    }

    /// <summary>Handles "Exit"/"OK" — exposed for the same reason as <see cref="ConfirmRestart"/>.</summary>
    internal void Dismiss() => Close();
}
