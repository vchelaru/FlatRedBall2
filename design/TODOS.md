# FlatRedBall2 — Deferred Items

This document tracks features and systems intentionally deferred from the initial implementation. Each item includes context and the recommended approach when it is implemented.

## Polygon — Concave Collision (Convex Decomposition)

**Status**: Not implemented — `Polygon` collision uses SAT, which only works for convex polygons.
**What's needed**:
- SAT is fundamentally limited to convex shapes. Concave polygons produce incorrect collision responses: shapes can pass through concave regions or collide with empty concave areas.
- Rendering already supports concave polygons via ear-clip triangulation; collision does not.
- Recommended approach: at `FromPoints` / `AddPoint` time, decompose the polygon into convex sub-polygons (e.g., Hertel-Mehlhorn convex partitioning on top of the existing ear-clip triangulation). Store the sub-polygons internally and union their collision results.
- `CollisionDispatcher` would need a `PolygonVsX` path that iterates sub-polygons.
- Consider whether the sub-polygon decomposition should be exposed publicly (e.g., `Polygon.ConvexParts`) for debugging.

## TileShapeCollection — Polygon Tile Support

**Status**: Not implemented
**What's needed**:
- `TileShapeCollection` currently stores only `AxisAlignedRectangle` tiles. There is no way to place a `Polygon` (e.g., a 45° slope triangle, a ramp, a concave wall section) as a tile.
- Common use cases: sloped terrain, angled platforms, ramps, non-rectangular collision in tile-based levels.
- Recommended approach: store a discriminated shape per cell — either a rectangle or a polygon — and dispatch to the correct collision resolver at runtime. The existing `AddTileAtCell` API could gain an overload accepting a `Polygon` (or a predefined `TileShape` enum for common cases like `TopLeftSlope`, `TopRightSlope`).
- The raycast and broad-phase queries must also handle polygon tiles so that `Raycast` and entity collision work correctly against sloped tiles.
- Consider whether polygon tiles can share a single prototype shape (translated per cell) to avoid allocating one `Polygon` instance per tile.

## Line vs TileShapeCollection — Closest Intersection

**Status**: Implemented — `TileShapeCollection.Raycast(Vector2 start, Vector2 end, out Vector2 hitPoint, out Vector2 hitNormal)` using DDA traversal.
**What's needed**:
- A query method on `TileShapeCollection` (or a static helper) that casts a line segment and returns the closest point of intersection with any tile — e.g., `TileShapeCollection.Raycast(Vector2 start, Vector2 end, out Vector2 hitPoint, out Vector2 hitNormal)`.
- "Closest" means the intersection nearest to `start`, not just any intersection.
- Use cases: line-of-sight checks, bullet/laser hit detection, sniper sight lines.
- Implementation should walk tiles along the line using a grid traversal algorithm (e.g., DDA or Amanatides–Woo) rather than brute-forcing all tiles — otherwise it defeats spatial partitioning.
- Return value should indicate whether any intersection occurred (bool), with `out` parameters for hit point and surface normal (needed for ricochet or wall-hugging behavior).

## Spatial Partitioning — Design Discussion Needed

**Status**: Design not yet decided
**What's needed**:
- `TileShapeCollection` already uses a dictionary-based grid for O(1) tile lookup, which is effectively a uniform-grid spatial partition. This is fine for tile collision.
- Open question: do we need a general-purpose spatial partition for entity-vs-entity collision at scale (many enemies, many projectiles)?
- Current `AddCollisionRelationship` does O(n×m) broad-phase — acceptable for small lists, becomes a bottleneck in shmups or crowd games.
- Candidates: uniform grid, quadtree, or simply relying on Apos.Shapes' existing partitioning. Decision should be made before implementing any dense entity-vs-entity collision sample.
- When decided, the partition should be transparent to game code (no API change to `AddCollisionRelationship`) — the screen manages it internally.

## Per-Shape Collision Exclusion

**Status**: Not implemented
**What's needed**:
- Currently, any shape passed to `Entity.Add` is automatically included in `_shapes` and therefore participates in `ICollidable`. There is no way to add a shape for rendering/attachment only without it being part of collision.
- Common use case: a visual indicator shape (e.g., a larger "shadow" circle or a range indicator) that should render but never collide.
- Recommended approach: an overload or flag on `Add` — e.g., `Add(shape, collides: false)` — that adds the shape to `_children` for attachment/rendering but skips adding it to `_shapes`. Alternatively, a separate `AddVisual(shape)` method.
- The exclusion should be reversible at runtime so shapes can be toggled in/out of collision (e.g., an invincibility window).

## Per-Shape Collision Targeting in Collision Relationships

**Status**: Not implemented
**What's needed**:
- Currently, `AddCollisionRelationship` uses all shapes in an entity's `ICollidable` (the full `_shapes` list). There is no way to target a specific named shape — e.g., collide only the "feet" rectangle against the ground, or only the "hitbox" circle against enemies.
- Common use cases: platformer feet vs. terrain (ignoring a body hitbox), top-down melee hitbox vs. hurtbox, JRPG interaction trigger vs. interactables, a "shadow" rectangle that collides with the floor but not walls.
- Recommended approach: an overload on `AddCollisionRelationship` that accepts a shape selector, e.g.:
  ```csharp
  screen.AddCollisionRelationship<Player, TileShapeCollection>(
      players, tiles,
      firstShape: p => p.FeetCollision,
      response: CollisionResponse.MoveFirst);
  ```
- The selector could alternatively be a `string` name if shapes get a `Name` property, but a typed lambda is safer and refactor-friendly.
- The `ICollidable` interface currently returns all shapes; per-shape targeting means the collision relationship must bypass the interface and call the resolver directly with the selected shape.

## Y-Sort Rendering (Top-Down Draw Order)

**Status**: Not implemented
**What's needed**:
- In top-down games, entities lower on screen must render in front of entities higher on screen — the classic painter's-algorithm Y-sort.
- A `SortMode` or `DrawOrder` concept on the renderer or per-layer, with at least a `YDescending` option (lower world Y = drawn later = appears in front).
- Could be a property on `Screen` or `Camera` (e.g., `Camera.SpriteSortMode = SpriteSortMode.YDescending`) that changes the sort key used when flushing the sprite batch each frame.
- Entities need a way to opt in or override their sort key (e.g., a `DrawY` offset for characters whose feet are not at `Y = 0`).

## Moving Entities Between Layers

**Status**: Not implemented
**What's needed**:
- No first-class API exists for transferring an entity (and all its children — shapes, sprites, Gum components) from one layer to another at runtime.
- Common use cases: a pickup moving from the world layer to a foreground layer when collected, a player "entering" a building and switching to an interior layer, toggling an entity onto a debug/overlay layer.
- Recommended approach: a method on `Entity` or `Screen` such as `entity.MoveToLayer(newLayer)` that atomically removes the entity's renderables from their current layer and adds them to the new one, preserving relative child ordering.
- Must handle all child types uniformly (shapes, sprites, Gum objects) so game code doesn't have to manually re-parent each child.
- Collision participation is independent of layer — moving an entity to a different layer must not affect which collision relationships it participates in.

## Path Object (Movement and Rendering)

**Status**: Not implemented
**What's needed**:
- A `Path` type that represents an ordered sequence of points and serves two purposes: (1) guiding entity movement along the path, and (2) rendering the path as a visible line or curve.
- Movement use cases: patrol routes, cutscene scripted movement, projectile arcs, NPC waypoints.
- Rendering use cases: displaying a trajectory preview, drawing a rope/cable, showing a debug movement route.
- Recommended API surface:
  - `Path` holds a list of `Vector2` waypoints and supports open or closed (looped) configurations.
  - A `Path.GetPositionAtDistance(float distance)` or `Path.GetPositionAtT(float t)` query for movement interpolation (linear segments to start; bezier curves as a future extension).
  - A `Path.Renderable` (or `Path` itself implementing `IRenderable`) that draws the polyline each frame, with configurable color and thickness.
- Movement helpers: a component or utility (e.g., `PathFollower`) that advances an entity along a `Path` at a given speed, fires an event on arrival at each waypoint, and optionally loops.
- The rendering and movement roles should be independently usable — a `Path` can be drawn without any entity following it, and a `PathFollower` should work without the path being visible.

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
