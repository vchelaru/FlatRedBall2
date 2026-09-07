using System.Linq;
using AnimationEditor.App.Services;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for the frozen/busy-primary recovery dialog (#1049). Button wiring is thin
/// enough to verify by invoking the handler methods directly (per animation-editor-testing's
/// last-resort guidance) rather than simulating a real pointer click.
/// </summary>
public class SingleInstanceRecoveryWindowTests
{
    [AvaloniaFact]
    public void ConfirmRestart_HungDialog_SetsRestartRequestedTrue()
    {
        var window = SingleInstanceRecoveryWindow.CreateHungDialog();
        window.Show();

        window.ConfirmRestart();

        Assert.True(window.RestartRequested);
    }

    [AvaloniaFact]
    public void CreateBusyDialog_HasNoRestartButton()
    {
        var window = SingleInstanceRecoveryWindow.CreateBusyDialog();

        var buttonRow = (StackPanel)((StackPanel)window.Content!).Children[1];
        var hasRestartButton = buttonRow.Children.OfType<Button>()
            .Any(b => b.Content as string == "Restart Animation Editor");

        Assert.False(hasRestartButton);
    }

    [AvaloniaFact]
    public void Dismiss_BusyDialog_LeavesRestartRequestedFalse()
    {
        var window = SingleInstanceRecoveryWindow.CreateBusyDialog();
        window.Show();

        window.Dismiss();

        Assert.False(window.RestartRequested);
    }
}
