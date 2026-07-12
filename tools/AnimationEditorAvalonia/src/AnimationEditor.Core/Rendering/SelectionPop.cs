using System;

namespace AnimationEditor.Core.Rendering;

/// <summary>
/// Pure, frame-rate-independent easing for the wireframe selection-outline "bump" (#542).
/// On selection change the outline starts enlarged/bold and eases to rest via the same
/// exponential chase as <see cref="ZoomChase"/> — a one-shot shrink-to-rest, never a loop.
/// <para>
/// Stateless: the caller owns the current bump <em>amount</em> (1 = full bump, 0 = rest).
/// Map amount to draw parameters with <see cref="OutlineExpandPx"/> and <see cref="StrokeWidth"/>.
/// </para>
/// </summary>
public static class SelectionPop
{
    /// <summary>Bump amount at the moment selection changes (full pop).</summary>
    public const float StartAmount = 1f;

    /// <summary>Bump amount once the outline has settled to its resting stroke.</summary>
    public const float RestAmount = 0f;

    /// <summary>
    /// Smoothing time constant in seconds. ~3× this is the perceived settle time, so 0.05 s
    /// gives a ~150 ms pop. Lower = snappier.
    /// </summary>
    public const float DefaultTimeConstantSeconds = 0.05f;

    /// <summary>
    /// Absolute amount at-or-below which the pop snaps to <see cref="RestAmount"/> and stops.
    /// Absolute (not relative) because the rest target is zero — <see cref="ZoomChase.IsSettled"/>
    /// is relative to target magnitude and would otherwise dribble for ~1 s toward 0.
    /// </summary>
    public const float SettleThreshold = 0.01f;

    /// <summary>Screen-space pixels to inflate each edge of the selected outline at full bump.</summary>
    public const float MaxExpandPx = 5f;

    /// <summary>Stroke width when settled — matches the historical hardcoded selected outline.</summary>
    public const float RestStrokeWidth = 1f;

    /// <summary>Stroke width at full bump.</summary>
    public const float PeakStrokeWidth = 2.5f;

    /// <summary>
    /// Eases <paramref name="amount"/> toward <see cref="RestAmount"/> over
    /// <paramref name="dtSeconds"/>. Snaps exactly to rest once within
    /// <see cref="SettleThreshold"/>.
    /// </summary>
    public static float Step(
        float amount, float dtSeconds,
        float timeConstantSeconds = DefaultTimeConstantSeconds)
    {
        if (IsSettled(amount)) return RestAmount;

        float next = ZoomChase.Step(amount, RestAmount, dtSeconds, timeConstantSeconds);
        return IsSettled(next) ? RestAmount : next;
    }

    /// <summary>True when <paramref name="amount"/> is at or below <see cref="SettleThreshold"/>.</summary>
    public static bool IsSettled(float amount) => amount <= SettleThreshold;

    /// <summary>Screen-space edge inflate for the current bump amount (0 at rest).</summary>
    public static float OutlineExpandPx(float amount) => amount * MaxExpandPx;

    /// <summary>Stroke width for the current bump amount (<see cref="RestStrokeWidth"/> at rest).</summary>
    public static float StrokeWidth(float amount) =>
        RestStrokeWidth + amount * (PeakStrokeWidth - RestStrokeWidth);
}
