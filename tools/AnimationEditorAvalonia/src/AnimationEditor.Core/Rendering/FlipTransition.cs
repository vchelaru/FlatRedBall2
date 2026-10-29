namespace AnimationEditor.Core.Rendering;

/// <summary>A frame's flip flags and sprite offset at one moment; the two ends of a <see cref="FlipTransition"/>.</summary>
public readonly record struct FlipPose(bool Horizontal, bool Vertical, bool Diagonal, float OffsetX, float OffsetY);

/// <summary>
/// The preview-only squash/stretch that plays when a frame's flip changes, so the user can see
/// which way the sprite turned. It never touches stored data; the frame already holds its new pose.
/// </summary>
/// <remarks>
/// A flip is a reflection, and the old and new poses differ by exactly one, so a straight blend of the
/// two 2x2 matrices is a squash: it collapses the sprite to a line on the mirror axis halfway through
/// (the vertical axis for H, the horizontal for V, the main diagonal for D) and opens it up the other side.
/// </remarks>
public static class FlipTransition
{
    public const float DurationSeconds = 0.2f;

    /// <summary>Smoothstep: slow at both ends so the squash does not look mechanical.</summary>
    public static float Ease(float t) => t * t * (3f - 2f * t);

    /// <summary>The (a, b, c, d) matrix, in the form of <see cref="FlipScaleCalculator.ComputeMatrix"/>, at progress <paramref name="t"/> (0 to 1).</summary>
    public static (float a, float b, float c, float d) Matrix(FlipPose from, FlipPose to, float t)
    {
        float e = Ease(t);
        var f = FlipScaleCalculator.ComputeMatrix(from.Horizontal, from.Vertical, from.Diagonal);
        var g = FlipScaleCalculator.ComputeMatrix(to.Horizontal, to.Vertical, to.Diagonal);
        return (
            f.a + (g.a - f.a) * e,
            f.b + (g.b - f.b) * e,
            f.c + (g.c - f.c) * e,
            f.d + (g.d - f.d) * e);
    }

    /// <summary>
    /// What to add to the frame's current (already new) offset so the sprite slides from its old offset
    /// to its new one. Zero once the transition is over.
    /// </summary>
    public static (float x, float y) OffsetDelta(FlipPose from, FlipPose to, float t)
    {
        float remaining = 1f - Ease(t);
        return ((from.OffsetX - to.OffsetX) * remaining, (from.OffsetY - to.OffsetY) * remaining);
    }
}
