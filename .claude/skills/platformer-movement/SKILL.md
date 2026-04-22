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

Movement coefficients are **recommended** in a JSON file for fast tuning and hot-reload. Copy the template from `.claude/templates/PlatformerConfig/player.platformer.json` into the project's `Content/` folder, adjust values, and add `<Content Include="Content/*.json" CopyToOutputDirectory="PreserveNewest" />` to the `.csproj`. For prototypes/tests, equivalent hardcoded values in C# are valid.

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

**Movement slots** are fixed names mapping to behavior fields: `ground` → `GroundMovement`, `air` → `AirMovement`, `climbing` → `ClimbingMovement` (see "Climbing Ladders" below), `afterDoubleJump` → reserved (parsed but not applied until the behavior wires a double-jump slot). All fields in a slot are nullable; omitted fields fall back to `new PlatformerValues()` defaults.

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
_platformer.IsOnGround               // true if entity was pushed upward by a collision this frame
_platformer.IsApplyingJump           // true while jump sustain is active (button held, duration not yet elapsed)
_platformer.DirectionFacing          // HorizontalDirection.Left or .Right
_platformer.GroundHorizontalVelocity // platform velocity transferred this frame, 0 when not on a moving platform
```

## Double Jump (Air Jumps)

`PlatformerBehavior` only jumps from the ground. Implement air jumps manually in the entity:

```csharp
private int _airJumpsRemaining;
private const int MaxAirJumps = 1;  // 1 = double jump

// In CustomActivity, after _platformer.Update(this, time):
if (_platformer.IsOnGround)
    _airJumpsRemaining = MaxAirJumps;

if (_platformer.JumpInput.WasJustPressed && !_platformer.IsOnGround && !_platformer.IsApplyingJump && _airJumpsRemaining > 0)
{
    VelocityY = _platformer.AirMovement.JumpVelocity;
    _airJumpsRemaining--;
}
```

Read the jump trigger off `_platformer.JumpInput` rather than the raw keyboard — that way the air jump automatically honors whatever binding (Space+Up, gamepad A, etc.) was configured in `CustomInitialize` via `.Or(...)`.

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

Use `BounceFirstOnCollision(elasticity: 0f)` — **not** `MoveFirstOnCollision`. The solid side can be a `TileShapeCollection` (for static level geometry) or an entity factory (for moving platforms, destructible blocks, etc.):

```csharp
// Against static tile geometry (preferred for level walls/floors)
screen.AddCollisionRelationship(playerFactory, tileShapeCollection)
      .BounceFirstOnCollision(elasticity: 0f);

// Against entity-based solids (moving platforms, breakable walls, etc.)
screen.AddCollisionRelationship<Player, MovingPlatform>(playerFactory, platformFactory)
      .BounceFirstOnCollision(elasticity: 0f);
```

`BounceFirstOnCollision` (which expands to the bounce with the player fully displaced
and the solid fixed) both separates the player (populating `LastReposition` for ground
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
playerVsTiles.BounceFirstOnCollision(elasticity: 0f);
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

## Moving Platforms

Standing on another `Entity` with non-zero `VelocityX` automatically transfers that horizontal
velocity to the platformer entity for the frame — the player rides the platform with no input,
and a jump carries the platform's momentum into the air. No opt-in needed: any bounce
relationship between an `IPlatformerEntity` and a regular `Entity` (not a `TileShapeCollection`)
gets this behavior whenever the separation pushes the platformer upward.

```csharp
AddCollisionRelationship<Player, MovingPlatform>(_playerFactory, _platformFactory)
    .BounceFirstOnCollision(elasticity: 0f);
```

The platform's own movement (path-follower, ping-pong in `CustomActivity`, etc.) is independent —
the engine just reads its `VelocityX` at collision time. Tile collections are excluded by design;
moving level geometry should be authored as entities.

**Animation gotcha:** standing still on a moving platform leaves the player with `VelocityX != 0` (they inherit the platform's velocity), so drive walk/idle animations off `MovementInput.X`, not `VelocityX` — see the animation section below.

## One-Way Platforms and Drop-Through

Jump-through (cloud) platforms are configured on the **collision relationship**, not the behavior — set `relationship.OneWayDirection = OneWayDirection.Up` and `relationship.AllowDropThrough = true`. The second flag is required for drop-through to bypass the relationship; leaving it `false` makes the barrier hard (e.g. Yoshi's Island ratchet doors — always blocks, Down+Jump has no effect on it). See the `collision-relationships` skill for the relationship-level semantics.

Drop-through is handled by the behavior. Set `PlatformerValues.CanFallThroughOneWayCollision = true` (default) to enable; `false` makes Down+Jump perform a normal jump and airborne Down has no effect.

Triggers:
- **Grounded Down+Jump** — suppresses one-way collision for one frame and skips the regular jump. After that frame, the entity's `LastPosition` is below the surface, so the one-way gate's positional check naturally prevents re-landing.
- **Airborne Down held** (`MovementInput.Y < -0.5`) — continuous suppression while falling, so the player can ride a downward arc through stacked clouds.

`PlatformerBehavior.IsSuppressingOneWayCollision` reflects the combined state; the one-way gate on each relationship reads it via the player's `IPlatformerEntity.Platformer`, **but only when the relationship has `AllowDropThrough = true`**. Relationships with `AllowDropThrough = false` (the default) ignore the suppression flag so hard one-way barriers remain impassable.

## Climbing Ladders (and SMW-Style Fences)

Climbing is a **fourth movement slot** alongside `GroundMovement`, `AirMovement`, and the reserved `AfterDoubleJump` — authored the same way in JSON, selected by a game-set flag at runtime. The same mechanic covers vertical ladders (single column) and Super Mario World-style fences (multi-column climbable surfaces with potentially uneven tops).

**The engine's contract.** When `PlatformerBehavior.IsClimbing == true` (and `ClimbingMovement` is assigned), `Update` switches the active slot to `ClimbingMovement`, zeros `AccelerationY`, drives `VelocityY = MovementInput.Y * ClimbingSpeed`, and skips fall-speed clamping and drop-through. On a jump press it clears `IsClimbing` and applies the climbing slot's `JumpVelocity`/`JumpApplyLength`/`JumpApplyByButtonHold` (same shape as a ground jump). Horizontal movement runs under the same accel/decel path as the other slots using the climbing slot's `MaxSpeedX`.

**What the engine does NOT do.** Detect ladders. Decide when to enter. Snap the player's X to a ladder's center. Handle walking off the top or falling off the side. Those are game-code concerns — the flag is the full contract.

### JSON

```json
"climbing": {
  "MaxSpeedX": 80,             // horizontal speed while on a ladder (usually slower than ground)
  "ClimbingSpeed": 120,        // vertical speed at full |MovementInput.Y|
  "JumpVelocity": 250          // upward velocity applied when player presses jump to leave ladder
                               //   (0 = "drop off" with no upward velocity; same shape as ground jump,
                               //    so JumpApplyLength/JumpApplyByButtonHold also work here)
}
```

**Consumed while climbing:** `MaxSpeedX`, `AccelerationTimeX`, `DecelerationTimeX`, `ClimbingSpeed`, `JumpVelocity`, `JumpApplyLength`, `JumpApplyByButtonHold`.

**Ignored while climbing** (parse without error, no effect): `Gravity`, `MaxFallSpeed`, all slope fields, `CanFallThroughOneWayCollision`.

### Detection: a separate `TileShapeCollection`

Author ladders (and SMW-style fences — same mechanic, different art) as their own `TileShapeCollection`, parallel to the solid-geometry collection. Use a dedicated TMX tile layer (e.g. `"Ladders"`). Do **not** add a `CollisionRelationship` between the player and the ladder collection — the game polls overlap directly each frame, no separation needed.

```csharp
public class Player : Entity
{
    public TileShapeCollection? Ladders { get; set; }   // assigned by the screen after TMX load
    private readonly PlatformerBehavior _platformer = new();
}
```

### Enter / exit pattern

```csharp
public override void CustomActivity(FrameTime time)
{
    bool overlappingLadder = Ladders != null && this.CollidesWith(Ladders);
    float inputY = _platformer.MovementInput?.Y ?? 0f;

    if (!_platformer.IsClimbing)
    {
        bool enterFromMiddle = overlappingLadder && inputY > 0.5f;
        bool enterFromTop = _platformer.IsOnGround && inputY < -0.5f && IsLadderBelowFeet();
        if (enterFromMiddle || enterFromTop)
        {
            _platformer.IsClimbing = true;
            VelocityY = 0f;
            // Optional X-snap to the ladder cell's center — classic feel (Mega Man). Skip for
            // free-floating fence climbing (SMW), where the player keeps their X.
            var (col, _) = Ladders!.GetCellAt(new System.Numerics.Vector2(X, Y));
            X = Ladders.GetCellWorldPosition(col, 0).X;
        }
    }
    else
    {
        // Recompute TopOfLadderY every frame — it tracks the player's current column. For a
        // single-column ladder the value is constant; for an SMW fence with an uneven top edge
        // the cap updates as the player slides left/right.
        _platformer.TopOfLadderY = ComputeTopOfLadderY();

        // Exit on landing (engine collision handled separation) or losing overlap (climbed off
        // the side, or fell off the bottom). Jump-off is engine-handled.
        if (_platformer.IsOnGround || !overlappingLadder)
            _platformer.IsClimbing = false;
    }

    _platformer.Update(this, time);
}

private bool IsLadderBelowFeet()
{
    if (Ladders == null) return false;
    // Probe one unit below the entity's feet (entity Y = feet by platformer convention).
    var (col, row) = Ladders.GetCellAt(new System.Numerics.Vector2(X, Y - 1f));
    return Ladders.GetTileAtCell(col, row) != null;
}

private float? ComputeTopOfLadderY()
{
    if (Ladders == null) return null;
    var (col, row) = Ladders.GetCellAt(new System.Numerics.Vector2(X, Y));
    if (Ladders.GetTileAtCell(col, row) == null) return null;
    int topRow = row;
    while (Ladders.GetTileAtCell(col, topRow + 1) != null) topRow++;
    // Cell world position is the cell center; +half grid = top edge.
    return Ladders.GetCellWorldPosition(col, topRow).Y + Ladders.GridSize / 2f;
}
```

### `TopOfLadderY` semantics

`TopOfLadderY` clamps `Y` and zeros upward velocity — the player can hold Up forever and won't pass the top. To support **walk-off-the-top** ladders, set `TopOfLadderY = null` and the game decides when to flip `IsClimbing = false` (e.g. when the player clears the topmost ladder cell + presses Up). The `ComputeTopOfLadderY` helper above gives the standard "stop at the top of the column" behavior, which is what most games want and is what makes SMW-style uneven fence tops work for free.

### Gotchas

- **Bottom-of-ladder needs no special probe.** Climbing down past the bottom of the ladder ends overlap; the exit branch (`!overlappingLadder`) flips `IsClimbing` to false and gravity takes over. Climbing down onto solid ground engages the standard player↔solid collision, which sets `IsOnGround = true` and the same exit branch fires. Don't add separate "am I at the bottom" logic.
- **`IPlatformerEntity` is not required for climbing.** Ground snap and the slope probe require it because collision relationships dispatch back into the behavior. The climbing path uses no relationship — the game polls `CollidesWith(Ladders)` directly — so a player class that only does climbing (no ground snap) doesn't need to implement `IPlatformerEntity`. If the player also wants ground snap, implement it for that reason, not for ladders.
- **Always author `JumpVelocity` on the climbing slot.** It defaults to 0, and "field omitted" is indistinguishable from "explicitly 0" once parsed. A climbing slot without `JumpVelocity` makes jump-off look broken — the player presses jump, leaves the ladder, and drops straight down with no upward velocity. The engine does not fall back to `AirMovement.JumpVelocity`.
- **`ClimbingMovement` null while `IsClimbing == true` throws** — same loud-failure pattern as `CollisionShape` with `SlopeSnapDistance > 0`. Assign the slot before entering the state.
- **Zero `VelocityY` when entering.** The engine only drives `VelocityY` from input while climbing; any residual fall speed from the frame before entering would be overwritten to `inputY * ClimbingSpeed` the same frame, but an `inputY = 0` entry would leave the player floating with whatever leftover upward jump velocity was mid-arc. Just clear it on entry for clarity.
- **Collision relationships still run.** If ladder-adjacent walls or one-way platforms should be passable while climbing (e.g., climbing up through a jump-through platform a ladder passes through), the game must either skip those relationships or rely on `AllowDropThrough` behavior while `IsClimbing` — the behavior does not auto-suppress them.

## Platformer Animations (User Code, Not Engine-Managed)

**Always use the template animations for platformer characters** — copy `PlatformerAnimations.achx` and `AnimatedSpritesheet.png` from `.claude/templates/AnimationChains/` into the project's `Content/Animations/` directory and load via `.achx`. Do not fall back to a shape placeholder for the player character.

The template chain names follow the pattern `Character<State><Direction>` — e.g. `CharacterIdleRight`, `CharacterWalkLeft`, `CharacterRunJumpRight`. There is no separate Fall chain; use `CharacterRunJump` for both jump and fall.

FRB2 does not provide an animation controller. Animation state selection is straightforward game code — a pattern match on `PlatformerBehavior` state, plus a facing suffix:

```csharp
private void UpdateAnimation()
{
    // Climb takes priority over ground/air. The template's climb chain has no Left/Right split
    // (player faces the ladder) — use CharacterClimbFront (or CharacterClimbRear) directly.
    if (_platformer.IsClimbing)
    {
        _sprite.PlayAnimation("CharacterClimbFront");
        return;
    }

    float inputX = _platformer.MovementInput?.X ?? 0f;
    string chain = (_platformer.IsOnGround, MathF.Abs(inputX) > 0.1f) switch
    {
        (true, false) => "CharacterIdle",
        (true, true)  => "CharacterWalk",
        _             => "CharacterRunJump",
    };

    chain += _platformer.DirectionFacing == HorizontalDirection.Left ? "Left" : "Right";
    _sprite.PlayAnimation(chain);
}
```

**Do not pause the climb animation when the player is still on the ladder.** Animation runs continuously — a "hanging on the ladder, not moving" state is a content choice (a 1-frame chain, or a multi-frame chain with subtle motion like the player's hair drifting). See the `animation` skill's "Never pause animation to express still state" gotcha.

**Why input, not velocity?** Velocity-based detection (`Math.Abs(VelocityX) > threshold`) breaks on moving platforms — the player stands still, inherits the platform's velocity, and the walk animation loops as if they're running. It also breaks on slippery ground — after releasing input the player is still sliding, so the walk animation keeps playing through the decel. Input-based selection is simpler and matches FRB1's state-driven approach. For games that genuinely want a slide animation distinct from walk, branch on `GroundHorizontalVelocity` (platform contribution this frame) or `VelocityX - GroundHorizontalVelocity` (speed relative to the platform).

Call `UpdateAnimation()` at the end of `CustomActivity`, after `_platformer.Update`. `PlayAnimation` is idempotent — calling with the same chain name each frame does not restart the animation.

For additional states (run, land, wall-slide, double-jump), add cases to the pattern match. Priority is explicit in the code order — no registration API or priority constants needed.

For non-looping animations (attack, land), set `_sprite.IsLooping = false` and use the `AnimationFinished` event to transition back to the default state.

## Gotchas

- `JumpApplyLength = TimeSpan.Zero` means no jump sustain — velocity is set once on press and immediately stops being held. This gives a fixed-height jump regardless of `JumpApplyByButtonHold`.
- `MaxFallSpeed` must be > 0 or the entity will be clamped to 0 downward velocity. Set it to a large value (e.g. 1000) if you don't want a meaningful cap.
- Ground detection is `LastReposition.Y > 0` — a purely horizontal collision (wall) does not register as ground.
- Slopes require `SlopeMode = SlopeCollisionMode.PlatformerFloor` on the **player's collision relationship**. The default `Standard` mode will cause the player to snag at polygon/rect seams.
