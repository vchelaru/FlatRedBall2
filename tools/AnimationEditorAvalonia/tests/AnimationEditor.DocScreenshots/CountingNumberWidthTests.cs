using System.Globalization;
using System.Linq;
using AnimationEditor.App;
using AnimationEditor.App.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// Numbers that count in place keep one width as their digits change (#1635). Lives here, not in App.Tests, because
/// it needs real text shaping with the app's Inter font, whose digits are proportional; the App.Tests headless text
/// stub gives every glyph the same width, so a proportional font could never fail there (see <see cref="TestAppBuilder"/>).
/// </summary>
public class CountingNumberWidthTests
{
    private static MainWindow ShowWindow()
    {
        var window = TestHelpers.BuildServices().CreateMainWindow();
        window.Width = 1400;
        window.Height = 900;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void SequenceTimeReadout_KeepsTheSameWidth_WhateverDigitsItShows()
    {
        var window = ShowWindow();
        var readout = window.FindControl<TextBlock>("SequenceTimeText")!;
        readout.MinWidth = 0; // the toolbar's minimum width would hide a difference in the text itself

        double WidthOf(string text)
        {
            readout.Text = text;
            readout.Measure(Size.Infinity);
            return readout.DesiredSize.Width;
        }

        double narrow = WidthOf("0.11 s / 3.78 s");
        double wide = WidthOf("0.88 s / 3.78 s");

        Assert.Equal(narrow, wide, 0.01);
        window.Close();
    }

    [AvaloniaFact]
    public void ZoomField_KeepsTheSameDigitWidth_WhateverDigitsItShows()
    {
        var window = ShowWindow();
        var zoom = window.FindControl<ZoomControl>("SequenceTimelineZoom")!;
        var combo = zoom.GetLogicalDescendants().OfType<AutoCompleteBox>().Single();

        double WidthOf(string text) => new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(combo.FontFamily), combo.FontSize, Brushes.Black).Width;

        Assert.Equal(WidthOf("111%"), WidthOf("808%"), 0.01);
        window.Close();
    }
}
