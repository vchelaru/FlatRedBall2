---
name: collision-relationships
description: "Collision Relationships in FlatRedBall2. Use when working with AddCollisionRelationship, MoveFirstOnCollision, BounceOnCollision, MoveBothOnCollision, CollisionOccurred events, collision response, collision setup, mass/elasticity, entity-vs-entity collision, screen boundaries, keeping entities in bounds, walls, floors, ceilings, static geometry, sensor shapes, awareness radius, or trigger zones. Trigger on any collision-related question."
---

# Collision Relationships in FlatRedBall2

`Screen.AddCollisionRelationship<A,B>` registers a pair of collidable groups. Each frame, after physics runs, every entity in group A is tested against every entity in group B. If they overlap, the configured response fires.

## Basic Setup

Call `AddCollisionRelationship` inside `Screen.CustomInitialize` after creating your factories:

```csharp
AddCollisionRelationship<Player, Wall>(_playerFactory, _wallFactory)
    .MoveFirstOnCollision();
```

## Self-Collision (Same List)

To collide entities within the same list (e.g., enemies pushing each other apart):

```csharp
AddCollisionRelationship<Enemy>(_enemyFactory)
    .MoveBothOnCollision(firstMass: 1f, secondMass: 1f);
```

This single-list overload iterates unique pairs only — no duplicate checks or self-collision.

## Fluent Modifiers

| Method | Effect |
|--------|--------|
| `.MoveFirstOnCollision()` | A gets pushed out, B stays fixed. Use for player vs. solid walls. |
| `.MoveSecondOnCollision()` | B gets pushed out, A stays fixed. |
| `.MoveBothOnCollision(firstMass, secondMass)` | Both objects share the separation weighted by mass. |
| `.BounceOnCollision(firstMass, secondMass, elasticity)` | Reflects A's velocity off B's surface normal using impulse physics. `firstMass: 0f` = A moves, B fixed (common for walls). |

## Responding to Collision Events

`CollisionOccurred` fires once per overlapping pair per frame, **after** position separation and velocity adjustment have been applied.

### Entity logic

```csharp
AddCollisionRelationship<Bullet, Enemy>(_bullets, _enemies)
    .MoveFirstOnCollision()
    .CollisionOccurred += (bullet, enemy) =>
    {
        enemy.TakeDamage(bullet.Damage);
        bullet.Destroy();
    };
```

### Physics customization

Override velocity after the engine's response:

```csharp
AddCollisionRelationship(_balls, _paddles)
    .BounceOnCollision(firstMass: 0f, secondMass: 1f)
    .CollisionOccurred += (ball, paddle) =>
    {
        ball.VelocityX = /* custom value */;
        ball.VelocityY = /* custom value */;
    };
```

### Triggers (no physics response)

Omit the fluent modifier entirely:

```csharp
AddCollisionRelationship(_balls, _deathZoneFactory)
    .CollisionOccurred += (ball, _) =>
    {
        _ballFactory.Destroy(ball);
        OnBallLost();
    };
```

## Execution Order

Collision runs after physics and before `CustomActivity` — by the time game logic runs, entities are already separated from any overlapping collision partner. See `engine-overview` for the full frame loop.

## Tile Collision (TileShapeCollection)

`TileShapeCollection` implements `ICollidable` and works as static geometry with the `entities, staticGeometry` overload:

```csharp
var tiles = new TileShapeCollection { GridSize = 16f };
tiles.AddTileAtCell(col, row);   // or AddTileAtWorld(x, y)

// TileShapeCollection has a dedicated overload — no explicit type arguments needed
AddCollisionRelationship(_playerFactory, tiles)
    .MoveFirstOnCollision();
```

`RepositionDirections` on adjacent tiles are maintained automatically — interior shared edges are cleared so entities glide across flat surfaces without snagging on seams.

### SlopeMode (per-relationship)

When one side is a `TileShapeCollection` containing polygon (slope) tiles, `relationship.SlopeMode` controls how overlap is resolved:

- `SlopeCollisionMode.Standard` (default) — SAT. Correct for top-down games and for non-player pairs (e.g., a ball bouncing off the same level tiles).
- `SlopeCollisionMode.PlatformerFloor` — vertical-only heightmap separation on floor slopes, with preferential landing. Use on the platformer player's relationship.

`SlopeMode` lives on the relationship — not the collection — specifically so the same level geometry can be used with different semantics per relationship (e.g., player = `PlatformerFloor`, kicked ball = `Standard`).

`PlatformerFloor` mode also **automatically contributes this collection as a ground-snap target** for any entity in the relationship that implements `IPlatformerEntity` — no separate "SnapTarget" wiring required. See the `platformer-movement` skill for how to configure `CollisionShape` / `SlopeSnapDistance` on the behavior.

```csharp
var playerVsTiles = AddCollisionRelationship(_playerFactory, solidTiles);
playerVsTiles.SlopeMode = SlopeCollisionMode.PlatformerFloor;
playerVsTiles.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);

// Same solidTiles, different relationship, Standard SAT — no conflict.
AddCollisionRelationship(_ballFactory, solidTiles)
    .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0.7f);
```

**Prefer `TileShapeCollection` over individual wall entities for static level geometry.** Individual entities sharing edges will cause the player to snag on seams between adjacent tiles because each entity maintains its own `RepositionDirections` independently. `TileShapeCollection` solves this by automatically suppressing interior shared edges. Use individual wall entities only when tiles need independent behavior (e.g., destructible blocks, moving platforms).

## Sensor Shapes (Awareness, Trigger Zones)

For non-physical overlap detection — e.g., enemy awareness radius, pickup range, trigger zones — add an extra shape child and target it with `WithFirstShape`.

Assume the entity already exposes `Body` (physical collider) and `Sensor` (larger trigger collider).

```csharp
AddCollisionRelationship(_enemies, solidTiles)
    .WithFirstShape(e => e.Body)
    .MoveFirstOnCollision();

AddCollisionRelationship<Enemy, Player>(_enemies, _playerFactory)
    .WithFirstShape(e => e.Sensor)
    .CollisionOccurred += (enemy, player) => enemy.Alert(player);
```

Direct `Vector2.Distance` checks are fine for simple one-off tests, but prefer sensor shapes when you want `CollisionOccurred` events, debug-shape visibility, or consistent integration with the collision pass.

## Common Pitfalls

- **Both sides move when only one should** — Use `.MoveFirstOnCollision()` for solid terrain.
- **Nothing happens on collision** — Confirm both entities have visible, correctly-sized shape children.
- **Type argument mismatch on overloads** — `AddCollisionRelationship<Enemy>(_enemies, _players)` is not the 2-list overload. Use two type args for entity-vs-entity (`<Enemy, Player>`), one type arg only for self-collision, and no explicit type args for `TileShapeCollection`.
- **Player tunnels through thin walls** — Discrete collision detection; keep velocities reasonable.
- **Don't use a `DiedThisFrame` flag** — The frame order is collision → entity `CustomActivity` → screen `CustomActivity`. A flag set during collision is stale by the time the screen reads it. Instead, destroy entities directly in `CollisionOccurred` and detect cleared groups via `_factory.Instances.Count == 0`.
- **Platformer gotcha**: swapping masses on BounceOnCollision can make the player phase through the floor.

## BounceOnCollision — Practical Defaults

```csharp
BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0.9f)
```
- **`firstMass = 0f`** — A is fully displaced; B stays fixed. Use for ball vs. immovable walls.
- **`secondMass = 0f`** — B is fully displaced; A stays fixed. Rarely used.
- **`elasticity = 1.0f`** — perfectly elastic. `0.9f` = 10% energy loss per bounce. `0f` = no bounce

>  **Wall/floor collisions**: keep `firstMass: 0f, secondMass: 1f` so the moving entity is separated from static geometry.
> **Note:** `BounceOnCollision` only adjusts A's velocity. B is unchanged when `firstMass == 0f`.

## Shape Dispatch

Collision between any two entity types just works — the engine inspects the shape children of each entity at runtime and resolves the overlap automatically. What shapes the entities contain doesn't matter; you don't need to know or specify them at the call site.

Concave `Polygon` shapes are fully supported: the engine automatically decomposes them into convex parts internally. No manual decomposition is needed and reading `CollisionDispatcher.cs` is never necessary.
