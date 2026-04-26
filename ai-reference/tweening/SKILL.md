---
name: tweening
description: "Tweening / interpolation in FlatRedBall2. Use when animating a float over time with an easing curve — position, scale, alpha, rotation, UI slide-in, hit-flash, juice. Covers Entity.Tween vs Screen.Tween, lifetime rules, and the required usings."
---

# Tweening in FlatRedBall2

Built on the `FlatRedBall.InterpolationCore` NuGet. FRB2 ships two extension methods that return a running `Tweener`:

```csharp
using FlatRedBall2.Tweening;              // Entity.Tween / Screen.Tween
using FlatRedBall.Glue.StateInterpolation; // InterpolationType, Easing, Tweener

// Entity-scoped (primary) — tween dies with the entity. Target a child shape directly:
var t = this.Tween(v => _circle.Radius = v, from: 0f, to: 100f, duration: TimeSpan.FromSeconds(0.5),
                   InterpolationType.Cubic, Easing.Out);

// Screen-scoped (for tweens with no natural entity owner).
this.Tween(v => Camera.Zoom = v, Camera.Zoom, 1.5f, TimeSpan.FromSeconds(0.3));
```

## Two `using` directives — why

`InterpolationType`, `Easing`, and `Tweener` come from the upstream `FlatRedBall.Glue.StateInterpolation` namespace and are **not** re-exported under `FlatRedBall2.Tweening`. Gum already depends transitively on `FlatRedBall.InterpolationCore` (via `FlatRedBall.GumCommon`), so the upstream namespace is present in every FRB2 project whether you use tweens or not. Declaring FRB2 wrappers with the same names would create IntelliSense auto-import ambiguity — picking the wrong one silently compiles but produces the wrong types. One set of types everywhere beats two similar ones, even if the upstream namespace is ugly.

---

## Choosing the scope

**Use `Entity.Tween`** whenever the setter writes to an entity or its children. Destroying the entity clears its tweens automatically, so there is no use-after-destroy risk when the entity dies mid-tween.

**Use `Screen.Tween`** only when there is no entity owner — screen fades, global UI transitions, camera-level state.

If in doubt, pick Entity.

---

## Pause behavior

Tweens freeze while `Screen.IsPaused` is true, alongside the rest of the Activity pipeline. For finer-grained control, override `ShouldAdvanceTweens` on the Entity or Screen subclass — e.g., freeze tweens on a single stunned entity without pausing the whole screen.

---

## Gotchas

- **Setter fires twice on the completing frame.** The upstream `Tweener` fires `PositionChanged` with a near-`to` value, then the wrapper invokes the setter once more with exactly `to` so the final value is precise. Fine for plain assignment (`v => X = v`). If your setter has side effects, guard them.
- **`PositionChanged` is assigned, not subscribed.** The wrapper wires it with `=`. Additional listeners must use `+=` after `Tween(...)` returns.
- **`Ended` is an event.** Use `+=`. Fires once, after the final setter call, only if the tween completes (not on `Stop()`).
- **Vector2 / Color = multiple tweeners.** v1 is float-only. Start two or three tweeners and let each drive one component, or drive one tween and project in the setter (`v => Position = Vector2.Lerp(from, to, v)`).

---

## Bump — wiggle from rest

`Tween` interpolates *between two values* — calling `Tween(0 → 100, Elastic, Out)` on a property already at 100 first **snaps to 0** and then plays the curve. That snap is wrong for reaction effects (a button at rest that should "bump" when clicked, a hit-pulse on a sprite, a UI panel that wiggles on validation error).

Use `Bump` instead. It starts at `restValue`, peaks at exactly `restValue + amplitude`, and settles back to `restValue` — no snap.

```csharp
// Button at scale 1.0 bumps to 1.2 and settles back, with a classic elastic wiggle.
this.Bump(v => button.Scale = v, restValue: 1.0f, amplitude: 0.2f,
          duration: TimeSpan.FromSeconds(0.4));

// "Kick" feel — single overshoot, no oscillation.
this.Bump(v => sprite.X = v, restValue: 100f, amplitude: 8f,
          duration: TimeSpan.FromSeconds(0.25), curve: BumpCurve.Back);
```

`amplitude` is in the setter's units (pixels, scale, alpha) — independent of `restValue`.

### Curves

All three curves share the same high-level shape — start at `restValue`, peak at exactly `restValue + amplitude`, settle back to `restValue`. Only the tail differs:

- `BumpCurve.Elastic` (default) — oscillates above AND below rest with decreasing amplitude. Classic wiggle.
- `BumpCurve.Back` — single overshoot, then a smooth settle. No oscillation.
- `BumpCurve.Bounce` — oscillates between rest and progressively smaller peaks above rest. Never crosses below rest. Ball-settling feel.

Negative amplitude works for all three — the bump is mirrored: peak is *below* rest at `restValue + amplitude`, settles at rest.

### Gotchas

- **Stacking is not coordinated.** Calling `Bump` while another `Tween`/`Bump` is driving the same property results in both setters firing each frame and fighting. Stop the previous tween first if needed. Same engine-wide stacking limitation as `Tween`.
- **Terminal snap precision.** Like `Tween`, the final setter call is exactly `restValue`.
