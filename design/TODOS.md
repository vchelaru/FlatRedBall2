# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Slope Polygons in Tiled Maps — Phase 2
**Priority: Next** — Phase 1 (emit polygon tiles from tileset `<polygon>` collision objects) is complete. Remaining work:

- Support `TilemapRectangleObject` (and maybe `TilemapEllipseObject`) collision objects as sub-cell rects — currently ignored
- Add TMX-based slope examples to sample levels
- Update `levels` skill once Phase 2 lands

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

