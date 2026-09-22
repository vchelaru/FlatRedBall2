namespace AnimationEditor.Core.Layout;

/// <summary>
/// Resolves a persisted layout dimension (e.g. the preview pane's row height, #904, or the
/// sidebar's column width, #1178) against sane bounds so a missing, corrupt, or otherwise
/// nonsensical stored value can never produce a broken window layout — a valid-JSON-but-bad-number
/// case the whole-file settings load's try/catch doesn't catch on its own.
/// </summary>
public static class PersistedDimensionValidator
{
    public static double Resolve(double? stored, double min, double max, double fallback)
    {
        if (stored is not { } value)
            return fallback;
        if (double.IsNaN(value) || double.IsInfinity(value))
            return fallback;
        if (value < min || value > max)
            return fallback;
        return value;
    }
}
