using FlatRedBall.Glue.StateInterpolation;

namespace FlatRedBall2.Tweening;

/// <summary>
/// Per-<see cref="BumpCurve"/> constants needed to remap an upstream
/// <see cref="Tweener"/> 0→1 curve into a "rest → peak → rest" bump.
/// <para>
/// <b>TCross</b> is the first <c>t ∈ [0, 1]</c> at which the raw upstream curve first reaches 1.0
/// (inclusive crossing). The Bump implementation pre-advances the tweener past this point so
/// the visible portion is just the post-cross tail (oscillation/decay back to 1.0).
/// </para>
/// <para>
/// <b>NaturalExcursion</b> is the magnitude of the curve's farthest deviation from 1.0 *after*
/// TCross. For <see cref="BumpCurve.Elastic"/> and <see cref="BumpCurve.Back"/> the tail
/// overshoots above 1 (so this is <c>peakValue − 1</c>). For <see cref="BumpCurve.Bounce"/> the
/// tail dips below 1 (so this is <c>1 − minValue</c>).
/// </para>
/// <para>
/// <b>MirrorTail</b> selects the sign of the remap. <c>false</c> = tail goes above 1 (Elastic,
/// Back) and the visible value is <c>restValue + amplitude * (raw − 1) / NaturalExcursion</c>.
/// <c>true</c> = tail goes below 1 (Bounce) and the visible value is
/// <c>restValue + amplitude * (1 − raw) / NaturalExcursion</c>. Either way, peak == amplitude.
/// </para>
/// <para>
/// Values are locked in by <c>BumpConstantsTests</c>; if upstream changes a curve formula,
/// that test fails loudly so the table can be updated rather than the bump silently drifting.
/// </para>
/// </summary>
internal readonly record struct BumpCurveConstants(
    InterpolationType Type, Easing Easing, float TCross, float NaturalExcursion, bool MirrorTail)
{
    public static BumpCurveConstants For(BumpCurve curve) => curve switch
    {
        BumpCurve.Elastic => new(InterpolationType.Elastic, Easing.Out, TCross: 0.075f, NaturalExcursion: 0.373f, MirrorTail: false),
        BumpCurve.Back    => new(InterpolationType.Back,    Easing.Out, TCross: 0.370f, NaturalExcursion: 0.100f, MirrorTail: false),
        // Bounce.Out reaches 1.0 at t=1/2.75 ≈ 0.364, then dips to 0.75 (segment 2 minimum)
        // before re-converging to 1.0. Tail excursion is downward; MirrorTail flips the remap
        // sign so positive amplitude still produces an *upward* visible bump.
        BumpCurve.Bounce  => new(InterpolationType.Bounce,  Easing.Out, TCross: 0.364f, NaturalExcursion: 0.250f, MirrorTail: true),
        _ => throw new System.ArgumentOutOfRangeException(nameof(curve), curve, null),
    };
}
