# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Tiled Collision Objects — Non-goals

Phases 1 (polygon tiles), 2 (sub-cell `<object>` rectangles, flip flags), and sub-cell rect adjacency (rect↔rect, rect↔full-cell, rect↔polygon) are complete. `SlopesSample` demonstrates all of it end-to-end. Remaining out-of-scope items:

- `TilemapEllipseObject` stays out of scope: FRB2 has `Circle` with uniform radius only, and Tiled ellipses allow `rx != ry`; no realistic tile-collision use case justifies the approximation work.

## Platformer Config JSON (Animations + Coefficients, Bundled)
**Priority: Soon** — Port FRB1's platformer animation layer AND externalize `PlatformerValues` in the same unified JSON file per entity. These were two separate TODOs; they're now bundled because they share a file, a loader, and the content-boundary motivation.

> **In-flight design notes:** `design/platformer-config-design.md` — settled decisions, open questions, and implementation order. Read before picking this up.

### Animation layer
Port FRB1's platformer animation layer so platformer entities can automatically switch animations based on behavior state (idle/walk/run/jump/fall/land/duck/climb/etc.) and facing direction. FRB1 has this wired through the "AnimationController" plugin / `PlatformerAnimationController` with per-state animation names and left/right variants.

- Map `PlatformerBehavior` states to animation chain names via conditions (e.g., `IsOnGround && VelocityX != 0`)
- Facing: append `leftSuffix`/`rightSuffix` (default `"Left"`/`"Right"` to match FRB1 editor output) to chain names; animation frames carry `FlipHorizontal`
- Support for user-defined states beyond the standard set (double-jump, wall-slide, etc.)
- Hook into `PlayAnimation` so transitions don't restart a chain that's already playing

### Coefficients layer (was "JSON-Driven PlatformerValues") — **Phase 1 landed**
Externalize `PlatformerValues` into the config file. Canonical application of the `content-boundary` philosophy.

> **Status:** `PlatformerConfig.FromJson` / `ApplyTo` extension landed. SlopesSample and AutoEvalCoinHopperSample converted. Template at `.claude/templates/PlatformerConfig/`. Skills and content-boundary updated. Hot-reload is a separate TODO below.

- Fills the fixed slots FRB2 already has: `movement.ground` → `PlatformerBehavior.GroundMovement`, `movement.air` → `AirMovement`, future `movement.afterDoubleJump` → the not-yet-wired double-jump slot. **Not arbitrary user-named profiles** — slot names are fixed and known to the behavior.
- Each movement slot is a nullable-field `PlatformerValues` DTO; omitted fields fall back to struct defaults.
- **Jump config in each slot supports two mutually-exclusive input modes:**
  - Derived: `minJumpHeight` + optional `maxJumpHeight` → calls `PlatformerValues.SetJumpHeights(...)`. Preferred mode.
  - Raw: `JumpVelocity` + `JumpApplyLength` + `JumpApplyByButtonHold` (direct-set escape hatch).
  - Loader errors if both modes are specified for the same slot.
- If a game needs "ice physics" or other movement variations, that's game-code swapping the whole `PlatformerValues` assignment — not an engine-level profile concept.
- ~~Hot-reload support~~ — promoted to its own TODO below.

### Shared schema
All three top-level sections (`suffixes`, `movement`, `animations`) are **optional** — a file can provide any subset. Single loader entry point (e.g., `PlatformerConfig.FromJson(path)`). Cross-reference `content-boundary` skill.

## PlatformerConfig Hot-Reload (File Watch)
**Priority: Soon** — Watch `player.platformer.json` (and any config JSON loaded via `PlatformerConfig.FromJson`) for changes and re-apply values at runtime without recompiling. This is the payoff of the content-boundary split: a designer edits JSON, saves, and sees the result in the running game immediately.

- Use `FileSystemWatcher` (or equivalent) to detect writes to the loaded path.
- On change: re-deserialize and re-apply via `ApplyTo`. Must handle partial/malformed saves gracefully (file may be mid-write) — retry on `IOException`, log parse errors rather than crashing.
- Debounce: editors often write multiple events per save; collapse into a single reload after a short delay (~100-200ms).
- API surface: something like `PlatformerConfig.WatchAndApply(path, behavior)` that returns a disposable handle, or a `FileWatchingConfig` wrapper. Exact shape TBD.
- Consider generalizing beyond `PlatformerConfig` — any JSON config file could benefit from watch+reload. A generic `ConfigWatcher<T>` might be the right abstraction, with `PlatformerConfig` as the first consumer.
- Thread safety: `FileSystemWatcher` fires on a threadpool thread; values must be applied on the game thread. Queue the reload and process it during the next `Update` tick.

## Designer-Placed Spawn Markers — API Decision Needed
**Priority: Soon** — **Discussion item, not yet a settled implementation.** Recurring friction across orchestrator samples: when a game needs designer-placed positions for non-tile things (player spawn, coin/pickup positions, enemy spawn points, trigger zones), the engine offers no path that satisfies the `content-boundary` philosophy. Coders fall back to hardcoded `Vector2[]` arrays in C#, which is the exact anti-pattern that doc warns against — the human now edits code to move a coin.

### What the coder typically wants
A way for the human to place markers in a tool, and for game code to read them at load time as `(name, type, worldX, worldY)` tuples — without recompiling.

### Candidate approaches to discuss
1. **Tiled object layers** (`<objectgroup>`). Tiled already supports `<object>` elements with `name`, `type`/`class`, `x`, `y` inside an `<objectgroup>`. The TMX format is parsed today (we already pull collision objects), so the marshalling work is small. Pro: zero new tooling, designers already have Tiled open. Con: object Y is in tile-pixel space and needs the same world-coord conversion as the rest of TMX; couples spawn data to the level file (may or may not be desired).
2. **Separate JSON sidecar per level** (e.g., `Level1.spawns.json`). Pro: decouples spawn data from level geometry; same loader pattern as the planned platformer config JSON. Con: humans must edit JSON by hand, no visual placement.
3. **Gum-based level editor** for non-tile placement. Almost certainly out of scope but worth naming so we don't quietly drift toward it.
4. **Tiled object layers + a thin wrapper** that exposes them under a stable API (`map.GetSpawns(layerName)`) so we can later swap the underlying source if needed. Best of (1) and (2).

### Open questions
- Should spawn markers live in the same TMX as collision/visuals, or in a sibling file?
- Do we want typed markers (e.g., `class="PlayerSpawn"`) and surface them as a discriminated set, or stay stringly-typed?
- Tiled object Y is top-down pixel-space; the API should hand back world-space `Vector2` already converted (Y+ up, centered on whatever `CenterOn` was called with) — do we want a flag to opt out?
- Tiled `Class` (formerly `Type`) supports custom property schemas — worth supporting for "coin worth 50 pts" / "enemy patrol radius 100" out of the box, or punt to v2?

### Related friction (also worth discussing alongside)
- `TileMap.GetCellWorldPosition(int col, int row)` helper — independent of the spawn-markers question, but the same coder hit both. Currently humans redo `+H/2 - 16*r + 8` on paper to place anything by row/col.

## Platformer Docs Audit (FRB1 → FRB2)
**Priority: Soon** — Manual pass through FRB1's platformer documentation (wiki, plugin README, CSV column names, PlatformerValues fields, predefined profiles, behavior hooks) to inventory every feature and flag gaps vs FRB2. Produce a checklist of what's ported, what's intentionally dropped, and what's still missing. Likely surfaces: climbing/ladders, moving-platform `groundHorizontalVelocity`, `IsUsingCustomDeceleration`, `MaxClimbingSpeed`, animation controller hooks, CSV-driven values.

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

