# Init-Only Data vs Reactive Properties on Entities

The footgun: a property whose only effect happens inside `CustomInitialize` looks configurable in IntelliSense but silently does nothing when assigned after `Create()` returns — which is when callers naturally configure things.

Three branches cover what to do instead. Pick by asking what the data is for.

## Branch A — Pure forwarding: don't write the property, expose the child

If the property would just read or write a child shape's already-reactive property (`Color`, `Radius`, `Width`, etc.), delete it and expose the child:

```csharp
// Wrong — FillColor does nothing post-init
public class Pop : Entity
{
    public Color FillColor { get; set; }
    Circle _circle = null!;
    public override void CustomInitialize()
    {
        _circle = new Circle { Radius = 20, IsVisible = true, Color = FillColor };
        Add(_circle);
    }
}
```

Caller does `pop.FillColor = Red` after `Create()` — too late, the Circle was built with the default. Silent failure.

```csharp
// Right — expose the Circle directly
public class Pop : Entity
{
    public Circle Circle { get; private set; } = null!;
    public override void CustomInitialize()
    {
        Circle = new Circle { Radius = 20, IsVisible = true };
        Add(Circle);
    }
}
```

Caller writes `pop.Circle.Color = Red`. Works at any time — `Circle.Color` is read every frame by the renderer.

The wrapper bought nothing. It only manufactured a footgun.

## Branch B — Init-only derived data: take it via `Create(configure)`

If the data is consumed only by `CustomInitialize` (a size variant, spawn color seed, particle lifetime, an enemy archetype index) and the gameplay never reassigns it post-spawn, do not expose it as a property at all. Pass it through `Factory<T>.Create(Action<T>)`, which runs the callback after engine injection but **before** `CustomInitialize`:

```csharp
public class Asteroid : Entity
{
    public AsteroidSize Size { get; set; } = AsteroidSize.Large;
    Circle _shape = null!;
    public override void CustomInitialize()
    {
        float r = Size == AsteroidSize.Small ? 10f : 30f;
        _shape = new Circle { Radius = r, IsVisible = true };
        Add(_shape);
    }
}

// Spawn site:
var a = _asteroidFactory.Create(e => e.Size = AsteroidSize.Small);
```

`Size` is set before `CustomInitialize` runs, so the Circle is built at the right radius. No reactive setter ceremony for state the game never mutates.

The contract: anything assigned inside the configure callback is guaranteed-set when `CustomInitialize` reads it. Properties used this way should be plain auto-properties (no reactive setter needed) and can be `init`-only if you want to forbid post-spawn assignment.

## Branch C — Post-spawn mutable derived state: write a reactive setter

If the property encodes a value that drives multiple visual properties or doesn't have a 1:1 child correspondence **and** the gameplay legitimately changes it at runtime (a damage-flash tint, a charge-up scale, an enemy switching aggro state), it earns a real property — but the setter must apply changes immediately:

```csharp
public class Asteroid : Entity
{
    Circle _shape = null!;
    AsteroidSize _size = AsteroidSize.Large;

    public AsteroidSize Size
    {
        get => _size;
        set { _size = value; if (_shape != null) _shape.Radius = value == AsteroidSize.Small ? 10f : 30f; }
    }

    public override void CustomInitialize()
    {
        _shape = new Circle { Radius = _size == AsteroidSize.Small ? 10f : 30f, IsVisible = true };
        Add(_shape);
    }
}
```

Timing doesn't matter — `asteroid.Size = Small` resizes the shape immediately, whether called before or after `CustomInitialize`. A reactive setter also works for init-time configuration (callers can assign before or after `Create()`), so Branch C subsumes Branch B if you want one path. Prefer Branch B when the data is genuinely never reassigned — it's less code and the intent is clearer.

## Why FRB2 differs from Unity / Godot

In Unity, "reactive forwarding" can be expensive — re-instantiating child GameObjects, rebinding renderers, tearing down subscriptions. The ecosystem tolerates init-only public fields because the alternative has real cost. FRB2's renderer reads shape properties every frame; there is no rebuild cost. Init-only properties without a reactive setter or a `Create(configure)` entry point have no engineering justification here. They only manufacture footguns.

## Decision checklist

1. Does it map 1:1 to a child shape's existing reactive property? → Branch A. Delete the property, expose the child.
2. Is it init-only (read inside `CustomInitialize`, never reassigned by gameplay)? → Branch B. Pass it via `Create(e => e.X = ...)`.
3. Does gameplay change it at runtime? → Branch C. Write a reactive setter.

If none apply, the property probably shouldn't exist.
