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

Movement coefficients **must** live in a JSON file, not hardcoded in C#. Copy the template from `.claude/templates/PlatformerConfig/player.platformer.json` into the project's `Content/` folder, adjust values, and add `<Content Include="Content/*.json" CopyToOutputDirectory="PreserveNewest" />` to the `.csproj`.

```csharp
public class Player : Entity
{
    private readonly PlatformerBehavior _platformer = new();

    public override void CustomInitialize()
    {
        PlatformerConfig.FromJson("Content/player.platformer.json").ApplyTo(_platformer);

        var keyboard = Engine.Input.Keyboard;
        // Use .Or() to accept multiple key combos (e.g. Space or Up for jump, Arrows or WASD for move):
        _platformer.JumpInput     = new KeyboardPressableInput(keyboard, Keys.Space)
                                        .Or(new KeyboardPressableInput(keyboard, Keys.Up));
        _platformer.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down)
                                        .Or(new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
    }

    public override void CustomActivity(FrameTime time)
    {
        _platformer.Update(this, time);  // call AFTER collisions are resolved
    }
}
```

## PlatformerConfig JSON

`PlatformerConfig.FromJson(path)` deserializes a JSON file into a pure data model; the `ApplyTo` extension method pushes it onto a `PlatformerBehavior`. The config is a model — it does not reference the behavior; `ApplyTo` lives in `PlatformerConfigExtensions`.

Template: `.claude/templates/PlatformerConfig/player.platformer.json`

**Movement slots** are fixed names mapping to behavior fields: `ground` → `GroundMovement`, `air` → `AirMovement`, `afterDoubleJump` → reserved (parsed but not applied until the behavior wires a double-jump slot). All fields in a slot are nullable; omitted fields fall back to `new PlatformerValues()` defaults.

**Jump configuration** supports two mutually-exclusive modes per slot:
- **Derived (preferred):** `minJumpHeight` + optional `maxJumpHeight` → calls `SetJumpHeights`.
- **Raw (escape hatch):** `JumpVelocity` + `JumpApplyLength` + `JumpApplyByButtonHold` set directly.
- Specifying fields from both modes in the same slot throws `InvalidOperationException`.

**Gravity gotcha — derived mode uses airborne gravity.** The jump trajectory always runs under the `air` slot's `Gravity` (while grounded, collision cancels gravity — so ground.Gravity never acts on the arc). `ApplyTo` resolves this automatically: derived mode on the ground slot uses `air.Gravity` for its `JumpVelocity`/`JumpApplyLength` math; if no `air` slot is authored, it falls back to the slot's own `Gravity`. Mismatched ground/air gravities with derived-mode jumps will produce correct heights, but the ground slot's `Gravity` field itself is effectively ceremonial for trajectory purposes — keep it equal to `air.Gravity` for a clear mental model.

**TimeSpan fields** (`AccelerationTimeX`, `DecelerationTimeX`, `JumpApplyLength`) are represented as seconds (float) in JSON.

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
| `Gravity` | Downward acceleration (positive value, Y− direction applied internally). Only acts while airborne — collision cancels it while grounded. A ground slot's `Gravity` is used only as a fallback for `SetJumpHeights` when no air slot is present. |
| `MaxFallSpeed` | Maximum downward speed (prevents infinite fall acceleration) |
| `JumpVelocity` | Upward velocity applied when jump is triggered |
| `JumpApplyLength` | How long to sustain `JumpVelocity` after pressing jump |
| `JumpApplyByButtonHold` | If true, releasing jump early cuts the jump short |
| `SetJumpHeights(min, max?)` | Computes `JumpVelocity`, `JumpApplyLength`, and `JumpApplyByButtonHold` from desired min/max jump heights in world units. `Gravity` must be set first. Prefer this over setting jump fields manually. |

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

**Entity solids arranged in a grid (brick rows, crate stacks, destructible walls) must set `factory.IsSolidGrid = true`** — otherwise the player snags on seams between adjacent entities (each body resolves separation independently). See `entities-and-factories`. Use `Overlay.DrawRepositionDirections(factory)` in `CustomActivity` to visualize.

## Slopes and Ramps

Set `SlopeMode = SlopeCollisionMode.PlatformerFloor` on the **player's collision relationship** (not on the `TileShapeCollection`) to enable slope collision for polygon tiles. In this mode:

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

tileShapeCollection.AddPolygonTileAtCell(col, row, upRampSlope);

var playerVsTiles = AddCollisionRelationship(_playerFactory, tileShapeCollection);
playerVsTiles.SlopeMode = SlopeCollisionMode.PlatformerFloor;
playerVsTiles.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);
```

Default is `SlopeCollisionMode.Standard` (SAT collision for polygon tiles), which is correct for top-down games but causes snagging in platformers. `SlopeMode` is per-relationship so the same tile collection can be shared by a platformer player (`PlatformerFloor`) and other entities (`Standard`, e.g. a kicked ball) at the same time.

## Ground Snapping (Slope Adherence)

Players who run off a downslope or off the top of an up-ramp onto lower flat ground will briefly go airborne for a frame without snapping — a standard platformer feature eliminates this by "hugging" the entity to a nearby surface.

Wiring checklist — all three conditions must hold for snap to fire:
1. The player entity implements `IPlatformerEntity` (exposes `Platformer => _platformer`)
2. A `CollisionRelationship` between the player and a `TileShapeCollection` has `SlopeMode = SlopeCollisionMode.PlatformerFloor` — each such relationship automatically contributes its collection as a snap probe target
3. `PlatformerBehavior.CollisionShape` is set to the player's collision `AxisAlignedRectangle`, and `PlatformerValues.SlopeSnapDistance > 0` on the active values set (default `8f`), and the entity was on a sloped surface last frame (`CurrentSlope != 0`)

```csharp
public class Player : Entity, IPlatformerEntity
{
    private readonly PlatformerBehavior _platformer = new();
    public PlatformerBehavior Platformer => _platformer;
    // ...
    _platformer.CollisionShape = body;
}

// In screen:
var playerVsSolid = AddCollisionRelationship(_playerFactory, _solid);
playerVsSolid.SlopeMode = SlopeCollisionMode.PlatformerFloor; // enables snap + slope collision
```

**No explicit snap target.** A player can have multiple `PlatformerFloor` relationships
(solid level, moving platforms, one-way floors, etc.) and every one of them contributes as a
snap candidate — the first one to produce a hit within the frame wins, and subsequent
relationships no-op for the rest of the frame.

**Partial-config throws.** If the active values have `SlopeSnapDistance > 0` and a
`PlatformerFloor` relationship dispatches while `CollisionShape` is null,
`ConsiderSnappingTo` throws `InvalidOperationException`. Set `SlopeSnapDistance = 0` on
values that should opt out of snap instead of leaving `CollisionShape` null.

**Debugging snap with `OnSnapDiagnostic`.** If snap isn't firing, assign a callback to get a
one-line reason per frame (success or skip):

```csharp
_platformer.OnSnapDiagnostic = msg => System.Diagnostics.Debug.WriteLine(msg);
```

Messages begin with `"snap: "` on success or `"skip: <reason>"` when a gate aborted. When the
callback is null there is no allocation cost.

Feet Y is derived from the shape (`AbsoluteY - Height/2`) at probe time, so the shape can be placed anywhere relative to the entity origin without additional configuration.

Tuning lives on `PlatformerValues`:

| Field | Default | Meaning |
|---|---|---|
| `SlopeSnapDistance` | `8f` | Max downward probe distance. `0` disables snap for this values set. Snap is also gated on `CurrentSlope != 0` (was on a sloped surface last frame) — flat-to-flat cliff drops fall ballistically regardless of this value. |
| `SlopeSnapMaxAngleDegrees` | `60f` | Surfaces whose upward normal is within this many degrees of straight up qualify as "walkable" for snap. |

The mechanism:
1. If the player was grounded last frame, is not grounded this frame, is not rising, and was on a sloped surface last frame (`CurrentSlope != 0`),
2. Raycast straight down from the player's feet by `SlopeSnapDistance`,
3. If it hits a walkable surface (normal within the angle threshold), move the player onto it, zero `VelocityY`, and set `IsOnGround = true`.

**Flat-to-flat ledges fall ballistically.** The slope gate means walking off the edge of a flat tile onto a lower flat tile does *not* snap — behaves as a cliff drop, matching classic platformer feel. Snap is specifically for hugging downslopes across tile seams, not for stepping onto lower platforms.

**Per-values-set config is intentional.** A walking state wants snap on; a ball/wheel state that wants Sonic-style launches off ramps should set `SlopeSnapDistance = 0` on its `PlatformerValues` so it flies off ramps naturally.

**The "was grounded last frame" gate is what makes jumps work.** Without it, snap would yank the player back to the floor on the first frame of every jump. Don't attempt to bypass it.

## Slope Speed Adjustment

`PlatformerBehavior.CurrentSlope` (signed degrees, +X-rise positive, `0` when airborne/flat) is refreshed each frame by a short downward raycast contributed by every `PlatformerFloor` relationship. **Defaults are active, not opt-in** — any platformer with slope tiles immediately gets the classic "slow going up, faster going down" feel without additional configuration.

| Field | Default | Meaning |
|---|---|---|
| `UphillFullSpeedSlope` | `0` | Below this, full `MaxSpeedX` going uphill. |
| `UphillStopSpeedSlope` | `60` | At/above this, uphill speed = 0. Linearly interpolated between. Set equal to `UphillFullSpeedSlope` to disable slowdown. |
| `DownhillFullSpeedSlope` | `0` | Below this, downhill uses unmodified `MaxSpeedX`. |
| `DownhillMaxSpeedSlope` | `60` | At/above this, downhill speed is multiplied by `DownhillMaxSpeedMultiplier`. Linearly interpolated between. |
| `DownhillMaxSpeedMultiplier` | `1.5` | Peak multiplier. Set to `1` to disable downhill boost. |

Under defaults, a 30° slope cuts uphill speed to 50% and boosts downhill speed to 125%; a 45° slope is 25% / 137.5%. Uphill vs downhill is determined by `sign(inputX) == sign(CurrentSlope)`.

**Requires `SlopeMode = SlopeCollisionMode.PlatformerFloor`** on the player's collision relationship — without it, `CurrentSlope` stays 0 and the multipliers collapse to 1.

When using acceleration, the adjusted max speed drives `AccelerationTimeX` magnitude (speeding up); `DecelerationTimeX` still uses the raw `MaxSpeedX` so braking isn't slowed on an uphill.

## One-Way Platforms and Drop-Through

Jump-through (cloud) platforms are configured on the **collision relationship**, not the behavior — set `relationship.OneWayDirection = OneWayDirection.Up` and `relationship.AllowDropThrough = true`. The second flag is required for drop-through to bypass the relationship; leaving it `false` makes the barrier hard (e.g. Yoshi's Island ratchet doors — always blocks, Down+Jump has no effect on it). See the `collision-relationships` skill for the relationship-level semantics.

Drop-through is handled by the behavior. Set `PlatformerValues.CanFallThroughOneWayCollision = true` (default) to enable; `false` makes Down+Jump perform a normal jump and airborne Down has no effect.

Triggers:
- **Grounded Down+Jump** — suppresses one-way collision for one frame and skips the regular jump. After that frame, the entity's `LastPosition` is below the surface, so the one-way gate's positional check naturally prevents re-landing.
- **Airborne Down held** (`MovementInput.Y < -0.5`) — continuous suppression while falling, so the player can ride a downward arc through stacked clouds.

`PlatformerBehavior.IsSuppressingOneWayCollision` reflects the combined state; the one-way gate on each relationship reads it via the player's `IPlatformerEntity.Platformer`, **but only when the relationship has `AllowDropThrough = true`**. Relationships with `AllowDropThrough = false` (the default) ignore the suppression flag so hard one-way barriers remain impassable.

## Platformer Animations (User Code, Not Engine-Managed)

**Always use the template animations for platformer characters** — copy `PlatformerAnimations.achx` and `AnimatedSpritesheet.png` from `.claude/templates/AnimationChains/` into the project's `Content/Animations/` directory and load via `.achx`. Do not fall back to a shape placeholder for the player character.

The template chain names follow the pattern `Character<State><Direction>` — e.g. `CharacterIdleRight`, `CharacterWalkLeft`, `CharacterRunJumpRight`. There is no separate Fall chain; use `CharacterRunJump` for both jump and fall.

FRB2 does not provide an animation controller. Animation state selection is straightforward game code — a pattern match on `PlatformerBehavior` state, plus a facing suffix:

```csharp
private void UpdateAnimation()
{
    string chain = (_platformer.IsOnGround, Math.Abs(VelocityX) > 5f) switch
    {
        (true, false) => "CharacterIdle",
        (true, true)  => "CharacterWalk",
        _             => "CharacterRunJump",
    };

    chain += _platformer.DirectionFacing == HorizontalDirection.Left ? "Left" : "Right";
    _sprite.PlayAnimation(chain);
}
```

Call `UpdateAnimation()` at the end of `CustomActivity`, after `_platformer.Update`. `PlayAnimation` is idempotent — calling with the same chain name each frame does not restart the animation.

For additional states (run, land, wall-slide, double-jump), add cases to the pattern match. Priority is explicit in the code order — no registration API or priority constants needed.

For non-looping animations (attack, land), set `_sprite.IsLooping = false` and use the `AnimationFinished` event to transition back to the default state.

## Gotchas

- `JumpApplyLength = TimeSpan.Zero` means no jump sustain — velocity is set once on press and immediately stops being held. This gives a fixed-height jump regardless of `JumpApplyByButtonHold`.
- `MaxFallSpeed` must be > 0 or the entity will be clamped to 0 downward velocity. Set it to a large value (e.g. 1000) if you don't want a meaningful cap.
- Ground detection is `LastReposition.Y > 0` — a purely horizontal collision (wall) does not register as ground.
- Slopes require `SlopeMode = SlopeCollisionMode.PlatformerFloor` on the **player's collision relationship**. The default `Standard` mode will cause the player to snag at polygon/rect seams.
