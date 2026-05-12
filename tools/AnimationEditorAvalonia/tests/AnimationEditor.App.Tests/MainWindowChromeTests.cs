using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

public class MainWindowChromeTests
{
    [AvaloniaFact]
    public void MainWindow_RemovesOsTitleBar()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TitleBar_ContainsMenuAndAppIdentity()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.NotNull(window.FindControl<Border>("TitleBarBorder"));
            Assert.NotNull(window.FindControl<Menu>("MainMenu"));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void TitleBar_HasWindowControlButtons()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Assert.NotNull(window.FindControl<Button>("MinimizeBtn"));
            Assert.NotNull(window.FindControl<Button>("MaximizeBtn"));
            Assert.NotNull(window.FindControl<Button>("CloseBtn"));
        }
        finally
        {
            window.Close();
        }
    }
}
