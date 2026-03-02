# Platformer Movement — Design Discussion

## Context

FlatRedBall 1 supports platformer entities via code generation from the FlatRedBall Editor.
A user marks an entity as a platformer entity and the editor generates ~500 lines of code that
handles input, velocity/acceleration, movement state transitions, and collision response.

FlatRedBall 2 does not have code generation. This document discusses where platformer movement
should live, how it should be structured, and how its key dependencies (collision direction
detection, input) should be wired.

The same design questions apply to top-down movement and racing movement — so decisions here
establish conventions across the whole "movement behavior" category.

---

## FRB1 Reference

### `PlatformerValues` — movement parameter set

A pure data class (~25 fields) defining behavior for a single movement mode.
Named sets include: `Ground`, `Air`, `AfterDoubleJump`, `IceGround`, `WaterSwim`, etc.
Loaded from a CSV file in FRB1; could be plain code objects in FRB2.

Key fields:
```
MaxSpeedX, AccelerationTimeX, DecelerationTimeX
Gravity, MaxFallSpeed
JumpVelocity, JumpApplyLength, JumpApplyByButtonHold
UsesAcceleration, IsUsingCustomDeceleration, CustomDecelerationValue
CanFallThroughCloudPlatforms, CloudFallThroughDistance
MoveSameSpeedOnSlopes, UphillFullSpeedSlope, UphillStopSpeedSlope, ...
CanClimb, MaxClimbingSpeed
```

### Movement state machine

Three primary states: `Ground`, `Air`, `AfterDoubleJump`.
Transitions:
- Landed (collision pushed entity upward) → `Ground`
- Left ground without jumping → `Air`
- Double-jumped → `AfterDoubleJump`

Each state binds a `PlatformerValues` instance. Switching states re-applies gravity.

### Input

```csharp
void InitializePlatformerInput(IInputDevice inputDevice)
{
    JumpInput = inputDevice.DefaultPrimaryActionInput;
    HorizontalInput = inputDevice.DefaultHorizontalInput;
    VerticalInput = inputDevice.DefaultVerticalInput;
}
```

---

## Decisions Made

### D1: `LastReposition` lives on `Entity` — IMPLEMENTED

The platformer needs to know which direction a collision pushed the entity:
- `LastReposition.Y > 0` → pushed up → landed on ground
- `LastReposition.Y < 0` → pushed down → hit ceiling, cancel held jump
- `LastReposition.X != 0` → pushed sideways → hit wall

**Decision:** Entity-level, accumulated across same-frame collisions, reset per frame.

Rationale:
- Shapes in FRB2 are deliberately lean (no velocity, no acceleration). `LastReposition` is
  physics-adjacent state — it belongs on the physics object, not on geometry.
- Accumulating across same-frame collisions (floor + wall corner) preserves both signals
  simultaneously.
- If a future multi-shape scenario needs per-shape tracking, it should use child entities,
  not sub-shapes.
- A public field (not property) lets the user zero it out freely, and is consistent with
  `Position`, `Velocity`, and `Acceleration` also being public fields.

```csharp
public Vector2 LastReposition;   // reset each frame by PhysicsUpdate; accumulated by SeparateFrom
```

### D2: `Position`, `Velocity`, `Acceleration` are `Vector2` fields — IMPLEMENTED

The scalar accessors (`X`, `Y`, `VelocityX`, etc.) remain as properties that delegate to the
fields. Vector2 math is now the natural idiom:
```csharp
entity.Velocity += new Vector2(0, jumpVelocity);
entity.Position += offset;
```

### D3: Collision direction feedback uses `LastReposition` — no callback signature change

`CollisionOccurred(a, b)` stays a two-argument callback. Inside the handler, the platformer
behavior reads `a.LastReposition` to determine what happened. No need to change the event
signature or pass separation vectors explicitly.

---

## Remaining Decisions — All Resolved

### D4: Location — `src/Movement/` namespace in engine core

Engine core keeps it discoverable for any AI or developer working on an FRB2 game.
Establishes the folder as the home for top-down and racing movement when those come later.

### D5: Composition — `PlatformerBehavior` component

```csharp
public class Player : Entity
{
    private readonly PlatformerBehavior _platformer = new();

    public override void CustomInitialize()
    {
        _platformer.GroundMovement = new PlatformerValues { MaxSpeedX = 200, ... };
        _platformer.JumpInput = Engine.Input.Keyboard.SpaceKey;
        _platformer.HorizontalInput = Engine.Input.Keyboard.HorizontalInput;
    }

    public override void CustomActivity(FrameTime time)
    {
        _platformer.Update(this, time);
    }
}
```

### D6: Minimal `PlatformerValues` for v1

```
MaxSpeedX
AccelerationTimeX, DecelerationTimeX
Gravity, MaxFallSpeed
JumpVelocity, JumpApplyLength, JumpApplyByButtonHold
UsesAcceleration
```

Deferred: slope handling, cloud platforms, climbing, custom deceleration,
`AfterDoubleJump` as a named state (null `AfterDoubleJumpMovement` falls back to `AirMovement`).

### D7: Top-down movement — separate pass later

Platformer establishes the `XxxValues` / `XxxBehavior` pattern. Top-down follows the same
template in a future pass.
