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

    /// <summary>How long a reveal takes from full bump to settled size.</summary>
    public const float DefaultDurationSeconds = 0.5f;

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
