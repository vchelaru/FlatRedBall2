---
name: collision-relationships
description: "Collision Relationships in FlatRedBall2. Use when working with AddCollisionRelationship, MoveFirstOnCollision, BounceOnCollision, MoveBothOnCollision, CollisionOccurred / CollisionStarted / CollisionEnded events, collision response, collision setup, mass/elasticity, entity-vs-entity collision, screen boundaries, keeping entities in bounds, walls, floors, ceilings, static geometry, sensor shapes, awareness radius, trigger zones, or zone-enter/zone-exit detection. Trigger on any collision-related question."
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
| `.BounceFirstOnCollision(elasticity)` | A bounces off B; B stays fixed. Use for a ball vs. immovable walls. |
| `.BounceSecondOnCollision(elasticity)` | B bounces off A; A stays fixed. Mirror of the above when the static side is A. |
| `.BounceBothOnCollision(firstMass, secondMass, elasticity)` | Both sides bounce; separation splits by mass ratio (equal masses share equally). |
| `.BounceOnCollision(firstMass, secondMass, elasticity)` | Escape hatch for the asymmetric raw-mass case. Prefer the three above. |

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
    .BounceFirstOnCollision()
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

## Enter/Exit Events — `CollisionStarted` / `CollisionEnded`

`CollisionStarted` fires once on the first frame a pair begins overlapping. `CollisionEnded` fires once when a previously-overlapping pair stops overlapping. Use these instead of hand-rolling a per-frame `_wasOverlapping` bool.

```csharp
var rel = AddCollisionRelationship(_player, _iceZones);
rel.CollisionStarted += (_, zone) => _player.ApplyMovementProfile(zone.Profile);
rel.CollisionEnded   += (_, _)    => _player.ResetMovementProfile();
```

- **Fires on resolving relationships too**, not just triggers. `Started` is useful for footstep sounds, dust puffs, landing-animation triggers.
- **Order on entry frame:** physics response → `CollisionStarted` → `CollisionOccurred`.
- **Destroying a side mid-overlap fires `Ended` synchronously** on the same frame (e.g. inside a `CollisionOccurred` handler). The destroyed entity's shapes have already been cleared by the time the handler runs, so use the argument for identity only — don't query geometry.
- **Tunneling:** if an entity moves so fast it overlaps for zero frames, neither event fires. Same limitation as `CollisionOccurred`.
- **Zero-overhead when unused** — no tracking runs if neither event has a subscriber.

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
playerVsTiles.BounceFirstOnCollision(elasticity: 0f);

// Same solidTiles, different relationship, Standard SAT — no conflict.
AddCollisionRelationship(_ballFactory, solidTiles)
    .BounceFirstOnCollision(elasticity: 0.7f);
```

**Prefer `TileShapeCollection` over individual wall entities for static level geometry.** Individual entities sharing edges will cause the player to snag on seams between adjacent tiles because each entity maintains its own `RepositionDirections` independently. `TileShapeCollection` solves this by automatically suppressing interior shared edges. Use individual wall entities only when tiles need independent behavior (e.g., destructible blocks, moving platforms) — and for grid-shaped arrangements of those, set `Factory<T>.IsSolidGrid = true` so the factory applies the same seam suppression across adjacent cells (see `entities-and-factories` skill).

### OneWayDirection (jump-through / cloud platforms)

`relationship.OneWayDirection` restricts a relationship so separation only fires when the entity is being pushed in the configured direction. All four directions (`Up`/`Down`/`Left`/`Right`) plus `None` (default) are supported — `Up` for jump-through floors, `Down` for ceiling-only barriers, `Left`/`Right` for one-way doors.

```csharp
var cloudTiles = new TileShapeCollection { GridSize = 16f };
cloudTiles.AddTileAtCell(3, 5);

var playerVsClouds = AddCollisionRelationship(_playerFactory, cloudTiles);
playerVsClouds.OneWayDirection = OneWayDirection.Up;
playerVsClouds.AllowDropThrough = true; // opt in to Down+Jump drop-through for clouds
playerVsClouds.BounceFirstOnCollision(elasticity: 0f);
```

Semantics — three gates, all must pass:
- Separation has the correct sign on the gated axis (e.g. `sep.Y > 0` for `Up`, `sep.X < 0` for `Left`).
- Velocity on the gated axis matches the push direction or is zero (e.g. `VelocityY <= 0` for `Up` — an upward-moving entity passes through even when SAT would push it onto the top).
- `LastPosition` on the gated axis was at or beyond the post-separation position (entity was cleanly on the correct side last frame, not peaking inside from the wrong side). For `Up` on sloped tiles this is slope-aware (surface-Y delta between `LastPosition.X` and current `X` is folded in so uphill walking passes); other directions use a flat gate.

The off-axis component of the separation is zeroed before applying — an entity clipping a platform's edge is pushed in the gated direction only, never sideways. This matches FRB1 cloud behavior.

### Sloped cloud platforms (polygon jump-through tiles)

For sloped one-way tiles (polygon cells in the `TileShapeCollection`) you must also set `SlopeMode = PlatformerFloor`. Without it the collection falls back to SAT, the slope-aware LastPosition gate doesn't run, and the player falls through while walking uphill.

```csharp
var cloudTiles = new TileShapeCollection { GridSize = 16f };
cloudTiles.AddPolygonTileAtCell(3, 5, rightAscendingSlopePrototype);

var playerVsClouds = AddCollisionRelationship(_playerFactory, cloudTiles);
playerVsClouds.OneWayDirection = OneWayDirection.Up;
playerVsClouds.AllowDropThrough = true;
playerVsClouds.SlopeMode = SlopeCollisionMode.PlatformerFloor; // required for sloped clouds
playerVsClouds.BounceFirstOnCollision(elasticity: 0f);
```

### Moving-platform velocity transfer (automatic for platformer entities)

When an `IPlatformerEntity` lands on top of a regular `Entity` (separation pushes the platformer
upward), the engine automatically feeds the other entity's `VelocityX` into the platformer for
that frame — the player rides the platform and inherits its horizontal momentum on jump. No
opt-in flag; works on any relationship configured with the standard player setup. Tile
collections are excluded (tiles don't have a meaningful velocity). See `platformer-movement` for
the full picture.

### `AllowDropThrough` — cloud platforms vs. hard one-way barriers

`OneWayDirection` and player drop-through are intentionally separate concerns:

- `AllowDropThrough = true` — this relationship honors
  `IPlatformerEntity.Platformer.IsSuppressingOneWayCollision`; when the player drop-through flag is active (Down+Jump, or airborne with Down held), the pair is skipped entirely. **Use for cloud platforms / jump-through floors.**
- `AllowDropThrough = false` (default) — player drop-through input is ignored; the relationship always blocks in the configured direction. **Use for hard one-way barriers** (e.g. Yoshi's Island ratchet doors) that should never be passable the wrong way.

Both forms are driven by the same `OneWayDirection` gate — the only difference is whether drop-through can bypass them. See the `platformer-movement` skill for drop-through wiring on the behavior.

## Default vs Non-Default Collision Shapes

Every shape attached with `Add(shape)` joins the entity's **default collision** — it participates in every `CollisionRelationship` the entity is on. That's what you want for the main body, but *not* for auxiliary shapes like:

- **Awareness radius / trigger zone** — larger shape that fires events but shouldn't push the entity.
- **Weak spot** — shape hit by bullets but not by terrain.
- **Probe** (ledge-detect, wall-slide-detect) — small shape queried manually via `CollidesWith`.
- **Muzzle point / attach anchor** — positional reference only, no collision at all.

If any of those join default collision, the entity gets shoved around by its own probes or weak spots. The fix is a single flag:

```csharp
Body = new AxisAlignedRectangle { Width = 14, Height = 14 };
Add(Body); // default collision — participates in relationships

Sensor = new Circle { Radius = 64 };
Add(Sensor, isDefaultCollision: false); // attached, moves with entity, but NOT in default collision

_footProbe = new AxisAlignedRectangle { Width = 2, Height = 2, Y = -1 };
Add(_footProbe, isDefaultCollision: false); // manual-query only
```

`SetDefaultCollision(shape, bool)` flips the flag after the fact if needed.

### Using a non-default shape in its own relationship

Register the shape's selector on the relationship — default collision stays untouched, the non-default shape participates only in this one:

```csharp
AddCollisionRelationship<Enemy, Player>(_enemies, _playerFactory)
    .WithFirstShape(e => e.Sensor)
    .CollisionOccurred += (enemy, player) => enemy.Alert(player);

AddCollisionRelationship<Bullet, Enemy>(_bullets, _enemies)
    .WithSecondShape(e => e.WeakSpot)
    .CollisionOccurred += (b, e) => e.TakeCritical(b.Damage);
```

### Using a non-default shape for manual queries

Probes queried each frame don't need a relationship at all — just call `CollidesWith` directly:

```csharp
// Ledge detection
if (_patrolInput.X > 0f && !_rightFoot.CollidesWith(SolidCollision))
    _patrolInput.X = -1f;
```

**Footgun**: forgetting `isDefaultCollision: false` on a probe/sensor silently drags the entity into collision responses (e.g., a ground-check probe pushing the entity upward every frame → jitter). If an entity misbehaves near terrain, check every `Add` call for this flag first.

## Common Pitfalls

- **Both sides move when only one should** — Use `.MoveFirstOnCollision()` for solid terrain.
- **Nothing happens on collision** — Confirm both entities have visible, correctly-sized shape children.
- **Type argument mismatch on overloads** — `AddCollisionRelationship<Enemy>(_enemies, _players)` is not the 2-list overload. Use two type args for entity-vs-entity (`<Enemy, Player>`), one type arg only for self-collision, and no explicit type args for `TileShapeCollection`.
- **Player tunnels through thin walls** — Discrete collision detection; keep velocities reasonable.
- **Don't use a `DiedThisFrame` flag** — The frame order is collision → entity `CustomActivity` → screen `CustomActivity`. A flag set during collision is stale by the time the screen reads it. Instead, destroy entities directly in `CollisionOccurred` and detect cleared groups via `_factory.Instances.Count == 0`.
- **Platformer gotcha**: using the raw `BounceOnCollision(firstMass, secondMass, elasticity)` with swapped masses can make the player phase through the floor. Prefer `BounceFirstOnCollision` / `BounceSecondOnCollision` / `BounceBothOnCollision` — they name the intent and don't require decoding mass numbers at the call site.

## Bounce Family — Practical Defaults

Prefer the named methods; they expand into the mass numbers for you:

| Scenario | Call |
|----------|------|
| Moving entity bounces off static geometry (wall, floor, ceiling) | `.BounceFirstOnCollision(elasticity)` |
| Static side is A, moving side is B (mirror of above) | `.BounceSecondOnCollision(elasticity)` |
| Entity vs entity, both sides move (equal or weighted masses) | `.BounceBothOnCollision(firstMass, secondMass, elasticity)` |
| Anything weirder | `.BounceOnCollision(firstMass, secondMass, elasticity)` (escape hatch) |

**Elasticity**: `1.0f` = perfectly elastic; `0.9f` = 10% energy loss per bounce; `0f` = no bounce.

> **Why name the intent?** In the raw `BounceOnCollision(firstMass, secondMass, elasticity)`, "which side moves" is encoded as a pair of numbers — `(0, 1, e)` means "first is displaced, second is fixed", same *shape* of call as `(1, 1, e)` for "both balls move." The named methods make the intent obvious without requiring the reader to decode masses.
>
> On the raw form: lower mass → more displacement. `firstMass = 0f` means A takes all the separation and B stays fixed (the wall case). Only matters when you need asymmetric non-0/non-1 masses, e.g. a light puck (mass 0.3) vs a heavier paddle (mass 1).

## Shape Dispatch

Collision between any two entity types just works — the engine inspects the shape children of each entity at runtime and resolves the overlap automatically. What shapes the entities contain doesn't matter; you don't need to know or specify them at the call site.

Concave `Polygon` shapes are fully supported: the engine automatically decomposes them into convex parts internally. No manual decomposition is needed and reading `CollisionDispatcher.cs` is never necessary.
