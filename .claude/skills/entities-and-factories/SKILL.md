---
name: entities-and-factories
description: "Entities and Factories in FlatRedBall2. Use when working with Entity subclasses, Factory<T>, spawning/creating/destroying entities, entity lifecycle, AddChild, shape children, CustomInitialize/CustomActivity, or Engine.GetFactory<T>(). Trigger on any entity creation, destruction, or factory question."
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
            Visible = true,
        };
        AddChild(Rectangle);

        _movement = new KeyboardInput2D(
            Engine.InputManager.Keyboard,
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

All three shape types can be attached with `AddChild`:

```csharp
var rect = new AxisAlignedRectangle { Width = 40, Height = 40, Visible = true };
AddChild(rect);

var circle = new Circle { Radius = 20, Visible = true };
AddChild(circle);

var poly = Polygon.CreateRectangle(40, 40);
poly.Visible = true;
AddChild(poly);
```

Shape position is relative to the parent entity's position.

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

## Always Use Factory — Even for Single Instances

Even for a single entity (e.g., one ball in Pong), create it through `Factory<T>`. This keeps lifecycle, collision (`IEnumerable<T>`), and `Engine.GetFactory<T>()` all working consistently.

## Spawning Entities from Within Another Entity

```csharp
// Player.cs — spawns a Ball on Space press
public override void CustomActivity(FrameTime time)
{
    if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.Space))
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
- **Shapes default `Visible = false`** — see `shapes` skill. Always set `Visible = true`.
- **`AddChild` only auto-registers to `RenderList` if `Engine` is set** — Factory sets `Engine` before `CustomInitialize`, so `AddChild` works correctly there.
- **`_movement` must be initialized once, not every frame** — create input objects in `CustomInitialize`.
- **Always use `Factory<T>`, never `new MyEntity()`** — bypassing Factory breaks `Engine.GetFactory<T>()` and collision relationships.

## Reference Files

For advanced patterns (death effects, particles, spawning from entities), see:
- `references/patterns.md` — Death effects, particle effects, configuring entities after Create()
