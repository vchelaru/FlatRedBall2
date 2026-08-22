using System;
using System.Globalization;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// Pure parse/clamp rule backing <c>AnimationEditor.Views.Controls.FlankerNumericField</c>'s
/// commit path (the "[−][value][+]" numeric field shared by every toolbar/inspector/dialog
/// numeric input across the desktop and browser hosts, #963).
/// </summary>
public static class NumericToolbarInput
{
    /// <summary>Parses a decimal input (invariant culture), clamping to [<paramref name="min"/>, <paramref name="max"/>]. Falls back to <paramref name="fallback"/> if the text doesn't parse.</summary>
    public static decimal ParseClamp(string? text, decimal min, decimal max, decimal fallback)
        => decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v)
            ? Math.Clamp(v, min, max)
            : fallback;
}
