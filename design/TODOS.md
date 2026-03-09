# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Gum — Zoom Correctness
**Priority: Medium**

When `Camera.Zoom != 1`, Gum layout and rendering must both scale correctly. The current approach:
1. Update loop divides `GraphicalUiElement.CanvasWidth/Height` by `Camera.Zoom` — keeps layout units consistent.
2. `GumRenderBatch.Begin` passes `Matrix.CreateScale(zoom, zoom, 1f)` to `GumBatch.Begin` — scales rendered output.

This is believed to be correct in principle but has **not been fully verified**. Need to test Gum UI at various zoom values (e.g. 1×, 1.5×, 2×) and window sizes and confirm:
- Anchored elements (TopLeft, TopRight, Center, etc.) land at the right screen positions.
- Text and control sizes appear proportionally correct.
- No offset or clipping artefacts at non-integer zoom levels.

The PongGravity sample uses `DisplaySettings.PreferredWindowWidth/Height = 2560×1440` with default zoom (1×) for its 2× scale effect — it does not exercise `Camera.Zoom` directly. A dedicated test or demo screen would be the cleanest way to verify.

## ACHX Animation Format
**Priority: Medium** — `Sprite.PlayAnimation` is a no-op.

- Parser for `.achx` XML format (from FRB1)
- `AnimationChain` and `AnimationFrame` runtime types
- Per-frame texture region, timing, and relative X/Y offset
- `Sprite.CurrentAnimation` state machine: playing, looped, completed events

## Async Synchronization Context
`// TODO: flush async sync context` comment in update loop.

- Custom `SynchronizationContext` that queues continuations
- Flush pending continuations during update loop (after collision, before CustomActivity)
- Allows `await` in entity/screen code to resume on the game thread

## Spatial Partitioning
**Design decision needed before building any dense entity-vs-entity collision sample.**

Current `AddCollisionRelationship` does O(n×m) broad-phase — fine for small lists, bottleneck in shmups or crowd games. Candidates: uniform grid, quadtree, or Apos.Shapes partitioning. Decision should be transparent to game code (no API change to `AddCollisionRelationship`).

## Polygon — Concave Collision
**Priority: Low / Deferred** — SAT only works for convex polygons. Concave polygons produce incorrect results.

- At `FromPoints`/`AddPoint` time, decompose into convex sub-polygons (e.g., Hertel-Mehlhorn on top of existing ear-clip triangulation)
- `CollisionDispatcher` would need a `PolygonVsX` path that iterates sub-polygons
- Consider exposing `Polygon.ConvexParts` for debugging

## Tiled Integration — TMX → TileShapeCollection
**Blocked** — waiting on MonoGame.Extended supporting the target MonoGame version.

- `TiledMapLayerRenderable.Draw` renders a tile layer
- `TiledCollisionGenerator.Generate` reads tile properties and calls `TileShapeCollection.AddTileAtCell`
- When this lands, `CameraControllingEntity.Map` should become a `MapBounds` struct/interface instead of `AxisAlignedRectangle`

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT
**Priority: Eventual** — currently targets MonoGame.Framework.DesktopGL only.

- Identify abstraction points for graphics init, fullscreen APIs, input, audio, content pipeline
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

