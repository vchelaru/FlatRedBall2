namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Computes how much the fine (minor) grid lines should fade, and when the coarse (major)
/// grid lines should thin out, as the camera zooms out. Dense grid lines look like noise
/// at low zoom (#720).
/// </summary>
public static class GridFadeCalculator
{
    /// <summary>Zoom at/above which the fine grid renders at full opacity.</summary>
    public const float FadeStartZoom = 0.75f;

    /// <summary>Zoom at/below which the fine grid is fully invisible.</summary>
    public const float FadeEndZoom = 0.5f;

    /// <summary>
    /// Zoom at/below which every other major line is hidden (halving major-line density)
    /// on top of the fine grid already being fully invisible at this zoom.
    /// </summary>
    public const float MajorThinZoom = 0.25f;

    /// <summary>
    /// Returns the fine-grid opacity multiplier (0..1) for the given zoom: 1 at/above
    /// <see cref="FadeStartZoom"/>, 0 at/below <see cref="FadeEndZoom"/>, linearly
    /// interpolated in between.
    /// </summary>
    public static float MinorLineAlphaFactor(float zoom)
    {
        if (zoom >= FadeStartZoom) return 1f;
        if (zoom <= FadeEndZoom) return 0f;
        return (zoom - FadeEndZoom) / (FadeStartZoom - FadeEndZoom);
    }

    /// <summary>
    /// Returns whether a major grid line should render. <paramref name="majorLineIndex"/>
    /// is the count of major lines from the texture origin (0, 1, 2, ...; e.g. major line
    /// n=8 with a 4-line major interval is <paramref name="majorLineIndex"/>=2). At/below
    /// <see cref="MajorThinZoom"/> only even-indexed major lines render — the origin line
    /// (index 0) always stays visible.
    /// </summary>
    public static bool IsMajorLineVisible(int majorLineIndex, float zoom)
    {
        if (zoom > MajorThinZoom) return true;
        return (((majorLineIndex % 2) + 2) % 2) == 0;
    }
}
