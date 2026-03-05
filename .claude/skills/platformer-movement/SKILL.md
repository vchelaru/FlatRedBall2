---
name: platformer-movement
description: "Platformer Movement in FlatRedBall2. Use when implementing platformer mechanics including jumping, ground detection, PlatformerBehavior, PlatformerValues, double jump, air control, variable-height jumps, or side-scrolling movement. Trigger on any platformer-related question."
---

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
            JumpApplyLength = TimeSpan.FromSeconds(0.2),
            JumpApplyByButtonHold = true,
            UsesAcceleration = false,
        };

        _platformer.GroundMovement = groundValues;
        _platformer.AirMovement = groundValues;   // reuse, or provide separate air values

        var keyboard = Engine.InputManager.Keyboard;
        _platformer.JumpInput     = new KeyboardPressableInput(keyboard, Keys.Space);
        _platformer.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down);
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
| `AccelerationTimeX` | Time to reach `MaxSpeedX` from rest. `TimeSpan.Zero` = instant |
| `DecelerationTimeX` | Time to stop from `MaxSpeedX`. `TimeSpan.Zero` = instant |
| `Gravity` | Downward acceleration (positive value, Y− direction applied internally) |
| `MaxFallSpeed` | Maximum downward speed (prevents infinite fall acceleration) |
| `JumpVelocity` | Upward velocity applied when jump is triggered |
| `JumpApplyLength` | How long to sustain `JumpVelocity` after pressing jump |
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
_platformer.IsApplyingJump    // true while jump sustain is active (button held, duration not yet elapsed)
_platformer.DirectionFacing   // HorizontalDirection.Left or .Right
```

## Double Jump (Air Jumps)

`PlatformerBehavior` only jumps from the ground. Implement air jumps manually in the entity:

```csharp
private int _airJumpsRemaining;
private const int MaxAirJumps = 1;  // 1 = double jump

// In CustomActivity, after _platformer.Update(this, time):
if (_platformer.IsOnGround)
    _airJumpsRemaining = MaxAirJumps;

bool jumpJustPressed = keyboard.WasKeyPressed(Keys.Space);
if (jumpJustPressed && !_platformer.IsOnGround && !_platformer.IsApplyingJump && _airJumpsRemaining > 0)
{
    VelocityY = _platformer.AirMovement.JumpVelocity;
    _airJumpsRemaining--;
}
```

The `!_platformer.IsApplyingJump` guard prevents the air jump from triggering during the sustain phase of the first jump — the player must reach the peak before double-jumping.

## Collision Setup

Use `BounceOnCollision` with `elasticity: 0f` — **not** `MoveFirstOnCollision`:

```csharp
screen.AddCollisionRelationship(playerList, solidTiles)
      .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);
```

`BounceOnCollision` both separates the player (populating `LastReposition` for ground
detection) and zeroes the velocity component into the surface. Without it, hitting a
ceiling leaves the player with upward velocity and they float against it.

`MoveFirstOnCollision` only repositions — it never touches velocity, which is wrong for
platformer collision.

## Gotchas

- `JumpApplyLength = TimeSpan.Zero` means no jump sustain — velocity is set once on press and immediately stops being held. This gives a fixed-height jump regardless of `JumpApplyByButtonHold`.
- `MaxFallSpeed` must be > 0 or the entity will be clamped to 0 downward velocity. Set it to a large value (e.g. 1000) if you don't want a meaningful cap.
- Ground detection is `LastReposition.Y > 0` — a purely horizontal collision (wall) does not register as ground.
