# Entity Patterns Reference

## Configuring Entities at Spawn

Position, velocity, and child shape state set on the returned instance after `Create()` work freely — they don't feed back into `CustomInitialize`:

```csharp
private void SpawnWall(float x, float y, float w, float h)
{
    var wall = _wallFactory.Create();
    wall.X = x; wall.Y = y;
    wall.Rectangle.Width = w;
    wall.Rectangle.Height = h;
}
```

When the entity needs a value **inside** `CustomInitialize` (a size variant that determines starting radius, a spawn color used to seed multiple children, a particle's lifetime), pass it via the `Create(Action<T>)` overload — the callback runs before `CustomInitialize`:

```csharp
var asteroid = _asteroidFactory.Create(a => a.Size = AsteroidSize.Small);
```

For the full decision tree on property design (forwarding vs init-only vs reactive), see `reactive-properties.md`.

## Render-Only Shape Children (`isDefaultCollision: false`)

Pass `isDefaultCollision: false` to `Add` to attach a shape (`AARect`, `Circle`, `Polygon`) for rendering and positioning only — it will not participate in `CollidesWith` or any standard collision relationship. Useful for visual indicators like attack ranges, sight cones, or HUD-style overlays attached to an entity.

```csharp
// Visual range indicator — renders but never collides by default
var range = new Circle { Radius = 64f, IsFilled = false, IsVisible = true };
Add(range, isDefaultCollision: false);
```

This overload requires `ICollidable`; it does not exist for `Sprite`. For non-collision renderables like `Sprite`, use plain `Add(child)`.

Toggle participation at runtime with `SetDefaultCollision` (idempotent — safe to call repeatedly):

```csharp
SetDefaultCollision(range, true);   // include in default collision
SetDefaultCollision(range, false);  // exclude from default collision
```

"Default collision" is the set of shapes checked by standard collision relationships. A shape excluded from default collision can still be targeted explicitly when per-shape collision targeting is supported.

## Solid-Grid Factories (`IsSolidGrid`)

For factories whose entities form a regular grid of solid blocks (destructible brick rows, crate walls, etc.), set `factory.IsSolidGrid = true`. The factory then maintains each entity's first `AARect` child's `SolidSides` based on 4-neighbor adjacency — interior shared faces are suppressed so a mover glides across the row without snagging at seams. Same fix as `TileShapes` does for tile grids, but for entity factories.

Cell size is inferred from the first entity's body; mismatched sizes throw. `TileMap.CreateEntities` automatically wraps its spawn loop in the factory's grid batch so RD is recomputed once at the end. For hand-authored bulk spawns, wrap the loop in `using (factory.BeginGridBatch()) { … }` — without batching, `Create` can't compute cell indices because `X`/`Y` aren't set until after `Create` returns.

## Spawning Entities from Within Another Entity

Use `Engine.GetFactory<T>()` to spawn from inside an entity's `CustomActivity`. Throws `InvalidOperationException` if no factory for `T` exists yet on the screen.

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

## Destroy and Spawn (Death Effects)

When an entity should trigger a visual effect on destruction, destroy it immediately and spawn a separate effect entity at the same position. Do not try to play an effect on the dying entity — once destroyed it is removed from the game.

```csharp
AddCollisionRelationship<Bullet, Enemy>(_bullets, _enemies)
    .CollisionOccurred += (bullet, enemy) =>
    {
        var explosion = Engine.GetFactory<Explosion>().Create();
        explosion.X = enemy.X;
        explosion.Y = enemy.Y;
        enemy.Destroy();
        bullet.Destroy();
    };
```

The effect entity manages its own lifetime — see the `timing` skill for the self-destruct pattern.

## Particle Effects

> **Future:** A dedicated particle tool is planned. For now, spawn short-lived entities using the entity lifetime pattern from the `timing` skill.

Spawn a burst from any entity:

```csharp
var factory = Engine.GetFactory<Particle>();
for (int i = 0; i < 12; i++)
{
    var p = factory.Create();
    p.X = X; p.Y = Y;
    p.Velocity = Engine.Random.RadialVector2(60f, 180f);
    p.Launch(lifetimeSeconds: Engine.Random.Between(0.3f, 0.8f));
}
```

Visual variety comes from randomizing velocity, color, size, and lifetime.
