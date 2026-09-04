using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for issue #1009: opening a file via the single-instance pipe (a second
/// process handing its .achx path to the already-running instance) must bring the window
/// forward, not just open the tab behind whatever else has focus.
/// </summary>
public class MainWindowForegroundTests
{
    [AvaloniaFact]
    public void BringToForeground_WhenMinimized_RestoresToNormal()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            window.WindowState = WindowState.Minimized;

            window.BringToForeground();

            Assert.Equal(WindowState.Normal, window.WindowState);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void BringToForeground_WhenAlreadyNormal_DoesNotThrowAndStaysNormal()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var ex = Record.Exception(() => window.BringToForeground());

            Assert.Null(ex);
            Assert.Equal(WindowState.Normal, window.WindowState);
        }
        finally
        {
            window.Close();
        }
    }
}
