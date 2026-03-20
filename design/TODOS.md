# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Tiled Integration — Remaining Work

Using MonoGame.Extended 6.0 (preview) for Tiled support. `TileMapLayerRenderable` delegates to `TilemapSpriteBatchRenderer.DrawLayer()` for per-layer rendering with frustum culling and automatic flip/rotation handling.

Remaining:
- `CameraControllingEntity.Map` should become a `MapBounds` struct/interface instead of `AxisAlignedRectangle`.

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT
**Priority: Eventual** — currently targets MonoGame.Framework.DesktopGL only.

- Identify abstraction points for graphics init, fullscreen APIs, input, audio, content pipeline
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

