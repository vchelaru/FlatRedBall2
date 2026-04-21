---
name: entities-and-factories
description: "Entities and Factories in FlatRedBall2. Use when working with Entity subclasses, Factory<T>, spawning/creating/destroying entities, entity lifecycle, Add, shape children, CustomInitialize/CustomActivity, or Engine.GetFactory<T>(). Trigger on any entity creation, destruction, or factory question."
---

# Entities and Factories in FlatRedBall2

An `Entity` is the base class for all game objects. It owns position, velocity, acceleration, drag, and a list of child shapes for collision and rendering. `Factory<T>` manages creating, tracking, and destroying entity instances from within a Screen.

## Creating an Entity Subclass

```csharp
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

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

## Lifecycle Order

1. `Factory<T>.Create()` — allocates the entity, sets `Engine`, calls `AddEntity` on the screen
2. `CustomInitialize()` — called immediately after; add shape children and initialize input here
3. Each frame: physics update → collision resolution → `CustomActivity(time)`

## Shape Children

All three shape types can be attached with `Add`:

```csharp
var rect = new AxisAlignedRectangle { Width = 40, Height = 40, IsVisible = true };
Add(rect);

var circle = new Circle { Radius = 20, IsVisible = true };
Add(circle);

var poly = Polygon.CreateRectangle(40, 40);
poly.IsVisible = true;
Add(poly);
```

Shape position is relative to the parent entity's position.

## Excluding a Shape from Default Collision

Pass `isDefaultCollision: false` to attach a **shape** (`AxisAlignedRectangle`, `Circle`, `Polygon`) for rendering/positioning only. It will not participate in `CollidesWith` or any standard collision relationship. This overload requires `ICollidable` — it does **not** exist for `Sprite`. For non-collision renderables like `Sprite`, use plain `Add(child)`:

```csharp
// Visual range indicator — renders but never collides by default
var range = new Circle { Radius = 64f, IsFilled = false, IsVisible = true };
Add(range, isDefaultCollision: false);
```

Use `SetDefaultCollision` to toggle participation at runtime (idempotent — safe to call multiple times):

```csharp
SetDefaultCollision(range, true);   // include in default collision
SetDefaultCollision(range, false);  // exclude from default collision
```

"Default collision" is the set of shapes checked by standard collision relationships. A shape excluded from default collision can still be targeted explicitly when per-shape collision targeting is supported.

## Using Factory&lt;T&gt; from a Screen

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

`Factory<T>` implements `IEnumerable<T>`, so you can pass it directly to `AddCollisionRelationship`.

## Inspecting the Factory

`Factory<T>.Instances` exposes the live list as `IReadOnlyList<T>`:

```csharp
int remaining = _enemyFactory.Instances.Count;
if (_brickFactory.Instances.Count == 0)
    MoveToScreen<NextLevelScreen>();
```

## Configuring Entities After Create()

`Create()` returns the entity instance. Set position and shape dimensions after creation:

```csharp
var wall = _wallFactory.Create();
wall.X = x; wall.Y = y;
wall.Rectangle.Width = w;
wall.Rectangle.Height = h;
```

## Entity Properties Must Be Reactive (Not Config)

Entity properties that affect shapes or visuals must apply their changes immediately in the setter — not store a value for some future initialization step to read. Since properties are reactive, they can be set at any time: after `CustomInitialize`, after `Factory.Create()`, or mid-game. Timing does not matter.

**Wrong — "configure-then-initialize" pattern:**
```csharp
public class Asteroid : Entity
{
    public AsteroidSize Size { get; set; }  // does nothing on its own

    public override void CustomInitialize()
    {
        // Reads Size to decide radius — but Size wasn't set yet!
        float radius = Size == AsteroidSize.Small ? 10f : 30f;
        Add(new Circle { Radius = radius, IsVisible = true });
    }
}
```

**Right — reactive property, works at any time:**
```csharp
public class Asteroid : Entity
{
    private Circle _shape = null!;
    private AsteroidSize _size = AsteroidSize.Large;

    public AsteroidSize Size
    {
        get => _size;
        set { _size = value; _shape.Radius = value == AsteroidSize.Small ? 10f : 30f; }
    }

    public override void CustomInitialize()
    {
        _shape = new Circle { Radius = 30f, IsVisible = true };
        Add(_shape);
    }
}

// Caller — timing doesn't matter:
var asteroid = _asteroidFactory.Create();  // defaults to Large
asteroid.Size = AsteroidSize.Small;        // immediately resizes the shape
```

This eliminates any dependency on Factory/CustomInitialize timing. There is no need for a separate `Setup()` method or a `Create(Action<T>)` overload.

## Always Use Factory — Even for Single Instances

Even for a single entity (e.g., one ball in Pong), create it through `Factory<T>`. This keeps lifecycle, collision (`IEnumerable<T>`), and `Engine.GetFactory<T>()` all working consistently.

## Solid-Grid Factories (`IsSolidGrid`)

For factories whose entities form a regular grid of solid blocks (destructible brick rows, crate walls, etc.), set `factory.IsSolidGrid = true`. The factory then maintains each entity's first `AxisAlignedRectangle` child's `RepositionDirections` based on 4-neighbor adjacency — interior shared faces are suppressed so a mover glides across the row without snagging at seams. Same fix as `TileShapeCollection` does for tile grids, but for entity factories.

Cell size is inferred from the first entity's body; mismatched sizes throw. `TileMap.CreateEntities` automatically wraps its spawn loop in the factory's grid batch so RD is recomputed once at the end. For hand-authored bulk spawns, wrap the loop in `using (factory.BeginGridBatch()) { … }` — without batching, `Create` can't compute cell indices because `X`/`Y` aren't set until after `Create` returns.

## Spawning Entities from Tiled Object Layers

For designer-placed entities (coins, enemies, spawn points), use `TileMap.CreateEntities` instead of hardcoded positions. See the `levels` skill for details.

## Spawning Entities from Within Another Entity

```csharp
// Player.cs — spawns a Ball on Space press
public override void CustomActivity(FrameTime time)
{
    if (Engine.Input.Keyboard.WasKeyPressed(Keys.Space))
    {
        var ball = Engine.GetFactory<Ball>().Create();
        ball.X = X; ball.Y = Y;
        ball.VelocityY = 300f;
    }
}
```

`GetFactory<T>()` throws `InvalidOperationException` if no factory for `T` exists yet.

## Destroying Entities

```csharp
enemy.Destroy();   // removes from factory, screen, and clears child shapes
```

`factory.Destroy(entity)` is equivalent.

## Common Pitfalls

- **Avoid naming fields/constants the same as `Entity` members.** `Acceleration`, `Velocity`, `Drag` already exist on `Entity` — shadowing them causes warnings.
- **`Engine` is null in the constructor** — see `engine-overview` Key Design Rules. Use `CustomInitialize` instead.
- **Shapes default `IsVisible = false`** — see `shapes` skill. Always set `IsVisible = true`.
- **`Add(child)` only auto-registers to the render pipeline if `Engine` is set** — Factory sets `Engine` before `CustomInitialize`, so `Add` works correctly there.
- **`_movement` must be initialized once, not every frame** — create input objects in `CustomInitialize`.
- **Always use `Factory<T>`, never `new MyEntity()`** — bypassing Factory breaks `Engine.GetFactory<T>()` and collision relationships.
- **Don't create entities for static walls/floors/ceilings** — use `TileShapeCollection` instead (see `collision-relationships` skill).
- **Fields are invalid after `Destroy()`** — `Destroy()` removes the entity immediately. Don't read health or other fields on an entity you just destroyed; use `factory.Instances.Count == 0` to detect when all enemies are gone.

## Reference Files

For advanced patterns (death effects, particles, spawning from entities), see:
- `references/patterns.md` — Death effects, particle effects, configuring entities after Create()
