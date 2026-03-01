# Collision Relationships in FlatRedBall2

`Screen.AddCollisionRelationship<A,B>` registers a pair of collidable groups. Each frame, after physics runs, every entity in group A is tested against every entity in group B. If they overlap, the configured response fires.

## Basic Setup

Call `AddCollisionRelationship` inside `Screen.CustomInitialize` after creating your factories:

```csharp
public class GameScreen : Screen
{
    private Factory<Player> _playerFactory = null!;
    private Factory<Wall>   _wallFactory   = null!;

    public override void CustomInitialize()
    {
        _playerFactory = new Factory<Player>(this);
        _wallFactory   = new Factory<Wall>(this);

        // Solid collision: player is pushed out, walls stay fixed
        AddCollisionRelationship<Player, Wall>(_playerFactory, _wallFactory)
            .MoveFirstOnCollision();
    }
}
```

## Fluent Modifiers

| Method | Effect |
|--------|--------|
| `.MoveFirstOnCollision()` | A gets pushed out, B stays fixed. Use for player vs. solid walls. |
| `.MoveSecondOnCollision()` | B gets pushed out, A stays fixed. |
| `.MoveBothOnCollision(firstMass, secondMass)` | Both objects share the separation weighted by mass. Equal masses = 50/50 split. |
| `.BounceOnCollision(firstMass, secondMass, elasticity)` | Reflects A's velocity off B using impulse physics. Separates positions automatically. |

### BounceOnCollision — Mass Semantics

```csharp
// Signature:
BounceOnCollision(float firstMass = 1f, float secondMass = 1f, float elasticity = 1f)
```

- **`firstMass = 0f`** — A (the ball) is fully displaced; B (the wall) stays fixed. Use this for a ball bouncing off immovable walls.
- **`secondMass = 0f`** — B is fully displaced; A stays fixed. Rarely used for bounce.
- **`elasticity = 1.0f`** — perfectly elastic (no energy loss). `0.9f` = 10% energy loss per bounce, ball slowly settles.

```csharp
// Ball bounces off immovable walls — ball moves, walls don't
AddCollisionRelationship(_balls, _walls)
    .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0.9f);

// Two-body elastic bounce (equal masses exchange velocities)
AddCollisionRelationship(_bulletsA, _bulletsB)
    .BounceOnCollision(firstMass: 1f, secondMass: 1f, elasticity: 1.0f);
```

> **Note:** `BounceOnCollision` only adjusts the velocity of A (the first group). B's velocity is unchanged when `firstMass == 0f`. `AdjustVelocityFrom` requires both objects to be `Entity` instances — it has no effect if either is a bare shape.

## Responding to Collision Events

Use the `CollisionOccurred` event for custom logic (damage, scoring, sound, etc.):

```csharp
AddCollisionRelationship<Bullet, Enemy>(_bullets, _enemies)
    .MoveFirstOnCollision()
    .CollisionOccurred += (bullet, enemy) =>
    {
        enemy.TakeDamage(bullet.Damage);
        bullet.Destroy();
    };
```

The event fires once per overlapping pair per frame, after separation has been applied.

## How Entity-vs-Entity Collision Works

`CollisionDispatcher` resolves collisions between concrete shape types (AABB vs AABB, Circle vs Circle, AABB vs Circle). When two entities collide, the dispatcher iterates their shape children and finds matching pairs.

For example, if `Player` has an `AxisAlignedRectangle` child and `Wall` has an `AxisAlignedRectangle` child, the dispatcher runs **AABB vs AABB** overlap and separation between those shapes. The parent entity's position is updated as a result.

Supported pairs:
- `AxisAlignedRectangle` vs `AxisAlignedRectangle`
- `Circle` vs `Circle`
- `AxisAlignedRectangle` vs `Circle` (and the reverse)
- `Polygon` vs `Polygon`

## Important: ShapeCollection as B Does Not Work

Passing a `ShapeCollection` as the second argument to `AddCollisionRelationship` hits the wildcard dispatch path and returns zero separation. **Use entity factories instead.** If you need reusable wall geometry, create a `Wall` entity class and a `Factory<Wall>`, then configure dimensions after `Create()`:

```csharp
// Correct — wall entities
AddCollisionRelationship<Player, Wall>(_playerFactory, _wallFactory)
    .MoveFirstOnCollision();

// Wrong — ShapeCollection as B returns zero separation
// AddCollisionRelationship<Player, ShapeCollection>(...) — do not do this
```

## Execution Order Each Frame

1. Physics update — `X += VelocityX*dt`, `Y += VelocityY*dt`, etc.
2. Collision resolution — all registered relationships run; positions are corrected
3. `CustomActivity(time)` — game logic runs with already-corrected positions

This means by the time `CustomActivity` executes, the entity is already separated from any overlapping collision partner.

## Common Pitfalls

- **Relationship not registered** — `AddCollisionRelationship` must be called from `CustomInitialize`. Relationships registered after the screen is running are still processed, but is unusual.
- **Both sides move when only one should** — Use `.MoveFirstOnCollision()` (player pushed, walls fixed) rather than `.MoveBothOnCollision()` for solid terrain.
- **Nothing happens on collision** — Confirm both entities have visible, correctly-sized shape children. The dispatcher only works with shape children, not the entity's own X/Y directly.
- **Player tunnels through thin walls at high speed** — FlatRedBall2 uses discrete (not continuous) collision detection. Keep velocities and wall thicknesses reasonable, or split fast movement into sub-steps.
