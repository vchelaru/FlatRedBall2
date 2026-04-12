# FlatRedBall2 — Completed Items

## Slope Polygons in Tiled Maps — Phase 2 (sub-cell rectangles)
`TileMapCollisionGenerator` now honors plain `<object>` rectangle collision objects (`TilemapRectangleObject`) in addition to polygons. A tile with any custom collision object (polygon or rect) skips the default full-cell rect; both shape types emit side-by-side on the same tile if authored. Rects are converted from Tiled's Y-down top-left coords to FRB2 centered Y-up local offsets (`cx = x + w/2 - G/2`, `cy = G/2 - (y + h/2)`) and placed via the new `TileShapeCollection.AddRectangleTileAtCell(col, row, localCenterX, localCenterY, width, height)`. Multiple rects per cell are allowed (unlike polygons, which throw on duplicate). Sub-cell rects are stored in a separate `Dictionary<(col,row), List<AxisAlignedRectangle>>` so they participate in visual property propagation, `Clear`, `ShiftAllTiles`, and collision (`GetSeparationFor`) but not in `RepositionDirections` adjacency updates (they do not cover full cell faces). Query via `GetRectangleTilesAtCell(col, row)`. Tile flip flags (H, V, diagonal) are honored for rects: diagonal transposes both center (`(cx,cy) → (-cy,-cx)`) and size (swap w/h); H negates cx; V negates cy — applied D → H → V matching the polygon path. `TileShapeCollection.Raycast` does not yet test sub-cell rects (tracked in TODOS). The former `TryBuildPolygonPrototypes` helper in `TileMapCollisionGenerator` is now `BuildCollisionShapes`, producing both polygon prototypes and rect specs in a single pass.

## Slope Polygons in Tiled Maps — Phase 2 (flip flags)
`TileMapCollisionGenerator.TryBuildPolygonPrototypes` now honors `TilemapTile.FlipFlags`. Flips are applied in Tiled's declared order (diagonal → horizontal → vertical) in centered-Y-up space: D swaps to `(-y, -x)`, H negates x, V negates y. Point order is preserved — `Polygon.FromPoints` normalizes winding internally so the odd-flip-reverses-winding concern doesn't reach SAT. `TileShapeCollection.AddPolygonTileAtCell` now throws `InvalidOperationException` on a duplicate `(col, row)` instead of silently dropping the second polygon; multi-polygon-per-cell is unsupported and authors should merge shapes in Tiled.

## Slope Polygons in Tiled Maps — Phase 1
`TileMapCollisionGenerator` now inspects `TilemapTileData.CollisionObjects` when building collision from a tile layer. For each matched tile with one or more `TilemapPolygonObject`s (authored as `<polygon>` in a tileset tile's `<objectgroup>`), the generator converts points from Tiled pixel space (Y-down, origin top-left of tile) to local Polygon space (Y-up, centered on the cell: `localX = tiledX - G/2`, `localY = G/2 - tiledY`) and calls `TileShapeCollection.AddPolygonTileAtCell` with a prototype built from `Polygon.FromPoints`. Tiles without polygon objects still emit a rect via `AddTileAtCell`. Flip flags, rectangle/ellipse collision objects, and multiple polygons per cell are deferred to Phase 2 (see TODOS.md).

## TileNodeNetwork (A* Pathfinding)
`TileNodeNetwork` in `FlatRedBall2.AI` is a grid-based node network for A* pathfinding. Construct with `xOrigin`, `yOrigin`, `gridSpacing`, `xCount`, `yCount`, and `DirectionalType` (Four or Eight). Populate via `FillCompletely()` or `AddAndLinkNode(x, y)` (by tile index) / `AddAndLinkNodeAtWorld(wx, wy)`. Remove nodes via `RemoveAt(x, y)`, `RemoveAtWorld`, or `RemoveNodesOverlapping(AxisAlignedRectangle)`. For 8-way networks, call `EliminateCutCorners()` after carving walls to prevent diagonal movement through corners of missing tiles. Query with `NodeAt(x,y)`, `NodeAtWorld`, and `GetClosestNode`. Find paths with `GetPath(start, end)` (allocates) or `GetPath(start, end, List<Vector2>)` (reuse to avoid GC). Result contains world positions from start (exclusive) to end (inclusive). `TileNode.Tag` holds optional game data per node.

## ACHX Animation Format
`AnimationFrame`, `AnimationChain` (extends `List<AnimationFrame>`), and `AnimationChainList` (extends `List<AnimationChain>`, string indexer by name) are the runtime types in `FlatRedBall2.Animation`. `AnimationChainListSave` / `AnimationChainSave` / `AnimationFrameSave` in `FlatRedBall2.Animation.Content` handle XML deserialization of `.achx` files via `AnimationChainListSave.FromFile(path)` and conversion to runtime via `ToAnimationChainList(ContentManagerService)` (content pipeline) or `ToAnimationChainList(GraphicsDevice)` (raw file loading via `Texture2D.FromFile`; textures are cached per path). Supports `TimeMeasurementUnit` (Undefined/Second/Millisecond — Undefined treated as Second for FRB1 compatibility), `TextureCoordinateType` (UV/Pixel), and `FileRelativeTextures`. `AnimationFrame.RelativeX`/`RelativeY` are applied to the sprite's local `X`/`Y` on each frame switch so per-frame positional offsets work correctly. `Sprite.AnimationChains`, `PlayAnimation(string)`, `PlayAnimation(AnimationChain)`, `Animate`, `IsLooping`, `AnimationSpeed`, `CurrentAnimation`, and `AnimationFinished` event are the playback API. `Screen.Update` calls `Sprite.AnimateSelf` on all sprites each frame inside the `!IsPaused` block. `PlatformerSample` includes `AnimatedPlayer` — an entity with `PlatformerBehavior` and a `Sprite` driven by `PlatformerAnimations.achx` (FRBeefcake spritesheet), switching between Idle/Walk/Jump/Fall × Left/Right based on movement state each frame.

## Polygon — Concave Collision
`Polygon.ConvexParts` (`IReadOnlyList<IReadOnlyList<Vector2>>`) exposes the convex decomposition in local space. `FromPoints`, `SetPoints`, and `CreateRectangle` automatically decompose: convex polygons get a single part (the original points); concave polygons are ear-clip triangulated then Hertel-Mehlhorn merged into the minimum set of convex sub-polygons. `CollisionDispatcher` iterates `ConvexParts` in `PolygonVsPolygon`, `PolygonVsAabb`, and `PolygonVsCircle`, running SAT per part-pair and returning the minimum-magnitude MTV from any overlapping pairs. Shapes placed in concave pockets no longer produce false collisions.

## Spatial Partitioning (Sweep-and-Prune)
`Factory<T>.PartitionAxis` (type `Axis` — `X` or `Y`) opts a factory into broad-phase culling. The engine sorts each factory's list in-place once per frame (insertion sort — O(n) on nearly-sorted data) before collision relationships run. Any `CollisionRelationship` whose both lists are factories with matching non-null `PartitionAxis` automatically sweeps instead of doing O(n×m) checks — pairs separated by more than their combined `BroadPhaseRadius` are skipped. Mismatched or null axes silently fall back to full O(n×m). `CollisionRelationship.DeepCollisionCount` reports narrow-phase checks performed last frame for profiling. `TileShapeCollection` is already internally partitioned by grid lookup and does not need this.

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

## Tiled Tile Rendering via TilemapSpriteBatchRenderer
`TileMapLayerRenderable` delegates to MonoGame.Extended's `TilemapSpriteBatchRenderer.DrawLayer()` for per-layer rendering. This provides frustum culling (only visible tiles drawn) and automatic handling of all 8 tile flip/rotation combinations. A `TiledRenderBatch` (no-op `IRenderBatch`) ensures the `SpriteBatch` is not in an active state when the MGEx renderer manages its own Begin/End. FRB's Y-up camera is mapped to an `OrthographicCamera` in Tiled's Y-down space per draw call.

## Camera Movement and Control
`CameraControllingEntity` in `src/Entities/CameraControllingEntity.cs`. `Target`/`Targets`, `TargetApproachStyle` (`Immediate`/`Smooth`/`ConstantSpeed`), `Map` (level bounds clamping via `BoundsRectangle`), `ScrollingWindowWidth/Height` (deadzone), `SnapToPixel`, `CameraOffset`, `EnableAutoZooming`, `IsKeepingTargetsInView`, `ShakeScreen`/`ShakeScreenUntil` (async, auto-cancels on transition), `ForceToTarget()`.

## BoundsRectangle Struct
`BoundsRectangle` (`readonly record struct` in `FlatRedBall2.Math`). Center-origin axis-aligned rectangle with `CenterX`, `CenterY`, `Width`, `Height` and computed `Left`/`Right`/`Top`/`Bottom` edges. Lightweight value type with no collision/rendering/attachment overhead — replaces `AxisAlignedRectangle` for non-collision uses like camera map bounds. Two-param constructor `BoundsRectangle(width, height)` centers at origin.
