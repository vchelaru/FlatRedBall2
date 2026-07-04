using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace AnimationEditor.App.Converters;

/// <summary>
/// Maps a tree row's zebra parity to its background band brush. Inputs (in order):
/// <c>IsOddStripe</c> (bool) and the row's <c>ActualThemeVariant</c>. Odd groups resolve the
/// theme's <c>TreeRowStripe</c> brush; even groups return <c>null</c> so the row shows the
/// TreeView's canvas background. The theme-variant input is bound purely so the value re-resolves
/// when the user switches light/dark at runtime — the band brush must follow the theme.
/// </summary>
public sealed class StripeBrushConverter : IMultiValueConverter
{
    public static readonly StripeBrushConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 1 || values[0] is not true)
            return null; // even group (or unset) → no band, show canvas

        var variant = values.Count > 1 && values[1] is ThemeVariant tv ? tv : ThemeVariant.Default;
        if (Application.Current is { } app
            && app.TryFindResource("TreeRowStripe", variant, out var res)
            && res is IBrush brush)
            return brush;

        return null;
    }
}
