# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Tiled Integration — Remaining Work

`TiledMapLayerRenderable` and `TiledCollisionGenerator` are implemented (MonoGame.Extended 5.* / MonoGame 5.4.*).

Remaining:
- `IsFlippedDiagonally` (anti-diagonal flip / 90° rotation) is not yet rendered correctly — tiles are drawn unrotated. Requires passing a rotation angle to `SpriteBatch.Draw`.
- `CameraControllingEntity.Map` should become a `MapBounds` struct/interface instead of `AxisAlignedRectangle` now that Tiled integration is live.

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT
**Priority: Eventual** — currently targets MonoGame.Framework.DesktopGL only.

- Identify abstraction points for graphics init, fullscreen APIs, input, audio, content pipeline
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

