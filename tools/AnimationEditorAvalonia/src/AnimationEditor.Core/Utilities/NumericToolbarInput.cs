using System;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// Pure parse/clamp rule backing <c>AnimationEditor.Views.Controls.FlankerNumericField</c>'s
/// commit path (the "[−][value][+]" numeric field shared by every toolbar/inspector/dialog
/// numeric input, #963).
/// </summary>
public static class NumericToolbarInput
{
    /// <summary>Evaluates <paramref name="text"/> as a <see cref="NumericEdit"/> (so <c>3 + 4</c>
    /// and a relative <c>* 2</c> both work, relative to <paramref name="fallback"/>), clamping to
    /// [<paramref name="min"/>, <paramref name="max"/>]. Returns <paramref name="fallback"/> if the
    /// text doesn't parse.</summary>
    public static decimal ParseClamp(string? text, decimal min, decimal max, decimal fallback)
        => NumericEdit.TryParse(text, out NumericEdit edit)
            ? Math.Clamp(edit.Apply(fallback), min, max)
            : fallback;
}
