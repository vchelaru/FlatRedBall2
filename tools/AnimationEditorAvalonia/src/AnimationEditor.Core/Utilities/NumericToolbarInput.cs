using System;
using System.Globalization;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// Pure parse/clamp/format rules for the toolbar's numeric text inputs (grid size, playback
/// speed). Shared by the desktop and browser hosts so the two can't drift out of sync.
/// </summary>
public static class NumericToolbarInput
{
    /// <summary>Formats a speed value for display, trimming trailing zeros beyond one decimal (e.g. 1.25 -> "1.25", 2.0 -> "2.0").</summary>
    public static string FormatSpeed(double speed) => speed.ToString("0.0#");

    /// <summary>Parses a grid-size input, clamping to [1, 512]. Falls back to 16 if the text doesn't parse or is below 1.</summary>
    public static int ParseGridSize(string? text)
        => int.TryParse(text, out int v) && v >= 1 ? Math.Min(v, 512) : 16;

    /// <summary>Parses a playback-speed input (invariant culture), clamping to [0.1, 10.0]. Falls back to 1.0 if the text doesn't parse.</summary>
    public static double ParseSpeed(string? text)
        => double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v)
            ? Math.Clamp(v, 0.1, 10.0)
            : 1.0;
}
