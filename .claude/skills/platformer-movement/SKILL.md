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
            UsesAcceleration = false,
        };
        groundValues.SetJumpHeights(minHeight: 16f, maxHeight: 48f); // must be after Gravity

        _platformer.GroundMovement = groundValues;
        _platformer.AirMovement = groundValues;   // reuse, or provide separate air values

        var keyboard = Engine.Input.Keyboard;
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
| `SetJumpHeights(min, max?)` | Computes `JumpVelocity`, `JumpApplyLength`, and `JumpApplyByButtonHold` from desired min/max jump heights in world units. `Gravity` must be set first. Prefer this over setting jump fields manually. |
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

## Entity Origin and Shape Offset

In platformers, an entity's Y position represents **the feet** (the bottom of the character). This means collision shapes and sprites must be offset upward so their bottom edge aligns with Y=0 on the entity:

```csharp
// For a 12×28 collision box, offset Y by half the height so the bottom sits at the entity's feet
var collisionBox = new AxisAlignedRectangle
{
    Width = 12,
    Height = 28,
    Y = 14,  // Height / 2 — centers the box above the entity origin
};
Add(collisionBox);
```

The same applies to sprites — the `.achx` template uses `<RelativeY>16</RelativeY>` on a 32-pixel-tall character for exactly this reason (16 = half of 32).

**If you skip this offset**, the character's feet will not align with the ground.

## Collision Setup

Use `BounceOnCollision` with `elasticity: 0f` — **not** `MoveFirstOnCollision`. The solid side can be a `TileShapeCollection` (for static level geometry) or an entity factory (for moving platforms, destructible blocks, etc.):

```csharp
// Against static tile geometry (preferred for level walls/floors)
screen.AddCollisionRelationship(playerFactory, tileShapeCollection)
      .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);

// Against entity-based solids (moving platforms, breakable walls, etc.)
screen.AddCollisionRelationship<Player, MovingPlatform>(playerFactory, platformFactory)
      .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);
```

`BounceOnCollision` both separates the player (populating `LastReposition` for ground
detection) and zeroes the velocity component into the surface. Without it, hitting a
ceiling leaves the player with upward velocity and they float against it.

`MoveFirstOnCollision` only repositions — it never touches velocity, which is wrong for
platformer collision.

## Slopes and Ramps

Set `tileShapeCollection.SlopeMode = SlopeCollisionMode.PlatformerFloor` to enable slope collision for polygon tiles. In this mode:

- **Polygon tiles push vertically only** (heightmap-based). The polygon's surface Y at the player's center X determines the push. No horizontal component means no snagging at slope seams.
- **Rect tiles next to polygon tiles** get their shared face suppressed automatically (like adjacent rects already do).
- **Preferential landing**: if the player is falling and standard collision would push them sideways off a ledge edge, they land on top instead. Only fires when the tile's Up face is active (nothing above it).

Create slope tiles with `AddPolygonTileAtCell`:

```csharp
// Right-triangle slope going up-right (bottom edge flat, hypotenuse top-right to bottom-left)
var upRampSlope = Polygon.FromPoints(new[]
{
    new Vector2(-8f, -8f), // bottom-left
    new Vector2( 8f, -8f), // bottom-right
    new Vector2( 8f,  8f), // top-right
});

tileShapeCollection.SlopeMode = SlopeCollisionMode.PlatformerFloor;
tileShapeCollection.AddPolygonTileAtCell(col, row, upRampSlope);
```

Default is `SlopeCollisionMode.Standard` (SAT collision for polygon tiles), which is correct for top-down games but causes snagging in platformers.

## Gotchas

- `JumpApplyLength = TimeSpan.Zero` means no jump sustain — velocity is set once on press and immediately stops being held. This gives a fixed-height jump regardless of `JumpApplyByButtonHold`.
- `MaxFallSpeed` must be > 0 or the entity will be clamped to 0 downward velocity. Set it to a large value (e.g. 1000) if you don't want a meaningful cap.
- Ground detection is `LastReposition.Y > 0` — a purely horizontal collision (wall) does not register as ground.
- Slopes require `SlopeMode = SlopeCollisionMode.PlatformerFloor`. The default `Standard` mode will cause the player to snag at polygon/rect seams.
