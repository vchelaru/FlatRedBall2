---
name: collision-relationships
description: "Collision Relationships in FlatRedBall2. Use when working with AddCollisionRelationship, MoveFirstOnCollision, BounceOnCollision, MoveBothOnCollision, CollisionOccurred events, collision response, collision setup, mass/elasticity, or entity-vs-entity collision. Trigger on any collision-related question."
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
| `.BounceOnCollision(firstMass, secondMass, elasticity)` | Reflects A's velocity off B using impulse physics. `firstMass: 0f` = A moves, B fixed (common for walls). |

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

## Common Pitfalls

- **Both sides move when only one should** — Use `.MoveFirstOnCollision()` for solid terrain.
- **Nothing happens on collision** — Confirm both entities have visible, correctly-sized shape children.
- **Player tunnels through thin walls** — Discrete collision detection; keep velocities reasonable.
- **Don't use a `DiedThisFrame` flag** — The frame order is collision → entity `CustomActivity` → screen `CustomActivity`. A flag set during collision is stale by the time the screen reads it. Instead, destroy entities directly in `CollisionOccurred` and detect cleared groups via `_factory.Instances.Count == 0`.

## BounceOnCollision — Mass Semantics

```csharp
BounceOnCollision(float firstMass = 1f, float secondMass = 1f, float elasticity = 1f)
```

- **`firstMass = 0f`** — A is fully displaced; B stays fixed. Use for ball vs. immovable walls.
- **`secondMass = 0f`** — B is fully displaced; A stays fixed. Rarely used.
- **`elasticity = 1.0f`** — perfectly elastic. `0.9f` = 10% energy loss per bounce.

> **Note:** `BounceOnCollision` only adjusts A's velocity. B is unchanged when `firstMass == 0f`.

## Shape Dispatch

Collision between any two entity types just works — the engine inspects the shape children of each entity at runtime and resolves the overlap automatically. What shapes the entities contain doesn't matter; you don't need to know or specify them at the call site.
