# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Tiled Collision Objects — Remaining

Phases 1 (polygon tiles) and 2 (sub-cell `<object>` rectangles) are complete. Remaining items:

- Add TMX-based slope/sub-cell examples to sample levels
- `TilemapEllipseObject` stays out of scope: FRB2 has `Circle` with uniform radius only, and Tiled ellipses allow `rx != ry`; no realistic tile-collision use case justifies the approximation work

## Slope Speed Adjustment (Platformer)
**Priority: Eventual** — Port the slope-speed multiplier pattern from FRB1. When a platformer entity walks on a slope, its horizontal speed should adjust based on slope steepness (usually faster downhill, slower uphill). Config lives on `PlatformerValues` (something like `UphillStopAngle`, `UphillSlowDownSlope`, `DownhillSpeedBoost`, matching the FRB1 naming). Applied in `PlatformerBehavior` after determining current-surface normal (re-use the snap probe's normal, or query it separately).

## Polygon Snagging in Top-Down (Standard mode)
**Priority: Eventual** — `TileShapeCollection` in `SlopeCollisionMode.Standard` can still snag at seams between adjacent polygon tiles when used for top-down angled walls.

- `Polygon.SuppressedEdges` bitfield exists but isn't wired into SAT correctly (opposite edges share the same axis, so edge-level suppression during SAT doesn't eliminate the axis)
- Consider a post-process MTV filter analogous to rectangles' `ComputeDirectionalSeparation`, or a different approach entirely
- Tests removed when we deferred this; re-add when addressed

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT
**Priority: Eventual** — currently targets MonoGame.Framework.DesktopGL only.

- Identify abstraction points for graphics init, fullscreen APIs, input, audio, content pipeline
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

