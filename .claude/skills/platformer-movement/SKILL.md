# Platformer Movement

## Overview

Platformer movement is provided by two classes in `FlatRedBall2.Movement`:

- **`PlatformerValues`** — a plain data class holding movement parameters for one movement mode (Ground, Air, etc.)
- **`PlatformerBehavior`** — a component added to an entity that reads input, drives the state machine, and applies velocity/acceleration each frame

## Minimal Setup

```csharp
public class Player : Entity
{
    private readonly PlatformerBehavior _platformer = new();

    public override void CustomInitialize()
    {
        var groundValues = new PlatformerValues
        {
            MaxSpeedX = 200f,
            Gravity = 600f,
            MaxFallSpeed = 800f,
            JumpVelocity = 400f,
            JumpApplyLength = 0.2f,
            JumpApplyByButtonHold = true,
            UsesAcceleration = false,
        };

        _platformer.GroundMovement = groundValues;
        _platformer.AirMovement = groundValues;   // reuse, or provide separate air values
        _platformer.JumpInput = Engine.Input.Keyboard.SpaceKey;
        _platformer.MovementInput = Engine.Input.Keyboard.HorizontalInput2D;
    }

    public override void CustomActivity(FrameTime time)
    {
        _platformer.Update(this, time);  // call AFTER collisions are resolved
    }
}
```

## Critical Call-Order Rule

`PlatformerBehavior.Update` reads `entity.LastReposition` to determine ground state.
`LastReposition` is accumulated by `SeparateFrom` during collision resolution and reset at
the start of each `PhysicsUpdate`.

**The Screen update order is: PhysicsUpdate → CollisionRelationships → CustomActivity.**
This means `Update` called from `CustomActivity` will always see the current frame's
collision results — no special wiring needed.

## PlatformerValues Fields

| Field | Description |
|---|---|
| `MaxSpeedX` | Maximum horizontal speed in world units/sec |
| `AccelerationTimeX` | Seconds to reach `MaxSpeedX` from rest. 0 = instant |
| `DecelerationTimeX` | Seconds to stop from `MaxSpeedX`. 0 = instant |
| `Gravity` | Downward acceleration (positive value, Y− direction applied internally) |
| `MaxFallSpeed` | Maximum downward speed (prevents infinite fall acceleration) |
| `JumpVelocity` | Upward velocity applied when jump is triggered |
| `JumpApplyLength` | Seconds to sustain `JumpVelocity` after pressing jump |
| `JumpApplyByButtonHold` | If true, releasing jump early cuts the jump short |
| `UsesAcceleration` | If false, `AccelerationTimeX`/`DecelerationTimeX` are ignored and velocity is set directly |

## Ground vs Air values

`GroundMovement` is used when `IsOnGround == true`; `AirMovement` is used otherwise.
If `GroundMovement` is null, `AirMovement` is used for both states.

Common patterns:
- Same values for ground and air: assign the same instance to both
- Reduced air control: set a lower `MaxSpeedX` or `AccelerationTimeX` in `AirMovement`
- Ice: high `AccelerationTimeX` and `DecelerationTimeX` in `GroundMovement`

## Reading State

```csharp
_platformer.IsOnGround        // true if entity was pushed upward by a collision this frame
_platformer.DirectionFacing   // HorizontalDirection.Left or .Right
```

## Collision Setup

The behavior has no special collision requirements. Set up a standard move relationship
between the player list and solid tiles:

```csharp
screen.AddCollisionRelationship(playerList, solidTiles)
      .MoveFirstOnCollision();
```

`LastReposition` is populated automatically by `SeparateFrom` inside the relationship.

## Gotchas

- `JumpApplyLength = 0` means no jump sustain — velocity is set once on press and immediately stops being held. This gives a fixed-height jump regardless of `JumpApplyByButtonHold`.
- `MaxFallSpeed` must be > 0 or the entity will be clamped to 0 downward velocity. Set it to a large value (e.g. 1000) if you don't want a meaningful cap.
- Ground detection is `LastReposition.Y > 0` — a purely horizontal collision (wall) does not register as ground.
