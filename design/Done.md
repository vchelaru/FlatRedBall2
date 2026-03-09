# FlatRedBall2 — Completed Items

## Audio System
`AudioManager` (at `Engine.Audio`) wraps MonoGame `SoundEffect` and `Song`. `Play(SoundEffect)` supports volume/pitch/pan, per-frame dedup (same sound plays at most once per frame), and a concurrent-instance cap (`MaxConcurrentSounds = 32`). Music via `PlaySong(Song, loop)` and `PlayPlaylist(params Song[])` with automatic playlist advancement. `SoundVolume`/`MusicVolume`/`SoundEnabled`/`MusicEnabled` for master control. Assets loaded via `Content.Load<SoundEffect>()` / `Content.Load<Song>()`.

## Gum Project File Loading (.gumx)
`EngineInitSettings.GumProjectFile` passed to `FlatRedBallService.Initialize`. Gum loads fonts (`.fnt` + texture atlas) and textures directly from disk. `.fnt`/`.png` assets are copied to output via `<Content Include="Content\GumProject\**" CopyToOutputDirectory="PreserveNewest" />` — not processed by MGCB. `_gum.LoadAnimations()` called automatically when a project file is provided. Demonstrated in PongGravity.

## TileShapeCollection — Polygon Tile Support
`AddPolygonTileAtCell(int col, int row, Polygon prototype)` / `RemovePolygonTileAtCell` / `GetPolygonTileAtCell`. Collision (`GetSeparationFor`) and `Raycast` both dispatch to polygon tiles. Screen.Add wires up render callbacks for both tile types.

## Per-Shape Collision Exclusion
`Add<T>(T child, bool isDefaultCollision)` attaches a shape without adding it to the default collision set. `SetDefaultCollision(ICollidable shape, bool)` toggles participation at runtime (idempotent; throws if shape is not a child of the entity). Demo in `DefaultCollisionDemoScreen`.

## Per-Shape Collision Targeting in Collision Relationships
`WithFirstShape(Func<A, ICollidable>)` and `WithSecondShape(Func<B, ICollidable>)` fluent methods on `CollisionRelationship<A, B>`. Selectors restrict which child shape is used for detection and response while keeping physics response applied to the parent entity. Demo in `ShapeSelectDemoScreen`.

## Y-Sort Rendering (Top-Down Draw Order)
`Screen.SortMode = SortMode.ZSecondaryParentY`. Items with different Z sort by Z; items with equal Z sort by parent entity's world-space Y descending. Uses insertion sort (stable, O(N) when nearly sorted).

## Moving Entities Between Layers
`entity.MoveToLayer(layer)` updates the `Layer` property on all `IRenderable` children and Gum visual children, recursing into child entities. `IRenderable.Layer` has a public setter.

## Path Object (Movement and Rendering)
`Path` (in `FlatRedBall2.Math`) and `PathFollower` (in `FlatRedBall2.Movement`). Builder API: `MoveTo`, `MoveBy`, `LineTo`, `LineBy`, `ArcTo`, `ArcBy`. Arc angles are signed radians (+ = CCW, − = CW, Y+ up). `IsLooped`, `PointAtLength/Ratio`, `TangentAtLength/Ratio`. `PathFollower`: `Speed`, `Loops`, `FaceDirection`, `WaypointReached`, `PathCompleted`, `Reset()`. Demo in `PathDemoScreen`.

## Overlay / Immediate-Mode Debug Drawing
`Overlay` static class in `FlatRedBall2.Diagnostics`: `Circle`, `Rectangle`, `Line`, `Arrow`, `Polygon`, `Sprite`, `Text`, `TextBackground`. Objects are pooled; appear for one frame and hide automatically the next. Pool resets on screen transition.

## Resolution Control and Camera Display Settings
`FlatRedBallService.DisplaySettings` (`DisplaySettings` class, `ResizeMode` enum, `WindowMode` enum). Covers `ResolutionWidth/Height`, `Zoom`, `ResizeMode` (`StretchVisibleArea` / `IncreaseVisibleArea`), `FixedAspectRatio` (letterbox/pillarbox), `WindowMode` (`Windowed` / `FullscreenBorderless`), `AllowUserResizing`. `PrepareWindow<T>` for flicker-free startup; `ApplyWindowSettings` for runtime changes.

## Pause-Aware Delay
`TimeManager._sinceScreenStart` (exposed as `CurrentScreenTimeSeconds`) does not advance while `Screen.IsPaused`. `DelaySeconds` and `DelayUntil` compare against this value, so they automatically suspend during pause. `DelayFrames` is not pause-aware by design — frame counting continues regardless. `TimeScale` still applies to screen time. `DeltaSeconds` in `FrameTime` reflects real elapsed time (not zeroed when paused). `ResetScreen()` resets screen time on screen transition.

## Pause System
`Screen.IsPaused`, `PauseThisScreen()`, `UnpauseThisScreen()`. When paused, entity physics, collision, and entity `CustomActivity` are all skipped. `Screen.CustomActivity` always runs so the screen can respond to input (e.g., showing a pause menu). TopDownMenuSample updated to use an in-screen Gum overlay rather than a screen transition, so game world state is preserved across pause/resume. `BouncingBallsSample` added as a demo: click to spawn balls with gravity that bounce off a `TileShapeCollection` arena and self-collide; ESC toggles pause with a code-only Gum label.

## Camera Movement and Control
`CameraControllingEntity` in `src/Entities/CameraControllingEntity.cs`. `Target`/`Targets`, `TargetApproachStyle` (`Immediate`/`Smooth`/`ConstantSpeed`), `Map` (level bounds clamping), `ScrollingWindowWidth/Height` (deadzone), `SnapToPixel`, `CameraOffset`, `EnableAutoZooming`, `IsKeepingTargetsInView`, `ShakeScreen`/`ShakeScreenUntil` (async, auto-cancels on transition), `ForceToTarget()`.
