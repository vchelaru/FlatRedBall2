using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// One-shot shrink-to-rest used to draw the eye to something that just appeared or was just
/// selected: the box starts enlarged and eases down to its real size. Shared by the PNG diff
/// region boxes (#606) and the wireframe frame-selection outline (#542). Deliberately never dips
/// under the final size — an undershoot that shrinks the box smaller than the region looks off
/// on this tool.
/// </summary>
public static class RevealAnimation
{
    // Where the box starts, as a multiple of its final size.
    private const float StartScale = 1.5f;

    /// <summary>
    /// Screen-space pixels a box is inflated by (per side) at the very start of a reveal,
    /// independent of zoom. A multiplicative bump (see <see cref="Scale"/>) shrinks to nothing
    /// when the box itself is tiny on-screen (e.g. zoomed out); a fixed pixel amount stays
    /// visible at any zoom, and reads as proportionally *bigger* on a small/zoomed-out box.
    /// </summary>
    public const float StartInflationPixels = 24f;

    /// <summary>How long a reveal takes from full bump to settled size.</summary>
    public const float DefaultDurationSeconds = 1f;

    /// <summary>Tick interval both hosts use to drive the reveal timer (~60 fps).</summary>
    public const float DefaultIntervalSeconds = 1f / 60f;

    /// <summary>
    /// Scale factor for a reveal at <paramref name="progress"/> (0 = just appeared, 1 = settled).
    /// Returns <see cref="StartScale"/> at 0 and 1.0 at 1, decreasing monotonically in between
    /// (easeOutCubic) so it never shrinks below the real size. Apply it to a box's on-screen size
    /// around its center.
    /// </summary>
    public static float Scale(float progress)
    {
        float t = Math.Clamp(progress, 0f, 1f);
        // easeOutCubic: fast grow-in that decelerates smoothly onto the final size, no undershoot.
        float easeOut = 1f - (1f - t) * (1f - t) * (1f - t);
        return StartScale - (StartScale - 1f) * easeOut;
    }

    /// <summary>
    /// Screen-space pixel inflation (per side) for a reveal at <paramref name="progress"/>
    /// (0 = just appeared, 1 = settled). Same easeOutCubic curve as <see cref="Scale"/>, but
    /// expressed as a fixed pixel amount — add it to each edge of an already screen-space rect
    /// (see <c>WireframeControl.InflateBy</c>), rather than multiplying the rect's own size.
    /// </summary>
    public static float InflationPixels(float progress)
    {
        float t = Math.Clamp(progress, 0f, 1f);
        float easeOut = 1f - (1f - t) * (1f - t) * (1f - t);
        return StartInflationPixels * (1f - easeOut);
    }

    /// <summary>
    /// Fraction of the reveal's progress (0..1) at which a dependent fade-in (e.g. resize
    /// handles) starts ramping up, via <see cref="HandleAlpha"/>. easeOutCubic decelerates hard
    /// near <c>progress=1</c>, so <see cref="InflationPixels"/> is already visually negligible
    /// well before then — waiting for full settle before starting a dependent animation reads as
    /// a dead pause. Starting the fade at this fraction and running it to completion by
    /// <c>progress=1</c> makes the two land together instead.
    /// </summary>
    public const float HandleFadeStartFraction = 0.6f;

    /// <summary>
    /// Opacity (0 = invisible, 1 = fully shown) for a dependent element that should fade in over
    /// the tail of the reveal — 0 for <paramref name="progress"/> up to
    /// <see cref="HandleFadeStartFraction"/>, then a linear ramp to 1 at <c>progress=1</c>. A
    /// short linear tail-fade (rather than a separately-timed animation) guarantees it finishes
    /// exactly when the reveal does, with no dead time and no independent duration to keep in sync.
    /// </summary>
    public static float HandleAlpha(float progress)
    {
        float t = Math.Clamp(progress, 0f, 1f);
        if (t <= HandleFadeStartFraction) return 0f;
        return (t - HandleFadeStartFraction) / (1f - HandleFadeStartFraction);
    }

    /// <summary>
    /// Advances <paramref name="progress"/> by one host tick of <paramref name="dtSeconds"/>
    /// toward 1 over <paramref name="durationSeconds"/>. Clamped to [0, 1].
    /// </summary>
    public static float StepProgress(
        float progress, float dtSeconds,
        float durationSeconds = DefaultDurationSeconds)
    {
        if (durationSeconds <= 0f) return 1f;
        return Math.Clamp(progress + dtSeconds / durationSeconds, 0f, 1f);
    }
}
