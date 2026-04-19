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

**Prefer `TileShapeCollection` over individual wall entities for static level geometry.** Individual entities sharing edges will cause the player to snag on seams between adjacent tiles because each entity maintains its own `RepositionDirections` independently. `TileShapeCollection` solves this by automatically suppressing interior shared edges. Use individual wall entities only when tiles need independent behavior (e.g., destructible blocks, moving platforms) — and for grid-shaped arrangements of those, set `Factory<T>.IsSolidGrid = true` so the factory applies the same seam suppression across adjacent cells (see `entities-and-factories` skill).

### OneWayDirection (jump-through / cloud platforms)

`relationship.OneWayDirection` restricts a relationship so separation only fires when the entity is being pushed in the configured direction. MVP implements `None` (default) and `Up`; `Down`/`Left`/`Right` throw `NotImplementedException` on the next collision pass.

```csharp
var cloudTiles = new TileShapeCollection { GridSize = 16f };
cloudTiles.AddTileAtCell(3, 5);

var playerVsClouds = AddCollisionRelationship(_playerFactory, cloudTiles);
playerVsClouds.OneWayDirection = OneWayDirection.Up;
playerVsClouds.AllowDropThrough = true; // opt in to Down+Jump drop-through for clouds
playerVsClouds.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);
```

Semantics for `Up` — three gates, all must pass:
- `sep.Y > 0` (separation pushes upward — skip otherwise, no `CollisionOccurred`).
- `VelocityY <= 0` (entity is falling or stationary — an upward-moving entity passes through even when SAT would push it onto the top).
- `LastPosition.Y` was at or above the post-separation Y (entity was cleanly on top last frame, not peaking inside the tile from below). On sloped tiles this is slope-aware: the surface-Y delta between `LastPosition.X` and current `X` is folded in, so uphill walking passes.

The X component of the separation is zeroed before applying — an entity clipping a cloud's side edge is lifted straight up, never shoved sideways. This matches FRB1 cloud behavior.

### Sloped cloud platforms (polygon jump-through tiles)

For sloped one-way tiles (polygon cells in the `TileShapeCollection`) you must also set `SlopeMode = PlatformerFloor`. Without it the collection falls back to SAT, the slope-aware LastPosition gate doesn't run, and the player falls through while walking uphill.

```csharp
var cloudTiles = new TileShapeCollection { GridSize = 16f };
cloudTiles.AddPolygonTileAtCell(3, 5, rightAscendingSlopePrototype);

var playerVsClouds = AddCollisionRelationship(_playerFactory, cloudTiles);
playerVsClouds.OneWayDirection = OneWayDirection.Up;
playerVsClouds.AllowDropThrough = true;
playerVsClouds.SlopeMode = SlopeCollisionMode.PlatformerFloor; // required for sloped clouds
playerVsClouds.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);
```

### `AllowDropThrough` — cloud platforms vs. hard one-way barriers

`OneWayDirection` and player drop-through are intentionally separate concerns:

- `AllowDropThrough = true` — this relationship honors
  `IPlatformerEntity.Platformer.IsSuppressingOneWayCollision`; when the player drop-through flag is active (Down+Jump, or airborne with Down held), the pair is skipped entirely. **Use for cloud platforms / jump-through floors.**
- `AllowDropThrough = false` (default) — player drop-through input is ignored; the relationship always blocks in the configured direction. **Use for hard one-way barriers** (e.g. Yoshi's Island ratchet doors) that should never be passable the wrong way.

Both forms are driven by the same `OneWayDirection` gate — the only difference is whether drop-through can bypass them. See the `platformer-movement` skill for drop-through wiring on the behavior.

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
