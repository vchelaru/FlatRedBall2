using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure, frame-rate-independent easing for a "chase the target" zoom animation (#425).
/// Each <see cref="Step"/> moves the current zoom a fraction of the remaining distance
/// toward the target using exponential smoothing, so the curve is identical whether driven
/// by one large timestep or several small ones (a dropped frame doesn't change the feel).
/// <para>
/// Stateless by design: the caller owns the current zoom and the target. Feed each stepped
/// value into <see cref="CanvasTransform.ZoomToward"/> to apply it while keeping the cursor
/// pivot anchored — the animation only changes <em>how fast</em> the zoom scalar reaches its
/// destination, never <em>how</em> the destination (pan/scroll/clamp) is computed.
/// </para>
/// </summary>
public static class ZoomChase
{
    /// <summary>
    /// Smoothing time constant in seconds. The bulk of the motion (~95 %) completes in
    /// roughly 3× this value, so 0.045 s gives a ~135 ms perceived zoom. Lower = snappier.
    /// This is the single knob to tune the zoom feel.
    /// </summary>
    public const float DefaultTimeConstantSeconds = 0.045f;

    /// <summary>
    /// Relative distance (fraction of the target's magnitude) within which the chase snaps
    /// exactly to the target and reports settled, cutting the otherwise-asymptotic tail so
    /// the animation terminates instead of dribbling indefinitely.
    /// </summary>
    public const float SnapRelativeThreshold = 0.0015f;

    /// <summary>
    /// Eases <paramref name="current"/> toward <paramref name="target"/> over
    /// <paramref name="dtSeconds"/> and returns the new zoom. Snaps to the exact target once
    /// within <see cref="SnapRelativeThreshold"/> so repeated application terminates.
    /// </summary>
    /// <param name="timeConstantSeconds">Smoothing time constant; defaults to
    /// <see cref="DefaultTimeConstantSeconds"/>.</param>
    public static float Step(
        float current, float target, float dtSeconds,
        float timeConstantSeconds = DefaultTimeConstantSeconds)
    {
        if (IsSettled(current, target)) return target;

        // Exponential approach: the fraction of the remaining gap closed this tick is
        // 1 − e^(−dt/τ). Splitting dt into sub-steps yields the identical result because
        // e^(−(a+b)/τ) = e^(−a/τ)·e^(−b/τ) — hence frame-rate independence.
        float alpha = 1f - MathF.Exp(-dtSeconds / timeConstantSeconds);
        float next = current + (target - current) * alpha;
        return IsSettled(next, target) ? target : next;
    }

    /// <summary>
    /// True when <paramref name="current"/> is within <see cref="SnapRelativeThreshold"/> of
    /// <paramref name="target"/> (relative to the target's magnitude) — the chase should stop
    /// and hold the exact target.
    /// </summary>
    public static bool IsSettled(float current, float target)
        => MathF.Abs(target - current) <= SnapRelativeThreshold * MathF.Max(1e-4f, MathF.Abs(target));
}
