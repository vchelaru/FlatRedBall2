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
