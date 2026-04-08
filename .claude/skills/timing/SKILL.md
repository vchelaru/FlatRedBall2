---
name: timing
description: "Timing in FlatRedBall2. Use when working with cooldowns, timers, delays, entity lifetimes, self-destruct, repeating events, or FrameTime.DeltaSeconds. Covers cooldown gates, repeating timers, and entity lifetime patterns."
---

# Timing in FlatRedBall2

The engine provides `FrameTime` to every `CustomActivity` call. Use `time.DeltaSeconds` (a `float`) to drive all time-based logic — cooldowns, repeating events, and entity lifetimes.

`FrameTime.SinceGameStart` is a `TimeSpan` (use `.TotalSeconds` for a float). It's useful for absolute timestamps but the countdown pattern below is simpler for most cases.

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

## Rate Accumulator (High-Frequency Events)

For events that fire multiple times per second (typewriter text, particle bursts, rapid-fire), use a `while` loop and subtract — not `if` and reset:

```csharp
// Field:
private float _charTimer = 0f;
private const float CharsPerSecond = 30f;

// In CustomActivity:
_charTimer += time.DeltaSeconds;
while (_charTimer >= 1f / CharsPerSecond && _charIndex < _fullText.Length)
{
    _charIndex++;
    _charTimer -= 1f / CharsPerSecond;  // carry remainder — don't reset to 0
}
```

- **`while` not `if`** — prevents dropped events when `DeltaSeconds` spikes (first frame, tab-back, debugger pause).
- **Subtract, don't reset** — preserves fractional remainder so rate stays accurate across frame boundaries.

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

### Gotchas

- **Always pass `Token`** to `DelaySeconds` and `DelayUntil`. Without it, a task created on one screen can complete after the screen has been destroyed and fire code against the new screen.
- **`DelayFrames` has no cancellation token** but is still cancelled on screen transition — continuations guarded by `Token` are not needed, but keep frame waits short regardless.
- **`async void` only at the top level** — use `async void` for `CustomInitialize`/event callbacks. Internal helpers should return `Task` and be `await`ed.
- **`DeltaSeconds` is not zero while paused** — timer fields decremented by `DeltaSeconds` in entity `CustomActivity` won't tick while paused because entity `CustomActivity` is skipped. But screen `CustomActivity` always runs, so timer fields there do still count down. Use `if (!IsPaused)` guards in screen code if needed.
- **`DelaySeconds` deadlocks if the screen is paused** — `DelaySeconds` counts screen time, which freezes when `PauseThisScreen()` is active. Never `await DelaySeconds(...)` from within a code path that has already called `PauseThisScreen()`. Use `DelayFrames` instead for timed sequences that need the screen paused.
