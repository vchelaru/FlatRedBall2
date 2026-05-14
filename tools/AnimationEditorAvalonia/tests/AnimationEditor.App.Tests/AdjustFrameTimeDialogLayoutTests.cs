using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Layout regression tests for the Adjust Frame Time dialog (#251).
/// Ensures the dialog uses SizeToContent instead of a fixed height so it
/// fits its content rather than leaving large empty regions.
/// </summary>
public class AdjustFrameTimeDialogLayoutTests
{
    /// <summary>
    /// Verifies that <see cref="MainWindow.BuildAdjustFrameTimeWindow"/> returns a
    /// Window whose height is driven by content (SizeToContent.Height) rather than
    /// a hardcoded pixel value.
    /// </summary>
    [AvaloniaFact]
    public void BuildAdjustFrameTimeWindow_UsesSizeToContentHeight_NotFixedHeight()
    {
        var window = MainWindow.BuildAdjustFrameTimeWindow();

        Assert.Equal(SizeToContent.Height, window.SizeToContent);
        Assert.True(double.IsNaN(window.Height),
            "Height must be unset (NaN) so the dialog sizes to its content rather than leaving empty space.");
    }
}
