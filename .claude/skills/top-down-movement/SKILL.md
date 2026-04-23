---
name: top-down-movement
description: "Top-Down Movement in FlatRedBall2. Use when implementing top-down movement mechanics including 4-way or 8-way movement, acceleration/deceleration, direction facing, or any bird's-eye-view player movement. Trigger on any top-down movement question."
---

> **Not for grid/tile-locked movement.** If one key press should move the player exactly one tile (Pokémon, dungeon crawler, roguelike), use the `grid-movement` skill instead. `TopDownBehavior` produces continuous analog movement — wrong for that use case.

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
| `DecelerationTime` | Seconds to stop from `MaxSpeed`. 0 = instant. Setting either accel or decel time to a non-zero value enables the ramp — no separate flag. |
| `UpdateDirectionFromInput` | If true (default), `DirectionFacing` follows input direction |
| `UpdateDirectionFromVelocity` | If true and `UpdateDirectionFromInput` is false, `DirectionFacing` follows actual velocity |
| `IsUsingCustomDeceleration` | If true, uses `CustomDecelerationValue` when entity exceeds `MaxSpeed` (e.g. after a knockback) |
| `CustomDecelerationValue` | Deceleration magnitude (units/s²) used when `IsUsingCustomDeceleration` is true |

## JSON Config (recommended for tunable values)

Externalize movement values into a JSON file so designers/playtesters can tune feel without a rebuild. Mirrors the platformer `PlatformerConfig` pattern.

```csharp
using FlatRedBall2.Movement;

var config = TopDownConfig.FromJson("Content/player.topdown.json");
config.ApplyTo(_topDown);  // populates _topDown.MovementValues
```

Template: `.claude/templates/TopDownConfig/player.topdown.json`. Omitted fields fall back to engine defaults; partial files override only what they specify. Combine with `Screen.WatchContent` for hot-reload (see `content-hot-reload` skill).

## Acceleration Setup

```csharp
var values = new TopDownValues
{
    MaxSpeed = 200f,
    AccelerationTime = 0.2f,   // 200ms to reach full speed
    DecelerationTime = 0.1f,   // 100ms to stop
};
```

When either time is non-zero, the behavior blends between `AccelerationTime` and `DecelerationTime`
based on the angle between the current velocity and the desired direction. Perfectly reversing direction
uses `DecelerationTime`; pressing directly forward uses `AccelerationTime`. Both times zero = instant
(velocity set directly each frame).

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
_topDown.IsMoving          // true when velocity magnitude > epsilon (use this for animation, NOT input)
_topDown.SpeedMultiplier   // read/write, defaults to 1f
_topDown.InputEnabled      // set false to freeze input without destroying movement values
```

Use `IsMoving` (not `MovementInput.X != 0`) to pick idle vs. walk animations — input stays non-zero when the entity is held against a wall, but `IsMoving` correctly flips to false because collision zeroes velocity.

## Mapping 8-Way Facing to 4-Direction Art

Games often keep `PossibleDirections.EightWay` (the default) so diagonal input feels responsive but ship art with only 4 cardinal chains (`WalkUp`, `WalkDown`, `WalkLeft`, `WalkRight`). Collapse the diagonals at animation-selection time with `ToCardinal()` — do **not** switch to `FourWay`, which snaps the facing itself and makes diagonals feel notchy.

```csharp
string chain = (_topDown.IsMoving ? "Walk" : "Idle") + _topDown.DirectionFacing.ToCardinal();
_sprite.PlayAnimation(chain);
```

`ToCardinal()` defaults to `DiagonalAxis.Horizontal` (UpRight/DownRight → Right, UpLeft/DownLeft → Left) because horizontal silhouettes usually read more distinctly. Pass `DiagonalAxis.Vertical` when up/down poses are more distinct than left/right.

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

## Gotchas

- Diagonal input magnitudes > 1 are clamped to the unit circle — full speed in 8 directions.
- `UpdateDirectionFromInput` defaults to `true`. Set it to `false` and `UpdateDirectionFromVelocity` to `true` if you want direction to lag behind input (e.g. tank-style).
- `InputEnabled = false` stops reading input but does not zero velocity — the entity will coast until friction/collision stops it.
