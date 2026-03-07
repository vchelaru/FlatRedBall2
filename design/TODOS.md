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

**Status**: Done — `FlatRedBallService.DisplaySettings` (`DisplaySettings` class, `ResizeMode` enum, `WindowMode` enum) in `src/Rendering/DisplaySettings.cs`.
- `ResolutionWidth`/`ResolutionHeight`: design resolution (world units visible at Zoom=1). Used as fixed world area for `StretchVisibleArea`. Also the fallback window size when restoring from fullscreen with no explicit size.
- `Zoom`: initial camera zoom. Copied to `Camera.Zoom` at each screen start; camera is independent after that. At 1.0, one world unit = one pixel.
- `ResizeMode`: `StretchVisibleArea` (fixed world area, scale fills window) or `IncreaseVisibleArea` (fixed pixels-per-unit, larger window shows more world).
- `FixedAspectRatio` (`float?`): enforces aspect ratio with letterbox/pillarbox bars. `null` = fill window.
- `LetterboxColor`: color of bars when `FixedAspectRatio` is set.
- `WindowMode`: `Windowed` or `FullscreenBorderless`. No hardware/exclusive fullscreen — `HardwareModeSwitch = false` + `IsFullScreen = true` on MonoGame DesktopGL.
- `PreferredWindowWidth`/`PreferredWindowHeight`: pixel size of the window at startup. `null` = leave unchanged.
- `AllowUserResizing`: whether the player can drag window borders.
- `Camera.Zoom`: runtime zoom (modifiable by game code, reset to `DisplaySettings.Zoom` on screen start).
- `Camera.TargetWidth`/`TargetHeight` have `internal set` — managed by the engine, not set directly by game code.
- `FlatRedBallService` subscribes to `Window.ClientSizeChanged` and updates the camera viewport and `TargetWidth`/`TargetHeight` (for `IncreaseVisibleArea`) on resize.
- `FlatRedBallService.PrepareWindow<T>(GraphicsDeviceManager)`: call from `Game1` constructor to apply startup window/fullscreen settings before `Initialize()` (no flicker).
- `FlatRedBallService.ApplyWindowSettings(DisplaySettings)`: public, safe to call at any time for runtime changes (settings menu, F11 toggle). Updates `DisplaySettings.WindowMode` to reflect current state. When restoring to windowed with no explicit size, falls back to `ResolutionWidth`/`ResolutionHeight`. Re-centers window after exiting fullscreen.
- Pixel-perfect snapping not yet implemented (see Camera Movement and Control item).

## Gum Project File Loading (.gumx)

**Status**: Not started — high priority
**What's needed**:

FRB2 currently runs Gum in "code-only" mode (`DefaultVisualsVersion.V2`). Full `.gumx` project support lets designers build UI in the Gum tool and have it loaded at runtime, enabling component reuse, screen-level layouts, and animations defined outside of C# code.

**MonoGameGum API entry point**: `GumService.Initialize(Game game, string gumProjectFile)` — already exists in the `Gum.MonoGame` package. The `gumProjectFile` is a path to the `.gumx` file (or the folder containing it). Returns the loaded `GumProjectSave`, or null if no project was loaded.

**What FRB2 must add**:

1. **Initialization path** — `FlatRedBallService.Initialize` currently calls `_gum.Initialize(game, DefaultVisualsVersion.V2)`. Add an overload or property (e.g., `FlatRedBallService.Default.GumProjectFile = "Content/MyProject.gumx"`) that switches to the project-loading `Initialize` overload instead. The two modes (code-only vs. project) are mutually exclusive.

2. **Content/font loading** — Gum loads fonts (`.fnt` + texture atlas) and textures from disk directly, not through the MonoGame content pipeline. Verify the correct `Content.RootDirectory` is set and that `.fnt`/`.png` assets are copied to output (not processed by MGCB). Document which assets go through MGCB vs. which are copied raw.

3. **Animation loading** — `GumService.LoadAnimations()` must be called after project load if the project uses animations. Wrap this or expose it via `FlatRedBallService.Gum`.

4. **Screen-to-GumScreen mapping** — Decide how FRB2 `Screen` types access Gum screens defined in the `.gumx`:
   - Option A: `Screen.GumScreen` property that game code populates manually by name from the loaded project (`GumService.GetScreen("MainMenu")`).
   - Option B: A naming convention (`Screen` subclass name matches Gum screen name) with automatic wiring in `ActivateScreen`.
   - The chosen approach must not break existing code-only Gum usage.

5. **Component instantiation** — Gum components defined in `.gucx` files should be instantiatable in game code by type name (e.g., `GumService.CreateComponent("HealthBar")`), not just built manually in C#.

6. **Screen lifecycle** — On screen transition, the current Gum screen's elements must be cleared/unloaded without disposing shared project assets (fonts, textures shared across screens).

**Related**: The `.gumx` load path may surface AOT issues (Gum uses XML deserialization internally); annotate when/if encountered.

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT

**Status**: Not started — eventual goal
**What's needed**:
- FRB2 currently targets MonoGame.Framework.DesktopGL exclusively. The goal is to also support FNA and KNI as drop-in backends.
- Each backend diverges on: graphics device initialization, fullscreen/borderless APIs, input handling, audio, and content pipeline format. Abstraction points will need to be identified and isolated (likely a thin `IBackend` or conditional compilation layer).
- Native AOT compatibility is a long-term goal. Blockers to audit:
  - Reflection-based code (e.g., `Activator.CreateInstance`, `Type.GetType`, `MakeGenericMethod`) must be replaced with source generators or static registration.
  - MonoGame content pipeline and Gum may have reflection dependencies of their own that need upstream fixes or workarounds.
  - AOT requires trimming-safe patterns throughout; `[DynamicallyAccessedMembers]` annotations or source-generated alternatives needed.
- Platform targets that fall out of this work: Windows (x64/ARM64), macOS, Linux, iOS, Android, and console platforms reachable via KNI.
- No action needed yet — flag any new code that is reflection-heavy or AOT-hostile so it can be cleaned up when this work begins.

## .NET 10 and Latest Language Features

**Status**: Not started
**What's needed**:
- FRB2 currently targets `net9.0`. Upgrading to `net10.0` unlocks C# 14 language features (e.g., null-conditional assignment `??=`, field-backed properties, `params` spans) and runtime improvements.
- Update `<TargetFramework>` in `src/FlatRedBall2.csproj` and `tests/FlatRedBall2.Tests/FlatRedBall2.Tests.csproj`.
- Update `<LangVersion>` (or remove it to default to the latest for the TFM).
- Verify all NuGet dependencies (MonoGame, Gum, Apos.Shapes) have net10 or `netstandard2.0`/`net6.0`+ compatible builds.
- Run full test suite and sample builds after the upgrade.

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
