using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// The one-shot "reveal" used to draw the eye to something that just appeared (the PNG diff region
/// boxes, #606): the box pops in enlarged then bounces a couple of times as it settles to its real
/// size, so a small or off-screen change is easy to spot. The double bounce reads as intentional
/// "juice" and catches the eye better than a single settle, because each change of direction is a
/// fresh motion cue.
/// </summary>
public static class RevealAnimation
{
    // Peak deviation from final size at progress 0: the box starts at 1 + Amplitude = 1.5×.
    private const float Amplitude = 0.5f;

    // Number of oscillations across the reveal — ~2 gives two visible bounces before settling.
    private const float Bumps = 2f;

    /// <summary>
    /// Scale factor for a reveal at <paramref name="progress"/> (0 = just appeared, 1 = settled).
    /// A damped cosine: starts at 1.5×, oscillates around 1.0 (over- and under-shooting) with the
    /// amplitude decaying to 0 at progress 1, so it lands exactly on the real size. Apply it to a
    /// box's on-screen size around its center.
    /// </summary>
    public static float Scale(float progress)
    {
        float t = Math.Clamp(progress, 0f, 1f);
        float decay = (1f - t) * (1f - t);   // quadratic falloff → smaller, gentler later bounces
        return 1f + Amplitude * decay * MathF.Cos(2f * MathF.PI * Bumps * t);
    }
}
