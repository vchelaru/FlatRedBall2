using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// The one-shot "reveal" used to draw the eye to something that just appeared (the PNG diff region
/// boxes, #606): the box starts enlarged and eases down to its real size, so it reads as a single
/// clean grow-and-settle rather than a static pop-in. Deliberately never dips under the final size —
/// an undershoot that shrinks the box smaller than the region looks off on this tool.
/// </summary>
public static class RevealAnimation
{
    // Where the box starts, as a multiple of its final size.
    private const float StartScale = 1.5f;

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
}
