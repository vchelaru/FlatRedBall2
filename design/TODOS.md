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

## TileShapeCollection

**Status**: Not yet implemented
**What it is**: A grid-based `ICollidable` for static tile collision. Shapes are positioned on a uniform tile grid, enabling spatial partitioning (only test tiles near the entity rather than all tiles). Usable independently of Tiled.
**What's needed**:
- `TileShapeCollection` class implementing `ICollidable` with an internal grid structure
- API to add tiles by grid position (e.g., `AddTile(int col, int row)`) with configurable tile size
- Spatial partitioning in `CollidesWith`/`GetSeparationVector` — only check tiles within range of the query shape's bounds
- `RepositionDirections` support per-tile (for one-way platforms)
- `Screen.AddCollisionRelationship` already accepts any `ICollidable` as static geometry (generalized from `ShapeCollection`)
- Skill file at `.claude/skills/shapes/` (or new file) covering `TileShapeCollection` usage

## Tiled Integration

**Status**: `TiledMapLayerRenderable` and `TiledCollisionGenerator` are stubs; depends on `TileShapeCollection`
**What's needed**:
- Add `MonoGame.Extended.Tiled` NuGet package reference
- `TiledMapLayerRenderable.Draw` renders a tile layer using MonoGame.Extended's tile renderer
- `TiledCollisionGenerator.Generate` reads tile properties and emits tiles into a `TileShapeCollection`

## ~~Same-List Collision~~ (COMPLETED)

**Status**: Implemented — `Screen.AddCollisionRelationship<A>(IEnumerable<A> list)` overload exists in `Screen.cs`.

## Camera Pixel-Perfect Mode

**Status**: Not implemented
**What's needed**:
- A `PixelPerfect` flag on `Camera` that snaps `X`/`Y` to nearest integer before computing the transform matrix
- Eliminates sub-pixel shimmer for pixel-art games

## ~~Rotation Velocity on Entity~~ (COMPLETED)

**Status**: Implemented — `RotationVelocity` (`Angle`) on `Entity`; applied in `PhysicsUpdate` alongside X/Y kinematics. Children skip rotation physics the same as positional physics.

## Top-Down Movement

**Status**: Not yet implemented — deferred after platformer movement
**What's needed**:
- `TopDownValues` data class (max speed, acceleration time, deceleration time, possibly 8-directional vs free angle)
- `TopDownBehavior` component following the same `Update(entity, time)` pattern as `PlatformerBehavior`
- Skill file at `.claude/skills/top-down-movement/SKILL.md`

## ~~GamepadPressableInput WasJustPressed / WasJustReleased~~ (COMPLETED)

**Status**: Implemented — `GamepadPressableInput` delegates to `IGamepad.WasButtonJustPressed`/`WasButtonJustReleased`.
