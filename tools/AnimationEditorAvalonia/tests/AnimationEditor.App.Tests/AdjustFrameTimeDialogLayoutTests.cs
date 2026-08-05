using AnimationEditor.Views.Dialogs;
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
    /// Verifies that the shared dialog requests content-driven height rather than
    /// a hardcoded pixel value.
    /// </summary>
    [AvaloniaFact]
    public void BuildAdjustFrameTimeWindow_UsesSizeToContentHeight_NotFixedHeight()
    {
        var options = EditorDialogs.AdjustFrameTimeOptions;

        Assert.True(options.SizeToContentHeight);
        Assert.Null(options.Height);
    }
}
