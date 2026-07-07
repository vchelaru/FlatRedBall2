using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// The one-shot "reveal" used to draw the eye to something that just appeared (the PNG diff region
/// boxes, #606): the box starts enlarged and settles to its real size with a slight back-ease
/// overshoot, so it reads as a quick bounce rather than a static pop-in.
/// </summary>
public static class RevealAnimation
{
    // Where the box starts, as a multiple of its final size.
    private const float StartScale = 1.5f;

    // easeOutBack overshoot constant (standard tuning).
    private const float Overshoot = 1.70158f;

    /// <summary>
    /// Scale factor for a reveal at <paramref name="progress"/> (0 = just appeared, 1 = settled).
    /// Returns <see cref="StartScale"/> at 0 and 1.0 at 1, overshooting slightly under 1.0 near the
    /// end for the bounce. Apply it to a box's on-screen size around its center.
    /// </summary>
    public static float Scale(float progress)
    {
        float t = Math.Clamp(progress, 0f, 1f);
        float p = t - 1f;
        float easeOutBack = 1f + (Overshoot + 1f) * p * p * p + Overshoot * p * p;
        return StartScale - (StartScale - 1f) * easeOutBack;
    }
}
