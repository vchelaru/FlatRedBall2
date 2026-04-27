using System;
using System.Threading.Tasks;
using FlatRedBall.Glue.StateInterpolation;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;

namespace FlatRedBall2.Tweening;

/// <summary>
/// Selects how RGB color channels are interpolated by the <see cref="Color"/> tween overloads.
/// </summary>
public enum ColorTweenMode
{
    /// <summary>
    /// Lerp each RGBA channel linearly. Matches <see cref="Color.Lerp(Color, Color, float)"/>.
    /// Cheapest mode and the natural choice for fades to/from black/white/transparent. Hue
    /// transitions through desaturated (gray) midtones — fine for similar colors, muddy for
    /// hue-distant pairs (e.g. red→blue passes through gray, not magenta).
    /// </summary>
    Rgb,

    /// <summary>
    /// Convert endpoints to HSV, lerp saturation/value/alpha linearly, and lerp hue along the
    /// shortest arc on the hue circle. Use for vivid hue sweeps where you want red→blue to
    /// pass through magenta rather than through gray.
    /// </summary>
    Hsv,
}

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
    /// <param name="entity">The entity to attach the tween to.</param>
    /// <param name="setter">The action invoked each frame with the interpolated value.</param>
    /// <param name="from">Starting value passed to <paramref name="setter"/> on the first update.</param>
    /// <param name="to">Final value; passed to <paramref name="setter"/> exactly on natural completion.</param>
    /// <param name="duration">Tween length.</param>
    /// <param name="type">The interpolation type (linear, bounce, elastic, etc).</param>
    /// <param name="easing">The easing curve applied to the interpolation type.</param>
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

    /// <summary>
    /// "Wiggles" a value from <paramref name="restValue"/> up to <c>restValue + amplitude</c> and
    /// back to rest, suitable for reaction effects (button bumps, hit pulses, juice). Unlike
    /// <see cref="Tween(Entity, Action{float}, float, float, TimeSpan, InterpolationType, Easing)"/>,
    /// the setter is invoked starting at <paramref name="restValue"/> on the first frame — there is
    /// no snap to a different starting value. The setter receives <paramref name="restValue"/>
    /// exactly on completion (terminal-precision snap).
    /// <para>
    /// <b>Stacking gotcha</b> — calling <c>Bump</c> while another tween (or another <c>Bump</c>) is
    /// driving the same property results in both setters firing each frame, which looks jittery.
    /// Stop the previous tween first if needed.
    /// </para>
    /// <para>
    /// <paramref name="amplitude"/> is the peak overshoot in the setter's units (negative values
    /// bump downward). <paramref name="curve"/> selects the shape — see <see cref="BumpCurve"/>.
    /// </para>
    /// </summary>
    public static Tweener Bump(
        this Entity entity,
        Action<float> setter,
        float restValue,
        float amplitude,
        TimeSpan duration,
        BumpCurve curve = BumpCurve.Elastic)
    {
        var tweener = CreateBump(setter, restValue, amplitude, duration, curve);
        // The bump's "to" is restValue — that's the terminal snap value the TweenList writes
        // after the upstream Tweener's last PositionChanged fires.
        entity._tweens.Add(tweener, setter, restValue);
        return tweener;
    }

    /// <summary>
    /// Screen-scoped equivalent of
    /// <see cref="Bump(Entity, Action{float}, float, float, TimeSpan, BumpCurve)"/>.
    /// Use only when no entity owns the property being bumped (camera, global UI). Prefer the
    /// entity overload when possible — it cleans up automatically when the entity is destroyed.
    /// </summary>
    public static Tweener Bump(
        this Screen screen,
        Action<float> setter,
        float restValue,
        float amplitude,
        TimeSpan duration,
        BumpCurve curve = BumpCurve.Elastic)
    {
        var tweener = CreateBump(setter, restValue, amplitude, duration, curve);
        screen._tweens.Add(tweener, setter, restValue);
        return tweener;
    }

    /// <summary>
    /// <see cref="Vector2"/>-valued tween. Drives a single underlying float tweener
    /// <c>0 → 1</c> and lerps between <paramref name="from"/> and <paramref name="to"/>
    /// each frame. The setter receives exactly <paramref name="to"/> on natural completion.
    /// All other semantics — pause behavior, <see cref="Tweener.Stop"/>, lifecycle cleanup —
    /// match the float overload.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Tweener Tween(
        this Entity entity,
        Action<Vector2> setter,
        Vector2 from,
        Vector2 to,
        TimeSpan duration,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        return entity.Tween(t => setter(Vector2.Lerp(from, to, t)),
            0f, 1f, duration, type, easing);
    }

    /// <summary>
    /// Screen-scoped <see cref="Vector2"/> tween. See the entity overload for semantics.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Tweener Tween(
        this Screen screen,
        Action<Vector2> setter,
        Vector2 from,
        Vector2 to,
        TimeSpan duration,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        return screen.Tween(t => setter(Vector2.Lerp(from, to, t)),
            0f, 1f, duration, type, easing);
    }

    /// <summary>
    /// <see cref="Color"/>-valued tween. <paramref name="mode"/> selects RGB (default) vs HSV
    /// blending — see <see cref="ColorTweenMode"/>. The setter receives exactly
    /// <paramref name="to"/> on natural completion in both modes.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Tweener Tween(
        this Entity entity,
        Action<Color> setter,
        Color from,
        Color to,
        TimeSpan duration,
        ColorTweenMode mode = ColorTweenMode.Rgb,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        var lerp = BuildColorLerp(from, to, mode);
        return entity.Tween(t => setter(lerp(t)), 0f, 1f, duration, type, easing);
    }

    /// <summary>
    /// Screen-scoped <see cref="Color"/> tween. See the entity overload for semantics.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Tweener Tween(
        this Screen screen,
        Action<Color> setter,
        Color from,
        Color to,
        TimeSpan duration,
        ColorTweenMode mode = ColorTweenMode.Rgb,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        var lerp = BuildColorLerp(from, to, mode);
        return screen.Tween(t => setter(lerp(t)), 0f, 1f, duration, type, easing);
    }

    /// <summary>
    /// Builds a <c>t → Color</c> closure for the chosen <see cref="ColorTweenMode"/>. HSV
    /// converts both endpoints once, up front, so per-frame work is just lerps. Hue uses the
    /// shortest arc on the hue circle (a red→blue tween crosses magenta, not gray).
    /// </summary>
    private static Func<float, Color> BuildColorLerp(Color from, Color to, ColorTweenMode mode)
    {
        if (mode == ColorTweenMode.Rgb)
            return t => Color.Lerp(from, to, t);

        var (h1, s1, v1) = from.ToHsv();
        var (h2, s2, v2) = to.ToHsv();
        // Shortest-arc hue: if the two hues are more than 180 degrees apart on the unbroken
        // [0, 360) circle, shift one by 360 so the linear lerp crosses the short way.
        if (h2 - h1 > 180f) h1 += 360f;
        else if (h1 - h2 > 180f) h2 += 360f;
        float a1 = from.A;
        float a2 = to.A;

        return t =>
        {
            float h = h1 + (h2 - h1) * t;
            float s = s1 + (s2 - s1) * t;
            float v = v1 + (v2 - v1) * t;
            float a = a1 + (a2 - a1) * t;
            var rgb = Colors.FromHsv(h, s, v);
            return new Color(rgb.R, rgb.G, rgb.B, (byte)System.Math.Round(a));
        };
    }

    /// <summary>
    /// Awaitable variant of <see cref="Tween(Entity, Action{float}, float, float, TimeSpan, InterpolationType, Easing)"/>.
    /// The returned <see cref="Task"/> completes when the tween finishes naturally — after the
    /// terminal-snap setter call has run, so the awaiter observes the exact <paramref name="to"/>
    /// value. Use this to chain tweens (<c>await this.TweenAsync(...); await this.TweenAsync(...);</c>)
    /// without manually duplicating the duration in <c>Engine.Time.Delay</c>.
    /// <para>
    /// <b>Cancellation</b> — if the entity is destroyed (or <see cref="Entity.StopAllTweens"/> is
    /// called) before the tween completes, the task is canceled and <c>await</c> throws
    /// <see cref="TaskCanceledException"/>. For fire-and-forget call sites (<c>_ = TweenAsync(...)</c>),
    /// the unobserved exception is swallowed silently — which is the intended behavior.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Task TweenAsync(
        this Entity entity,
        Action<float> setter,
        float from,
        float to,
        TimeSpan duration,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        return TweenAsyncCore(entity._tweens, setter, from, to, duration, type, easing);
    }

    /// <summary>
    /// Screen-scoped equivalent of
    /// <see cref="TweenAsync(Entity, Action{float}, float, float, TimeSpan, InterpolationType, Easing)"/>.
    /// Cancellation fires when the screen is destroyed or <see cref="Screen.StopAllTweens"/> is called.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Task TweenAsync(
        this Screen screen,
        Action<float> setter,
        float from,
        float to,
        TimeSpan duration,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        return TweenAsyncCore(screen._tweens, setter, from, to, duration, type, easing);
    }

    /// <summary>
    /// Awaitable <see cref="Vector2"/>-valued tween. See the float overload for cancellation semantics.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Task TweenAsync(
        this Entity entity,
        Action<Vector2> setter,
        Vector2 from,
        Vector2 to,
        TimeSpan duration,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        return TweenAsyncCore(entity._tweens, t => setter(Vector2.Lerp(from, to, t)),
            0f, 1f, duration, type, easing);
    }

    /// <summary>
    /// Screen-scoped awaitable <see cref="Vector2"/> tween.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Task TweenAsync(
        this Screen screen,
        Action<Vector2> setter,
        Vector2 from,
        Vector2 to,
        TimeSpan duration,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        return TweenAsyncCore(screen._tweens, t => setter(Vector2.Lerp(from, to, t)),
            0f, 1f, duration, type, easing);
    }

    /// <summary>
    /// Awaitable <see cref="Color"/>-valued tween. See the float overload for cancellation semantics
    /// and <see cref="ColorTweenMode"/> for blending modes.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Task TweenAsync(
        this Entity entity,
        Action<Color> setter,
        Color from,
        Color to,
        TimeSpan duration,
        ColorTweenMode mode = ColorTweenMode.Rgb,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        var lerp = BuildColorLerp(from, to, mode);
        return TweenAsyncCore(entity._tweens, t => setter(lerp(t)), 0f, 1f, duration, type, easing);
    }

    /// <summary>
    /// Screen-scoped awaitable <see cref="Color"/> tween.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is <c>null</c>.</exception>
    public static Task TweenAsync(
        this Screen screen,
        Action<Color> setter,
        Color from,
        Color to,
        TimeSpan duration,
        ColorTweenMode mode = ColorTweenMode.Rgb,
        InterpolationType type = InterpolationType.Linear,
        Easing easing = Easing.InOut)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        var lerp = BuildColorLerp(from, to, mode);
        return TweenAsyncCore(screen._tweens, t => setter(lerp(t)), 0f, 1f, duration, type, easing);
    }

    private static Task TweenAsyncCore(
        TweenList list, Action<float> setter, float from, float to, TimeSpan duration,
        InterpolationType type, Easing easing)
    {
        // RunContinuationsAsynchronously matches TimeManager.Delay — keeps the awaiter's
        // continuation off the engine's update stack so a long await chain can't unwind
        // through tween advancement.
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tweener = CreateRunning(setter, from, to, duration, type, easing);
        list.Add(tweener, setter, to, completed =>
        {
            if (completed) tcs.TrySetResult();
            else tcs.TrySetCanceled();
        });
        return tcs.Task;
    }

    private static Tweener CreateBump(
        Action<float> setter, float restValue, float amplitude, TimeSpan duration, BumpCurve curve)
    {
        if (setter == null) throw new ArgumentNullException(nameof(setter));
        var constants = BumpCurveConstants.For(curve);

        // Strategy: drive an upstream Tweener 0→1 over an extended duration, then pre-advance it
        // past the curve's "tCross" (first time the raw value crosses 1.0). The tail of the curve
        // — its overshoot, oscillation, and decay back to 1.0 — becomes the visible bump. Remap
        // each raw value (range [1, 1 + naturalOvershoot]) to the user's units so peak == amplitude
        // regardless of restValue's magnitude.
        float visibleSeconds = (float)duration.TotalSeconds;
        float totalSeconds = visibleSeconds / (1f - constants.TCross);
        float preAdvance = constants.TCross * totalSeconds;

        var tweener = new Tweener(0f, 1f, totalSeconds, constants.Type, constants.Easing);
        tweener.Start();
        // Pre-advance silently (no setter wired yet) so the user's setter never sees the
        // sub-tCross portion of the curve (the rising 0→1 ramp).
        tweener.Update(preAdvance);
        float excursion = constants.NaturalExcursion;
        // sign = +1 for curves whose tail goes ABOVE 1 (Elastic, Back) — raw - 1 is positive at
        // the peak, so we want it to map to +amplitude.
        // sign = -1 for curves whose tail goes BELOW 1 (Bounce) — raw - 1 is negative at the
        // dip, so we flip it so positive amplitude still produces a visible *upward* bump.
        float sign = constants.MirrorTail ? -1f : 1f;
        tweener.PositionChanged = new Tweener.PositionChangedHandler(raw =>
        {
            setter(restValue + amplitude * sign * (raw - 1f) / excursion);
        });
        return tweener;
    }
}
