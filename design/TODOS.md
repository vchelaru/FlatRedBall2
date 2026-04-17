# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Tiled Collision Objects — Non-goals

Phases 1 (polygon tiles), 2 (sub-cell `<object>` rectangles, flip flags), and sub-cell rect adjacency (rect↔rect, rect↔full-cell, rect↔polygon) are complete. `SlopesSample` demonstrates all of it end-to-end. Remaining out-of-scope items:

- `TilemapEllipseObject` stays out of scope: FRB2 has `Circle` with uniform radius only, and Tiled ellipses allow `rx != ry`; no realistic tile-collision use case justifies the approximation work.

## PlatformerConfig JSON — Coefficients (Landed)

> **Status: Complete.** `PlatformerConfig.FromJson` / `ApplyTo` extension landed. SlopesSample and AutoEvalCoinHopperSample converted. Template at `.claude/templates/PlatformerConfig/`. Skills and content-boundary updated. Hot-reload is a separate TODO below.

Externalizes `PlatformerValues` into a JSON file per entity. Canonical application of the `content-boundary` philosophy. Fills the fixed slots FRB2 already has: `movement.ground` → `PlatformerBehavior.GroundMovement`, `movement.air` → `AirMovement`, future `movement.afterDoubleJump` → the not-yet-wired double-jump slot.

### Animation — intentionally not engine-managed

FRB1 had an `AnimationController` / `PlatformerAnimationController` that mapped behavior states to animation chains via a layered priority system. **FRB2 does not port this.** The controller was primarily useful for FRB1's code-generation model (Glue editor emitted animation layers that coexisted with hand-written code). Without a code generator, the abstraction adds indirection for no benefit — the equivalent if-statement or pattern match is shorter, more readable, and directly debuggable. See the `platformer-movement` skill for the recommended animation pattern.

## PlatformerConfig Hot-Reload (File Watch)
**Priority: Soon** — Watch `player.platformer.json` (and any config JSON loaded via `PlatformerConfig.FromJson`) for changes and re-apply values at runtime without recompiling. This is the payoff of the content-boundary split: a designer edits JSON, saves, and sees the result in the running game immediately.

- Use `FileSystemWatcher` (or equivalent) to detect writes to the loaded path.
- On change: re-deserialize and re-apply via `ApplyTo`. Must handle partial/malformed saves gracefully (file may be mid-write) — retry on `IOException`, log parse errors rather than crashing.
- Debounce: editors often write multiple events per save; collapse into a single reload after a short delay (~100-200ms).
- API surface: something like `PlatformerConfig.WatchAndApply(path, behavior)` that returns a disposable handle, or a `FileWatchingConfig` wrapper. Exact shape TBD.
- Consider generalizing beyond `PlatformerConfig` — any JSON config file could benefit from watch+reload. A generic `ConfigWatcher<T>` might be the right abstraction, with `PlatformerConfig` as the first consumer.
- Thread safety: `FileSystemWatcher` fires on a threadpool thread; values must be applied on the game thread. Queue the reload and process it during the next `Update` tick.

## Designer-Placed Spawn Markers
**Priority: Soon** — Initial implementation landed. `TileMap.CreateEntities<T>` with `Origin` enum and reflection-based property mapping. AutoEvalCoinHopperSample converted. Needs runtime testing to verify coordinate conversion.

### Decision: Tiled Object Layers Behind a Stable Wrapper

Use Tiled object layers with **visual tiles** (designers place tiles from the art tileset onto object layers) and **Tiled Classes** on tile definitions for type identification. The engine surfaces these through a wrapper API so the underlying source (Tiled today, possibly LDTK later) can change without breaking game code.

### Design Decisions

- **Visual tiles with classes.** Designers place tiles from the visual tileset onto object layers. Each tile definition in the tileset has a Class (e.g., `"Coin"`, `"Player"`, `"CeilingTurret"`). This differs from collision layers, which use the StandardTileset on dedicated tile layers — the difference is justified because spawn markers are concrete visible things, not abstract geometry.
- **Any number of object layers.** The engine scans all object layers for matching classes. Designers organize layers however they want — one big "Entities" layer or separate layers per category. The engine doesn't care.
- **Class name as discriminator.** `CreateEntities` filters by tile Class, not by layer name or object name. Stringly-typed at the engine level; game code switches on the class string.
- **Spawn data lives in the TMX.** Spawn positions are inherently coupled to level geometry — if you move a platform, you want to see the coin sitting on it. Separate files create sync bugs.
- **World-space positions, always.** The engine converts Tiled's top-down pixel coordinates to world space (Y+ up). No opt-out flag; raw pixel coords are available by reading TMX directly.
- **Origin is a code-level concern, not a Tiled property.** The designer shouldn't see confusing alignment settings in Tiled that do nothing visually but break things in-game. Origin is an optional parameter on `CreateEntities`, defaulting to `Center`.
- **Custom properties auto-applied via reflection.** Tiled custom properties (e.g., `worth=50`, `patrolRadius=100`) are automatically mapped to matching entity properties by the engine using reflection. Zero boilerplate for game code — if the entity has `public int Worth { get; set; }` and the Tiled object has `worth=50`, it just works.

### Core API Shape

```csharp
// Spawn all Coin entities from any object layer
map.CreateEntities("Coin", coinFactory);

// Player spawns with feet-at-bottom origin
map.CreateEntities("Player", playerFactory, Origin.BottomCenter);

// Ceiling turret with top origin
map.CreateEntities("CeilingTurret", turretFactory, Origin.TopCenter);
```

`Origin` enum: `Center` (default), `BottomCenter`, `TopCenter`, `BottomLeft`, `TopLeft`, etc.

### AOT Consideration

Reflection-based property mapping conflicts with the Native AOT goal (see Multi-Backend TODO). When AOT becomes a priority, this will need a source-generator or explicit-mapping alternative. Acceptable for now — AOT is `Priority: Eventual`.

### Related friction (still open)
- `TileMap.GetCellWorldPosition(int col, int row)` helper — independent of spawn markers, but addresses similar "where in world space is this tile?" friction.

## Platformer Docs Audit (FRB1 → FRB2)
**Priority: Soon** — Manual pass through FRB1's platformer documentation (wiki, plugin README, CSV column names, PlatformerValues fields, predefined profiles, behavior hooks) to inventory every feature and flag gaps vs FRB2. Produce a checklist of what's ported, what's intentionally dropped, and what's still missing. Likely surfaces: climbing/ladders, moving-platform `groundHorizontalVelocity`, `IsUsingCustomDeceleration`, `MaxClimbingSpeed`, CSV-driven values. (Note: AnimationController is intentionally not ported — see the "Animation — intentionally not engine-managed" note above.)

## Implement `OneWayDirection` Down / Left / Right
**Priority: Eventual** — Currently only `None` and `Up` are implemented; the other three throw `NotImplementedException`. `Down` supports ceiling-only / uppercut-style barriers; `Left`/`Right` support Yoshi's-Island-style one-way doors.

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

