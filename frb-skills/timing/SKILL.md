---
name: timing
description: "Timing in FlatRedBall2. Use when working with cooldowns, timers, delays, entity lifetimes, self-destruct, repeating events, or FrameTime.DeltaSeconds. Covers cooldown gates, repeating timers, and entity lifetime patterns."
---

# Timing in FlatRedBall2

The engine provides `FrameTime` to every `CustomActivity` call. Use `time.DeltaSeconds` (a `float`) to drive all time-based logic — cooldowns, repeating events, and entity lifetimes.

`FrameTime.SinceGameStart` is a `TimeSpan` (use `.TotalSeconds` for a float). It's useful for absolute timestamps but the countdown pattern below is simpler for most cases.

### Scaled vs. Unscaled Delta

`FrameTime` carries two per-frame deltas:

- **`Delta` / `DeltaSeconds`** — multiplied by `Engine.Time.TimeScale`. This is what you want 99% of the time. If the player triggers slow-mo by setting `TimeScale = 0.5f`, gameplay using `DeltaSeconds` slows down with it.
- **`UnscaledDelta` / `UnscaledDeltaSeconds`** — the raw wall-clock delta, ignoring `TimeScale`. Use for things that should run at real-world speed regardless of slow-mo or fast-forward: UI animations, debug overlays, screen-shake decay, anything player-facing that would feel wrong if it slowed down with the game world.

"Unscaled" means **not multiplied by `TimeScale`**. It does *not* mean "ignores pause" — both `Delta` and `UnscaledDelta` keep advancing while a screen is paused; the difference is purely the scale multiplication. (Pausing is handled separately — see "Pause and `Entity.PauseMode`" below.)

For cumulative wall-clock time across the whole game, read `Engine.Time.UnscaledTimeSinceStart` (a `TimeSpan`).

## Pause and `Entity.PauseMode`

`Screen.PauseThisScreen()` suppresses entity physics, entity `CustomActivity`, and collision processing for the screen. `Screen.CustomActivity`, Gum UI, and input keep running — pause-menu logic still works.

By default, every entity is `PauseMode.Pausable` and freezes with the screen. To opt an individual entity out of pause suppression — e.g., a cursor entity, parallax background, or animated menu spinner that should keep ticking even while gameplay is frozen — set `entity.PauseMode = PauseMode.Always;` (typically in `CustomInitialize`). Its `PhysicsUpdate` and `CustomActivity` will run every frame regardless of `Screen.IsPaused`.

Note: collision processing for `Always` entities is still gated by screen pause — they move independently but won't interact with other entities until the screen unpauses. For UI-style entities that just need to animate or follow input, this is exactly what you want.

## TimeSpan Convention

Engine-wide rule: **public time-state and duration parameters use `TimeSpan`** (`Engine.Time.CurrentScreenTime`, `Engine.Time.UnscaledTimeSinceStart`, `Engine.Time.Delay(TimeSpan)`, `Tween` `duration:`, `AnimationFrame.FrameLength`, etc.). The **deliberate exception** is `FrameTime.DeltaSeconds`, which stays `float` because per-frame physics math (`Position += Velocity * dt`) would be hostile if it required `(float)Delta.TotalSeconds` at every call site.

For `Engine.Time.Delay`, a convenience overload `DelaySeconds(double)` is also available so call sites with literal seconds don't need `TimeSpan.FromSeconds(...)` ceremony — both forms are valid:

```csharp
await Engine.Time.Delay(TimeSpan.FromMilliseconds(500), Token); // canonical
await Engine.Time.DelaySeconds(0.5, Token);                     // convenience
```

---

## Cooldown Gate

Lets an action fire at most once per interval, only when triggered by input or a condition:

```csharp
// Field:
private float _fireCooldown = 0f;

// In CustomActivity:
_fireCooldown -= time.DeltaSeconds;
if (_fireCooldown <= 0f && firePressed)
{
    SpawnBullet();
    _fireCooldown = 0.25f;   // seconds until next shot allowed
}
```

## Repeating Timer

Fires unconditionally every N seconds — useful for AI actions, spawners, scripted beats:

```csharp
// Field (initialize to the desired interval):
private float _shootTimer = 2f;

// In CustomActivity:
_shootTimer -= time.DeltaSeconds;
if (_shootTimer <= 0f)
{
    SpawnBullet();
    _shootTimer = 2f;
}
```

## Entity Lifetime (Self-Destruct)

Entities that should expire after a fixed duration track their own remaining time and destroy themselves:

```csharp
public class Explosion : Entity
{
    private float _lifetime = 0.5f;

    public override void CustomActivity(FrameTime time)
    {
        _lifetime -= time.DeltaSeconds;
        if (_lifetime <= 0f)
            Destroy();
    }
}
```

To make the lifetime configurable at spawn time, expose it as a method or property:

```csharp
public class Particle : Entity
{
    private float _lifetime;

    public void Launch(float lifetimeSeconds) => _lifetime = lifetimeSeconds;

    public override void CustomActivity(FrameTime time)
    {
        _lifetime -= time.DeltaSeconds;
        if (_lifetime <= 0f)
            Destroy();
    }
}

// At spawn time:
var p = factory.Create();
p.X = X; p.Y = Y;
p.Launch(Engine.Random.Between(0.3f, 0.8f));
```

All three patterns are the same mechanism — a float field decremented by `DeltaSeconds` each frame — applied to different use cases.

---

## Async Delay APIs

`Engine.Time` exposes three awaitable helpers for sequencing game logic without manual state machines.

### `DelaySeconds` — pause-aware

```csharp
public override async void CustomInitialize()
{
    await Engine.Time.DelaySeconds(2.0, Token); // waits 2 s of screen time
    MoveToScreen<NextScreen>();
}
```

- Counts **screen time**, which freezes while `Screen.IsPaused` is true.
- Respects `Engine.Time.TimeScale` (slow-mo / fast-forward).
- Resets to zero on screen transition.
- Always pass `Token` (the screen's `CancellationToken`) so the task cancels automatically on screen change rather than completing against the new screen.

### `DelayUntil` — condition-based

```csharp
await Engine.Time.DelayUntil(() => _boss.IsDefeated, Token);
ShowVictoryUI();
```

Checked once per frame. Useful when the duration is unknown. Not pause-aware by design — the predicate is evaluated every frame regardless of pause state.

### `DelayFrames` — frame-count-based

```csharp
await Engine.Time.DelayFrames(2); // wait exactly 2 frames
```

Frame count always advances, even while paused — useful for UI sequencing that should not be affected by game pause.

### Waiting for Player Input

Use `DelayUntil` to bridge an event-driven UI action (e.g., a button click) into an async game loop, instead of polling with a flag in `CustomActivity`:

```csharp
BattleAction? _choice = null;
attackButton.Click += (_, _) => _choice = BattleAction.Attack;
fleeButton.Click   += (_, _) => _choice = BattleAction.Flee;

await Engine.Time.DelayUntil(() => _choice.HasValue, Token);
var chosen = _choice.Value;
```

> **Lifetime warning:** If the screen transitions before the player clicks, `Token` fires and the `await` cancels cleanly. Do not use `TaskCompletionSource.TrySetResult` from a button handler that may outlive the screen — the continuation would run against the new screen.

### Gotchas

- **Always pass `Token`** to `DelaySeconds` and `DelayUntil`. Without it, a task created on one screen can complete after the screen has been destroyed and fire code against the new screen.
- **`DelayFrames` has no cancellation token** but is still cancelled on screen transition — continuations guarded by `Token` are not needed, but keep frame waits short regardless.
- **`async void` only at the top level** — use `async void` for `CustomInitialize`/event callbacks. Internal helpers should return `Task` and be `await`ed.
- **`DeltaSeconds` is not zero while paused** — timer fields decremented by `DeltaSeconds` in entity `CustomActivity` won't tick while paused because entity `CustomActivity` is skipped. But screen `CustomActivity` always runs, so timer fields there do still count down. Use `if (!IsPaused)` guards in screen code if needed.
- **`DelaySeconds` freezes with the screen** — `DelaySeconds` counts screen time, which is paused by `PauseThisScreen()` and resumes on `UnpauseThisScreen()`. This is the right behavior for game logic ("wait 2 s, then spawn the boss" should not advance while the player is paused at a menu). For UI flows that should *keep* advancing during pause — paced dialog, animated menu reveals — use `DelayFrames` instead, which always ticks regardless of pause.

## Simulation Clock (Pauseable, Variable Speed)

Strategy and city-builder games need a game-wide clock that fires discrete "sim ticks" at a rate controlled by the player (pause / 1× / 2× / 3×). This is different from per-entity timers and from `DelaySeconds` (which counts real screen time and can't be sped up).

```csharp
// In Screen:
private float _simAccumulator;
private float _timeScale = 1f;   // 0 = paused, 1 = normal, 2 = fast, 3 = fastest
private const float TickInterval = 2f; // real seconds at 1× speed

// In CustomActivity:
_simAccumulator += time.DeltaSeconds * _timeScale;
while (_simAccumulator >= TickInterval)
{
    _simAccumulator -= TickInterval;
    RunSimTick();
}
```

**Why a `while` loop instead of `if`?** At high time scales, multiple ticks can accumulate in a single frame. Using `while` processes all of them before proceeding, which keeps the simulation deterministic.

**Changing speed mid-game:** Update `_timeScale` at any time — the accumulator continues from wherever it left off. Pausing is `_timeScale = 0f`; the accumulator freezes until resumed.

**Running ticks on the screen (not an entity)** is important: entity `CustomActivity` is skipped when the screen is paused, but screen `CustomActivity` always runs. Keep the accumulator and `RunSimTick()` call on the screen so you can gate it yourself with `_timeScale`.

### Pause Gate — Awaiting Player Input While Paused

To show a dialog and freeze the game until the player responds (NPC dialogue, skill selection, confirmation), pause the screen, display the UI, and `await DelayUntil` for the player's choice, then resume. `DelayUntil` evaluates its predicate every frame regardless of pause state, so it resolves the moment the player acts:

```csharp
PauseThisScreen();           // freeze entities; UI and input stay active
_dialogPanel.IsVisible = true;
await Engine.Time.DelayUntil(() => _playerChose, Token);
_dialogPanel.IsVisible = false;
UnpauseThisScreen();
```

`DelaySeconds` does **not** work here — it counts screen time, which is frozen while paused.
