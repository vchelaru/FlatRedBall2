# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Tiled Integration — Remaining Work

Using MonoGame.Extended 6.0 (preview) for Tiled support. The `TileMapLayerRenderable` handles per-layer rendering with manual tile drawing for Z-order integration.

Remaining:
- Verify `TilemapTileFlipFlags.FlipDiagonally` rotation renders correctly (now attempted via 90° rotation in `TileMapLayerRenderable`).
- `CameraControllingEntity.Map` should become a `MapBounds` struct/interface instead of `AxisAlignedRectangle`.
- Consider migrating to `TilemapSpriteBatchRenderer` for maps that don't need per-layer Z control (better performance via frustum culling).

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT
**Priority: Eventual** — currently targets MonoGame.Framework.DesktopGL only.

- Identify abstraction points for graphics init, fullscreen APIs, input, audio, content pipeline
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

