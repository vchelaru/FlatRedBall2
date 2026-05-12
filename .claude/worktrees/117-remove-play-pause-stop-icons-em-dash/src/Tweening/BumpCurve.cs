namespace FlatRedBall2.Tweening;

/// <summary>
/// Curve shape used by <see cref="TweeningExtensions.Bump(Entity, System.Action{float}, float, float, System.TimeSpan, BumpCurve)"/>
/// to animate from a rest value to a peak overshoot and back. Each value names a recognizable
/// "feel" rather than exposing the underlying interpolation type/easing pair.
/// </summary>
public enum BumpCurve
{
    /// <summary>Multiple decaying overshoots; classic "wiggle."</summary>
    Elastic,
    /// <summary>Single overshoot, then settles. Sharper than Elastic — no oscillation.</summary>
    Back,
    /// <summary>Multiple decaying bounces, like a ball settling. Visible value oscillates
    /// between rest and progressively smaller peaks at <c>restValue + amplitude</c>; never
    /// crosses past rest in the opposite direction (mirror of negative amplitude).</summary>
    Bounce,
}
