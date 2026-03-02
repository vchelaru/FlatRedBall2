# FlatRedBall2 — Deferred Items

This document tracks features and systems intentionally deferred from the initial implementation. Each item includes context and the recommended approach when it is implemented.

## Audio System

**Status**: Stubbed — `AudioManager` methods throw `NotImplementedException`
**What's needed**:
- `IAudioBackend` implementation using MonoGame's `SoundEffect` and `Song` APIs
- `ContentManagerService` integration for loading `.wav` / `.mp3` / `.ogg` assets
- Volume control, looping, and fade in/out support
- Optional: `NAudio` or `BASS` backend for platforms where MonoGame audio is limited

## ACHX Animation Format

**Status**: Stubbed — `Sprite.PlayAnimation` is a no-op
**What's needed**:
- Parser for `.achx` XML format (from FRB1)
- `AnimationChain` and `AnimationFrame` runtime types
- Per-frame texture region, timing, and relative X/Y offset application
- `Sprite.CurrentAnimation` state machine: playing, looped, completed events

## Async Synchronization Context

**Status**: `// TODO: flush async sync context` comment in update loop
**What's needed**:
- A custom `SynchronizationContext` that queues continuations
- Flushing all pending continuations during the update loop (after collision, before CustomActivity)
- Allows `await` in entity/screen code to resume on the game thread

## DebugRenderer Drawing

**Status**: All draw methods are no-ops
**What's needed**:
- Primitive line/shape rendering (Apos.Shapes NuGet, or custom triangle-strip renderer)
- `SpriteFont` integration for `DrawText`
- Alternative: use MonoGame's built-in primitive batch via vertex buffers

## Tiled Integration

**Status**: `TiledMapLayerRenderable` and `TiledCollisionGenerator` are stubs
**What's needed**:
- Add `MonoGame.Extended.Tiled` NuGet package reference
- `TiledMapLayerRenderable.Draw` renders a tile layer using MonoGame.Extended's tile renderer
- `TiledCollisionGenerator.Generate` reads tile properties and emits `AxisAlignedRectangle` shapes into a `ShapeCollection`

## Same-List Collision

**Status**: Not implemented — `CollisionRelationship` only handles two different lists
**What's needed**:
- `AddCollisionRelationship<T>(IEnumerable<T> list)` overload for self-vs-self collision
- Pair iteration: `for i in 0..n, for j in i+1..n` to avoid duplicate pairs and self-collision

## Camera Pixel-Perfect Mode

**Status**: Not implemented
**What's needed**:
- A `PixelPerfect` flag on `Camera` that snaps `X`/`Y` to nearest integer before computing the transform matrix
- Eliminates sub-pixel shimmer for pixel-art games

## Rotation Velocity on Entity

**Status**: Omitted from initial implementation
**What's needed**:
- `RotationVelocity` and `RotationAcceleration` properties on `Entity`
- Updated in the physics pass alongside X/Y kinematics

## FrameTime — convenience accessor for elapsed seconds

**Status**: `SinceGameStart` is a `TimeSpan`; accessing total elapsed seconds requires `time.SinceGameStart.TotalSeconds`
**What's needed**:
- A `TotalSecondsElapsed` (or similar) float shorthand on `FrameTime`, analogous to `DeltaSeconds`
- Reduces verbosity in any code that needs an absolute time value (e.g. `PlatformerBehavior` jump timer)

## Top-Down Movement

**Status**: Not yet implemented — deferred after platformer movement
**What's needed**:
- `TopDownValues` data class (max speed, acceleration time, deceleration time, possibly 8-directional vs free angle)
- `TopDownBehavior` component following the same `Update(entity, time)` pattern as `PlatformerBehavior`
- Skill file at `.claude/skills/top-down-movement/SKILL.md`

## GamepadPressableInput WasJustPressed / WasJustReleased

**Status**: Returns `false` — previous state tracking not implemented
**What's needed**:
- Expose `WasButtonPressed(Buttons)` and `WasButtonReleased(Buttons)` on `IGamepad`
- Update `Gamepad` implementation to track previous frame state (already done internally, needs interface exposure)
- Update `GamepadPressableInput` to use these methods
