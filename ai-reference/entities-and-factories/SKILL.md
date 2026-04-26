---
name: entities-and-factories
description: "Entities and Factories in FlatRedBall2. Use when working with Entity subclasses, Factory<T>, spawning/creating/destroying entities, entity lifecycle, Add, shape children, CustomInitialize/CustomActivity, or Engine.GetFactory<T>(). Trigger on any entity creation, destruction, or factory question."
---

# Entities and Factories in FlatRedBall2

`Entity` is the base class for game objects. It owns position, velocity, acceleration, drag, and a list of child shapes for collision and rendering. `Factory<T>` manages creating, tracking, and destroying entity instances from within a `Screen`.

## Rules

1. **Always spawn through `Factory<T>`** — never `new MyEntity()`. Bypassing the factory breaks `Engine.GetFactory<T>()` and collision relationships. This applies even when there is only one instance (e.g., one ball in Pong).
2. **Override `CustomInitialize` for setup, `CustomActivity` for per-frame logic.** Add shape children, create input handlers, and wire references in `CustomInitialize`. The constructor is too early — `Engine` is null until the factory injects it (see `engine-overview`).
3. **Don't write properties whose only effect happens in `CustomInitialize`.** They look configurable but silently fail when assigned after `Create()` returns. Three fixes by case: expose the child shape directly (forwarding), pass init-only data through `Create(e => e.X = ...)` so it's set before `CustomInitialize` runs, or write a reactive setter for state the gameplay legitimately mutates. See `references/reactive-properties.md` — this is the most common entity-design footgun in FRB2.
4. **Don't create entities for static walls / floors / ceilings.** Use `TileShapeCollection` instead — see `collision-relationships`.

## Lifecycle Order

1. `Factory<T>.Create()` — allocates the entity, sets `Engine`, calls `AddEntity` on the screen
2. `CustomInitialize()` — called immediately after; add shape children and initialize input here
3. Each frame: physics update → collision resolution → `CustomActivity(time)`

## Minimal Entity Example

```csharp
public class Player : Entity
{
    private KeyboardInput2D _movement = null!;
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 40, Height = 40,
            Color = new Color(80, 140, 255, 220),
            IsVisible = true,
        };
        Add(Rectangle);

        _movement = new KeyboardInput2D(
            Engine.Input.Keyboard,
            Keys.Left, Keys.Right, Keys.Up, Keys.Down);
    }

    public override void CustomActivity(FrameTime time)
    {
        const float Speed = 200f;
        VelocityX = _movement.X * Speed;
        VelocityY = _movement.Y * Speed;
    }
}
```

`Rectangle` is exposed directly as a public auto-property so callers can write `player.Rectangle.Color = ...` at any time. Do not wrap it in a forwarding property like `Color` or `FillColor` — see `references/reactive-properties.md` for why.

For shape types and visual properties (`IsVisible`, `Color`, `IsFilled`, etc.), see the `shapes` skill. Shapes default to `IsVisible = false` — always set it explicitly.

## Using `Factory<T>` from a Screen

```csharp
public class GameScreen : Screen
{
    private Factory<Player> _playerFactory = null!;

    public override void CustomInitialize()
    {
        _playerFactory = new Factory<Player>(this);
        var player = _playerFactory.Create();
        player.X = 100; player.Y = 50;
    }
}
```

`Factory<T>` implements `IEnumerable<T>` — pass it directly to `AddCollisionRelationship`.

`Create(Action<T>)` runs the callback after engine injection but before `CustomInitialize`, so init-only fields are guaranteed-set when the entity reads them: `_asteroidFactory.Create(a => a.Size = AsteroidSize.Small)`. Use this instead of "create, then assign" whenever the value is consumed inside `CustomInitialize`. See `references/reactive-properties.md`.

`Factory<T>.Instances` exposes the live list as `IReadOnlyList<T>`:

```csharp
if (_brickFactory.Instances.Count == 0)
    MoveToScreen<NextLevelScreen>();
```

`Engine.GetFactory<T>()` looks up a factory by type — used when spawning from inside another entity. Throws if no factory for `T` exists yet on the screen.

## Destroying Entities

```csharp
enemy.Destroy();   // removes from factory, screen, and clears child shapes
```

`factory.Destroy(entity)` is equivalent. **Fields are invalid after `Destroy()`** — don't read state on an entity you just destroyed; use `factory.Instances.Count == 0` to detect when all are gone.

## Fire-and-Forget Effects

For short-lived visual entities the spawner doesn't want to keep a reference to — explosions, hit sparks, dust puffs, falling enemy bodies, damage numbers — skip the subclass and factory entirely. `Screen.CreateFireAndForget` builds and registers a one-shot `Entity` with a `Sprite` child and self-destroys when the animation finishes (or after a duration for the texture overload).

```csharp
// Plays once and destroys on AnimationFinished — IsLooping is forced to false
var fx = CreateFireAndForget(_explosionAchx, "Explode", x, y);

// Static texture for `duration` seconds, then destroys
var num = CreateFireAndForget(_damageTex, x, y, duration: 0.5f);
num.VelocityY = 60f;
```

The returned `Entity` is fully wired — set `Velocity`/`Acceleration`, `AttachTo` a parent, or `Add` shapes for collision before the next frame. Use a real `Entity` subclass + `Factory<T>` instead when the effect needs gameplay logic, queryable state, or a looping animation with timed cleanup.

## Entity.Name

Optional `string?` for identifying entities in tests and diagnostics. `SceneSnapshot.Named("player")` matches case-insensitively. Has no effect on collision, rendering, or lifecycle.

## See Also

- `references/reactive-properties.md` — property-vs-child-shape decision; the most common entity-design footgun
- `references/patterns.md` — render-only shapes (`isDefaultCollision`), solid-grid factories (`IsSolidGrid`), spawning from within an entity, death effects, particles, configuring after `Create()`
- `shapes` skill — shape types, visibility, color, render pipeline registration
- `collision-relationships` skill — `AddCollisionRelationship` over a `Factory<T>`, `TileShapeCollection`
- `levels` skill — `TileMap.CreateEntities` for designer-placed entities

## Common Pitfalls

- **Naming fields the same as `Entity` members.** `Acceleration`, `Velocity`, `Drag` already exist on `Entity` — shadowing them causes warnings.
- **Initializing input objects every frame.** Create `KeyboardInput2D` and similar in `CustomInitialize`, not `CustomActivity`.
- **`Add(child)` before `Engine` is set.** Auto-registration to the render pipeline only happens once `Engine` is set; Factory sets it before `CustomInitialize`, so `Add` works correctly there.
