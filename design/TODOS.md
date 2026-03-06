# FlatRedBall2 — Deferred Items

This document tracks features and systems intentionally deferred from the initial implementation. Each item includes context and the recommended approach when it is implemented.

## Polygon — Concave Collision (Convex Decomposition)

**Status**: Not implemented (deferred — low priority, implement much later) — `Polygon` collision uses SAT, which only works for convex polygons.
**What's needed**:
- SAT is fundamentally limited to convex shapes. Concave polygons produce incorrect collision responses: shapes can pass through concave regions or collide with empty concave areas.
- Rendering already supports concave polygons via ear-clip triangulation; collision does not.
- Recommended approach: at `FromPoints` / `AddPoint` time, decompose the polygon into convex sub-polygons (e.g., Hertel-Mehlhorn convex partitioning on top of the existing ear-clip triangulation). Store the sub-polygons internally and union their collision results.
- `CollisionDispatcher` would need a `PolygonVsX` path that iterates sub-polygons.
- Consider whether the sub-polygon decomposition should be exposed publicly (e.g., `Polygon.ConvexParts`) for debugging.

## TileShapeCollection — Polygon Tile Support

**Status**: Implemented — `AddPolygonTileAtCell(int col, int row, Polygon prototype)` / `RemovePolygonTileAtCell` / `GetPolygonTileAtCell`. Collision (`GetSeparationFor`) and `Raycast` both dispatch to polygon tiles. Screen.Add wires up render callbacks for both tile types.

## Spatial Partitioning — Design Discussion Needed

**Status**: Design not yet decided
**What's needed**:
- `TileShapeCollection` already uses a dictionary-based grid for O(1) tile lookup, which is effectively a uniform-grid spatial partition. This is fine for tile collision.
- Open question: do we need a general-purpose spatial partition for entity-vs-entity collision at scale (many enemies, many projectiles)?
- Current `AddCollisionRelationship` does O(n×m) broad-phase — acceptable for small lists, becomes a bottleneck in shmups or crowd games.
- Candidates: uniform grid, quadtree, or simply relying on Apos.Shapes' existing partitioning. Decision should be made before implementing any dense entity-vs-entity collision sample.
- When decided, the partition should be transparent to game code (no API change to `AddCollisionRelationship`) — the screen manages it internally.

## Per-Shape Collision Exclusion

**Status**: Done — `Add<T>(T child, bool isDefaultCollision)` where `T : IAttachable, ICollidable` attaches a shape without adding it to the default collision set. `SetDefaultCollision(ICollidable shape, bool)` toggles participation at runtime (idempotent; throws if shape is not a child of the entity). Demo in `DefaultCollisionDemoScreen`. Also used in `ShipEntity` and `OtherEntity` in the Y-sort demo.

## Per-Shape Collision Targeting in Collision Relationships

**Status**: Done — `WithFirstShape(Func<A, ICollidable>)` and `WithSecondShape(Func<B, ICollidable>)` fluent methods on `CollisionRelationship<A, B>`. Selectors restrict which child shape is used for detection and response while keeping the physics response (separation, bounce) applied to the parent entity. Example:
```csharp
screen.AddCollisionRelationship(players, tiles)
    .WithFirstShape(p => p.FeetCollision)
    .MoveFirstOnCollision();
```
Demo in `ShapeSelectDemoScreen` / `ShapeSelectPlayer`: player has a large BodyCircle (visual only, radius 40) and a small CollisionRect (20×20). A narrow 64px passage in the level lets the rect through but not the circle — the cyan circle visibly overlaps the walls while the player passes through.

## Y-Sort Rendering (Top-Down Draw Order)

**Status**: Done — `Screen.SortMode = SortMode.ZSecondaryParentY`. Items with different Z sort by Z; items with equal Z sort by parent entity's world-space Y descending (higher Y = behind, lower Y = in front). Uses insertion sort (stable, O(N) when nearly sorted). Two unit tests in `RenderListTests`.

## Moving Entities Between Layers

**Status**: Done — `entity.MoveToLayer(layer)` updates the `Layer` property on all `IRenderable` children (shapes, sprites) and Gum visual children, recursing into child entities. Collision participation is unchanged. `IRenderable.Layer` now has a public setter to support this.

## Path Object (Movement and Rendering)

**Status**: Done — `Path` (in `FlatRedBall2.Math`) and `PathFollower` (in `FlatRedBall2.Movement`).
- Builder API: `MoveTo`, `MoveBy`, `LineTo`, `LineBy`, `ArcTo`, `ArcBy` — all fluent (return `this`).
- Arc angles are signed radians (+ = CCW, − = CW, Y+ up). For a full circle, chain two `ArcTo(π)` calls — a single arc from a point to itself has zero chord length and degenerates.
- Queries: `PointAtLength`, `PointAtRatio`, `TangentAtLength`, `TangentAtRatio`, `TotalLength`.
- `IsLooped = true` closes the path back to start and includes the closing segment in `TotalLength`.
- `Path` implements `IRenderable` — `screen.Add(path)` renders it as a polyline. `Color`, `LineThickness`, `Visible` control appearance.
- `PathFollower`: `Speed`, `Loops`, `FaceDirection`, `WaypointReached` event, `PathCompleted` event, `Reset()`. Call `Activity(entity, deltaSeconds)` each frame.
- Demo in `PathDemoScreen`.

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

## DebugRenderer Drawing / Visuals

**Status**: Done — `Overlay` static class in `FlatRedBall2.Diagnostics` provides immediate-mode draw calls: `Circle`, `Rectangle`, `Line`, `Arrow`, `Polygon`, `Sprite`, `Text`, `TextBackground`. Objects are pooled and reused; they appear for one frame and are hidden automatically the next. Pool resets on screen transition. `FlatRedBallService.Update` calls `Overlay.BeginFrame()` each frame so objects disappear even when no draw calls are made.

## Tiled Integration — TMX → TileShapeCollection

**Status**: Deferred — waiting on MonoGame.Extended being ready
**What's needed**:
- Add `MonoGame.Extended.Tiled` NuGet package reference once it supports the target MonoGame version
- `TiledMapLayerRenderable.Draw` renders a tile layer using MonoGame.Extended's tile renderer
- `TiledCollisionGenerator.Generate` reads tile properties from a parsed TMX map and calls `TileShapeCollection.AddTileAtCell` for each solid tile

## Resolution Control and Camera Display Settings

**Status**: Not implemented
**What's needed**:
- No built-in system for controlling how the game view responds to window size, aspect ratio changes, or display scaling. Game code currently has no way to declare a target resolution or constrain how the camera adapts.
- Required concepts (see FRB1 for prior art on naming and behavior):
  - **Desired aspect ratio / letterboxing**: define a target aspect ratio; when the window does not match, add letterbox or pillarbox bars rather than stretching the view.
  - **Zoom**: a scalar applied to the camera's view independent of window size — used for pixel-art scaling (e.g., 2×, 3×) or dynamic zoom-in/out effects.
  - **Resize mode**: how the visible world area responds when the window is resized. Options typically include: fixed world area (zoom changes), fixed zoom (world area changes), and fixed width or fixed height.
- Where this lives (on `Camera`, a `DisplaySettings` object, a `ResolutionManager`, etc.) is an open design question — to be decided at implementation time by referencing FRB1's approach.
- Must integrate with pixel-perfect snapping (see Camera Movement and Control item) so that integer zoom levels produce clean pixel alignment.

## Camera Movement and Control

**Status**: Not implemented
**What's needed**:
- No built-in camera movement exists. Game code must manually set `Camera.X`/`Camera.Y` each frame, with no smoothing, following, or bounds clamping provided.
- Two design directions to evaluate — pick one or support both:
  1. **Built-in camera behaviors on `Camera`**: properties like `Camera.Follow(entity)`, `Camera.Bounds`, `Camera.Deadzone`, `Camera.Smoothing` that the engine applies each frame automatically.
  2. **`CameraController` entity**: a first-class `Entity` subclass that holds a `Camera` reference and implements movement logic in `CustomActivity` — game code adds it to the screen like any other entity.
- Required behaviors regardless of approach:
  - **Target following**: smoothly track a target position (lerp or spring-based), with configurable lead distance.
  - **World bounds clamping**: prevent the camera from showing outside a defined world rectangle.
  - **Deadzone**: a region around the target within which the camera does not move.
  - **Pixel-perfect snapping**: snap the final camera position to the nearest integer pixel before computing the transform matrix, eliminating sub-pixel shimmer for pixel-art games. This replaces the earlier standalone "Camera Pixel-Perfect Mode" item.
- Screen shake and other effects (see separate item if added) should compose on top of whatever movement system is chosen.
