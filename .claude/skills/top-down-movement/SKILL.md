---
name: top-down-movement
description: "Top-Down Movement in FlatRedBall2. Use when implementing top-down movement mechanics including 4-way or 8-way movement, acceleration/deceleration, direction facing, or any bird's-eye-view player movement. Trigger on any top-down movement question."
---

# Top-Down Movement

## Overview

Top-down movement is provided by two classes in `FlatRedBall2.Movement`:

- **`TopDownValues`** — a plain data class holding movement parameters
- **`TopDownBehavior`** — a component added to an entity that reads input and applies velocity/acceleration each frame

## Minimal Setup

```csharp
public class Player : Entity
{
    private readonly TopDownBehavior _topDown = new();

    public override void CustomInitialize()
    {
        var values = new TopDownValues
        {
            MaxSpeed = 200f,
            UsesAcceleration = false,
        };

        _topDown.MovementValues = values;

        var keyboard = Engine.Input.Keyboard;
        _topDown.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down);
    }

    public override void CustomActivity(FrameTime time)
    {
        _topDown.Update(this, time);  // call from CustomActivity (after collisions)
    }
}
```

## TopDownValues Fields

| Field | Description |
|---|---|
| `MaxSpeed` | Maximum speed in world units/sec |
| `AccelerationTime` | Seconds to reach `MaxSpeed` from rest. 0 = instant |
| `DecelerationTime` | Seconds to stop from `MaxSpeed`. 0 = instant |
| `UsesAcceleration` | If false, velocity is set directly each frame (instant response) |
| `UpdateDirectionFromInput` | If true (default), `DirectionFacing` follows input direction |
| `UpdateDirectionFromVelocity` | If true and `UpdateDirectionFromInput` is false, `DirectionFacing` follows actual velocity |
| `IsUsingCustomDeceleration` | If true, uses `CustomDecelerationValue` when entity exceeds `MaxSpeed` (e.g. after a knockback) |
| `CustomDecelerationValue` | Deceleration magnitude (units/s²) used when `IsUsingCustomDeceleration` is true |

## Acceleration Setup

```csharp
var values = new TopDownValues
{
    MaxSpeed = 200f,
    UsesAcceleration = true,
    AccelerationTime = 0.2f,   // 200ms to reach full speed
    DecelerationTime = 0.1f,   // 100ms to stop
};
```

When `UsesAcceleration` is true, the behavior blends between `AccelerationTime` and `DecelerationTime`
based on the angle between the current velocity and the desired direction. Perfectly reversing direction
uses `DecelerationTime`; pressing directly forward uses `AccelerationTime`.

## Direction Facing

```csharp
_topDown.PossibleDirections = PossibleDirections.FourWay;   // Right, Up, Left, Down
_topDown.PossibleDirections = PossibleDirections.EightWay;  // + diagonals (default)

TopDownDirection dir = _topDown.DirectionFacing;
// e.g. TopDownDirection.Up, .DownLeft, etc.
```

## Direction to Vector2

`TopDownDirectionExtensions.ToVector2()` converts a `TopDownDirection` to a normalized world-space `Vector2` (Y+ up):

```csharp
using FlatRedBall2.Movement;

Vector2 offset = _topDown.DirectionFacing.ToVector2();
```

Useful for positioning a hitbox or spawning a projectile in front of the entity:

```csharp
// Place a sword hitbox 32 units in front of the player
var dir = _topDown.DirectionFacing.ToVector2();
_swordHitbox.X = X + dir.X * 32f;
_swordHitbox.Y = Y + dir.Y * 32f;
```

Diagonal directions return a normalized vector (magnitude 1.0), not `(1, 1)`.

## Speed Multiplier

```csharp
_topDown.SpeedMultiplier = 0.5f;  // half speed (e.g. in mud)
_topDown.SpeedMultiplier = 1f;    // normal
```

Scales `MaxSpeed` without modifying the `TopDownValues` object.

## Reading State

```csharp
_topDown.DirectionFacing   // TopDownDirection enum — updated each frame
_topDown.SpeedMultiplier   // read/write, defaults to 1f
_topDown.InputEnabled      // set false to freeze input without destroying movement values
```

## Collision Setup

Use `MoveFirstOnCollision` for standard top-down (no bounce needed):

```csharp
screen.AddCollisionRelationship(playerList, solidTiles)
      .MoveFirstOnCollision();
```

## AI Movement (enemies, NPCs)

Create a class in the project that is a settable `I2DInput` — so AI entities share the same acceleration, deceleration, and `DirectionFacing` logic as players. Set `X`/`Y` each frame before calling `Update`. Magnitudes > 1 are normalized automatically.

```csharp
private readonly DirectionalInput _aiInput = new();

// in CustomInitialize:
_topDown.MovementInput = _aiInput;

// in CustomActivity, before _topDown.Update:
var dir = Vector2.Normalize(new Vector2(_target.X - X, _target.Y - Y));
_aiInput.X = dir.X;
_aiInput.Y = dir.Y;
```

## Grid-Based (Tile-Snap) Movement — Pokémon / Classic Zelda Style

**Do not use `TopDownBehavior` for grid movement.** `TopDownBehavior` controls velocity, not position — the entity will stop at sub-tile coordinates. Grid-style movement requires a state machine that commits to a target tile center, locks input mid-step, and snaps on arrival.

**Do not use `CollisionRelationship` for solid tile collision in grid movement.** The standard collision system is move-then-correct (post-physics). Grid movement requires check-before-commit: query the target cell first, and only move if it's clear. Using both will produce confusing double-correction.

Key implementation points:
- Use `IsKeyDown` (not `WasKeyPressed`) — holding walks continuously, one tile at a time.
- Declare a public property on the entity and inject `_solidTiles` from the screen after `Factory.Create()`:

```csharp
// In the entity:
public TileShapeCollection? SolidTiles { get; set; }

// In the screen, after Factory.Create():
var player = _playerFactory.Create();
player.SolidTiles = _solidCollision;
```

- Use `SolidTiles?.GetTileAtWorld(nx, ny) == null` to check the target cell before committing the move.
- Snap to exact position on arrival (`X = _targetX`) — never accumulate sub-pixel drift.
- Don't use `CollisionRelationship` for NPC/entity collision during grid movement — check target coordinates directly before committing the step.

<!-- If this grid-movement section exceeds ~25 lines, move it to a dedicated grid-movement/SKILL.md skill. -->

## Gotchas

- Diagonal input magnitudes > 1 are clamped to the unit circle — full speed in 8 directions.
- `UpdateDirectionFromInput` defaults to `true`. Set it to `false` and `UpdateDirectionFromVelocity` to `true` if you want direction to lag behind input (e.g. tank-style).
- `InputEnabled = false` stops reading input but does not zero velocity — the entity will coast until friction/collision stops it.
