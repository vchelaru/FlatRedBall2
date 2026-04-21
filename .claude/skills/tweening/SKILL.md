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
var t = this.Tween(v => _circle.Radius = v, from: 0f, to: 100f, durationSeconds: 0.5f,
                   InterpolationType.Cubic, Easing.Out);

// Screen-scoped (for tweens with no natural entity owner).
this.Tween(v => Camera.Zoom = v, Camera.Zoom, 1.5f, 0.3f);
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
