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

        var keyboard = Engine.InputManager.Keyboard;
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

## Gotchas

- Diagonal input magnitudes > 1 are clamped to the unit circle — full speed in 8 directions.
- `UpdateDirectionFromInput` defaults to `true`. Set it to `false` and `UpdateDirectionFromVelocity` to `true` if you want direction to lag behind input (e.g. tank-style).
- `InputEnabled = false` stops reading input but does not zero velocity — the entity will coast until friction/collision stops it.
