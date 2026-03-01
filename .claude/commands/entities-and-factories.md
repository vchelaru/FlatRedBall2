# Entities and Factories in FlatRedBall2

An `Entity` is the base class for all game objects. It owns position, velocity, acceleration, drag, and a list of child shapes for collision and rendering. `Factory<T>` manages creating, tracking, and destroying entity instances from within a Screen.

## Creating an Entity Subclass

Subclass `Entity` and override the lifecycle hooks:

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
        // Engine is already set here — safe to access Engine.InputManager
        Rectangle = new AxisAlignedRectangle
        {
            Width = 40,
            Height = 40,
            Color = new Color(80, 140, 255, 220),
            Visible = true,
        };
        AddChild(Rectangle);   // auto-adds to RenderList because Engine is set

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
3. Each frame: physics update (`X += VelocityX*dt`, etc.) → collision resolution → `CustomActivity(time)`

## Shape Children

All three shape types can be attached with `AddChild`:

```csharp
// AxisAlignedRectangle — cannot rotate
var rect = new AxisAlignedRectangle { Width = 40, Height = 40, Visible = true };
AddChild(rect);

// Circle
var circle = new Circle { Radius = 20, Visible = true };
AddChild(circle);

// Polygon — supports rotation
var poly = Polygon.CreateRectangle(40, 40);
poly.Visible = true;
AddChild(poly);
```

Shape position is relative to the parent entity's position. A child at `X = 0, Y = 0` renders at the entity's center.

## Using Factory&lt;T&gt; from a Screen

```csharp
public class GameScreen : Screen
{
    private Factory<Player> _playerFactory = null!;

    public override void CustomInitialize()
    {
        _playerFactory = new Factory<Player>(this);  // pass the screen

        var player = _playerFactory.Create();        // entity is ready to use
        player.X = 100;
        player.Y = 50;
    }
}
```

`Factory<T>` implements `IEnumerable<T>`, so you can pass it directly to `AddCollisionRelationship`.

## Configuring Entities After Create()

`Create()` returns the entity instance. You can set position and shape dimensions after creation:

```csharp
private void SpawnWall(float x, float y, float w, float h)
{
    var wall = _wallFactory.Create();
    wall.X = x;
    wall.Y = y;
    wall.Rectangle.Width = w;    // Rectangle is a public property on the entity
    wall.Rectangle.Height = h;
}
```

## Spawning Entities from Within Another Entity

Entities can spawn other entities without receiving a factory reference. Call `Engine.GetFactory<T>()` — it returns the factory registered for that type. The factory is registered automatically when `new Factory<T>(screen)` is called in the screen's `CustomInitialize`.

```csharp
// Player.cs — spawns a Ball on Space press, no factory field needed
public override void CustomActivity(FrameTime time)
{
    if (Engine!.InputManager.Keyboard.WasKeyPressed(Keys.Space))
    {
        var ball = Engine.GetFactory<Ball>().Create();
        ball.X = X;
        ball.Y = Y;
        ball.VelocityY = 300f;
    }
}
```

`GetFactory<T>()` throws `InvalidOperationException` if no factory for `T` has been created yet — check that the screen calls `new Factory<Ball>(this)` before any entity tries to spawn one.

## Common Pitfalls

- **`Engine` is null in the entity constructor** — Do not call `AddChild` or access `Engine.InputManager` in the constructor. Use `CustomInitialize` instead.
- **`Visible` defaults to `false` on shapes** — Always set `Visible = true` or the shape won't render.
- **`AddChild` only auto-registers to `RenderList` if `Engine` is set** — Factory sets `Engine` before calling `CustomInitialize`, so `AddChild` inside `CustomInitialize` works correctly. If you create entities outside of Factory, call `screen.Register(entity)` first.
- **`_movement` must be initialized once, not every frame** — Create input objects in `CustomInitialize` and store them as fields. Creating them in `CustomActivity` is wasteful and allocates every frame.
