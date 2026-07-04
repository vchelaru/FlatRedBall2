using System.Globalization;
using AnimationEditor.App.Converters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies the ANIMATIONS-tree zebra converter maps parity + theme onto the right band brush.
/// Runs under [AvaloniaFact] so the real App's ThemeDictionaries (with <c>TreeRowStripe</c>) are loaded.
/// </summary>
public class StripeBrushConverterTests
{
    private static object? Convert(bool isOdd, ThemeVariant variant) =>
        StripeBrushConverter.Instance.Convert(
            new object?[] { isOdd, variant }, typeof(IBrush), null, CultureInfo.InvariantCulture);

    [AvaloniaFact]
    public void OddGroup_ResolvesBandBrush()
    {
        Assert.IsAssignableFrom<IBrush>(Convert(isOdd: true, ThemeVariant.Dark));
    }

    [AvaloniaFact]
    public void EvenGroup_ReturnsNull_ShowingCanvasBackground()
    {
        Assert.Null(Convert(isOdd: false, ThemeVariant.Dark));
    }

    [AvaloniaFact]
    public void BandBrush_DiffersBetweenLightAndDark()
    {
        var dark  = Assert.IsType<SolidColorBrush>(Convert(isOdd: true, ThemeVariant.Dark));
        var light = Assert.IsType<SolidColorBrush>(Convert(isOdd: true, ThemeVariant.Light));
        Assert.NotEqual(dark.Color, light.Color); // theme-aware: the band follows the variant
    }
}
