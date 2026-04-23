using System;
using FlatRedBall.Glue.StateInterpolation;

namespace FlatRedBall2.Tweening;

/// <summary>
/// Entry points for tweening floats on <see cref="Entity"/> and <see cref="Screen"/>.
/// <para>
/// Tweens are advanced automatically each frame. Entity-scoped tweens are removed when the
/// entity is destroyed; screen-scoped tweens when the screen is destroyed.
/// </para>
/// <para>
/// Call sites need both <c>using FlatRedBall2.Tweening;</c> (for the extension methods) and
/// <c>using FlatRedBall.Glue.StateInterpolation;</c> (for <see cref="InterpolationType"/> and
/// <see cref="Easing"/>). See the tweening skill for the rationale.
/// </para>
/// </summary>
public static class TweeningExtensions
{
    /// <summary>
    /// Starts a tween on <paramref name="entity"/>. The returned <see cref="Tweener"/> is already
    /// running; its <c>PositionChanged</c> delegate is wired to <paramref name="setter"/>, which
    /// is invoked each frame with the interpolated value. The setter also receives exactly
    /// <paramref name="to"/> as its final call when the tween completes naturally — useful for
    /// setters with a precise terminal value (e.g. snapping a fade to fully transparent). The
    /// tween is dropped from the entity's internal list automatically on completion,
    /// <see cref="Tweener.Stop"/>, or <see cref="Entity.Destroy"/>.
    /// <para>
    /// <b>Setter fires twice on the completing frame</b> — once from the upstream <c>PositionChanged</c>
    /// at the near-final value, then again with exactly <paramref name="to"/>. Plain assignment
    /// is fine; setters with side effects should guard them.
    /// </para>
    /// <para>
    /// <b>Frozen while the screen is paused</b> — see <see cref="Screen.IsPaused"/>. Override
    /// <see cref="Entity.ShouldAdvanceTweens"/> to pause an individual entity's tweens
    /// (e.g. during a stun) without pausing the whole screen.
    /// </para>
    /// </summary>
    /// <param name="setter">Invoked each frame with the interpolated value. Must not be <c>null</c>.</param>
    /// <param name="from">Starting value passed to <paramref name="setter"/> on the first update.</param>
    /// <param name="to">Final value; passed to <paramref name="setter"/> exactly on natural completion.</param>
    /// <param name="duration">Tween length.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Tweener Tween(
        this Entity entity,
        Action<float> setter,
        float from,
        float to,
        TimeSpan duration,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        var tweener = CreateRunning(setter, from, to, duration, type, easing);
        entity._tweens.Add(tweener, setter, to);
        return tweener;
    }

    /// <summary>
    /// Starts a tween owned by <paramref name="screen"/>. Semantics match
    /// <see cref="Tween(Entity, Action{float}, float, float, TimeSpan, InterpolationType, Easing)"/>,
    /// but the tween lives for the screen's lifetime instead of an entity's. Use for tweens that
    /// have no natural entity owner (camera shakes, UI reveals, global fades). Prefer the entity
    /// overload whenever the setter writes to an entity or one of its children — the entity-scoped
    /// tween is cleared automatically if the entity dies mid-tween, eliminating use-after-destroy risk.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Tweener Tween(
        this Screen screen,
        Action<float> setter,
        float from,
        float to,
        TimeSpan duration,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        var tweener = CreateRunning(setter, from, to, duration, type, easing);
        screen._tweens.Add(tweener, setter, to);
        return tweener;
    }

    private static Tweener CreateRunning(
        Action<float> setter, float from, float to, TimeSpan duration,
        InterpolationType type, Easing easing)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        // The 5-arg Tweener ctor calls Start() internally then sets Running=false (upstream
        // quirk). Call Start() again so the returned tweener is actually advancing.
        var tweener = new Tweener(from, to, (float)duration.TotalSeconds, type, easing);
        tweener.Start();
        tweener.PositionChanged = new Tweener.PositionChangedHandler(setter);
        return tweener;
    }
}
