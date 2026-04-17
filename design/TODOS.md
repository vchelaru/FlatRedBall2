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

## Content Hot-Reload
**Priority: Soon** — General-purpose content hot-reload system. The original scope was PlatformerConfig JSON only, but the real need is broader: PNGs, Tiled maps, JSON configs, and potentially any content file. Gum already supports hot-reload natively.

### Two reload strategies

Content changes fall into two categories:

1. **In-place reload** — the engine patches the existing object without the game knowing. No screen restart, no state loss. Examples:
   - `Texture2D.SetData` for a PNG that hasn't changed dimensions
   - Value-by-value assignment for JSON configs (platformer values, etc.)
   - Tile replacement in a TMX when only tile data changed (no structural changes)

2. **Screen restart** — when in-place isn't possible, the engine restarts the current screen. Examples:
   - PNG changed dimensions → must `new` the `Texture2D`, which invalidates all references
   - TMX structural changes (object layers added/removed, map resized) → entities may have been modified since load (enemy moved, coin collected)
   - Any change where the engine can't determine what's safe to patch

The watcher should prefer in-place when possible and fall back to screen restart otherwise.

### Implementation order

#### 1. `RestartScreen()` — prerequisite, independently valuable

> **Status: Increments 1 & 2 landed.** See Done.md ("Screen Restart" + "Hot-Reload Restart Hooks"). Death/retry, hot-reload mode, and the user-defined `SaveHotReloadState`/`RestoreHotReloadState` hooks are all in. Engine-managed automatic preservation (camera position, tracked entity kinematics) is still open — covered below.

**Engine-managed defaults still open.** The user's Save/Restore hooks cover game-specific state (score, timer, collected items). The engine should also automatically preserve a small set of generic critical variables so most games don't need to write any hooks at all to get a non-jarring hot-reload:
- Camera position (from `CameraControllingEntity` or direct camera state)
- The tracked entity's position, velocity, and acceleration

This is the next increment. Apply BEFORE the user's `RestoreHotReloadState` so user code can override the engine's automatic preservation if it wants.

**Edge case:** Restoring player position after a TMX structural change could place the player inside new geometry. This is acceptable — collision pushes them out on the next frame, which is better than teleporting to spawn.

#### 2. `ContentWatcher` — generic file watch infrastructure

Handles the boring parts that are common across all content types:
- `FileSystemWatcher` (or equivalent) to detect writes
- Debounce: editors fire multiple events per save; collapse into a single reload after ~100-200ms
- Thread safety: `FileSystemWatcher` fires on a threadpool thread; queue the reload and process it during the next `Update` tick on the game thread
- Graceful error handling: retry on `IOException` (file mid-write), log parse errors rather than crashing
- Returns a disposable handle for cleanup

#### 3. JSON hot-reload — first consumer

Simplest case. `ContentWatcher` detects change → deserialize → apply values in-place. No screen restart needed.

```csharp
var watcher = new ContentWatcher("Content/player.platformer.json", () => {
    var config = PlatformerConfig.FromJson("Content/player.platformer.json");
    config.ApplyTo(_player.Platformer);
});
```

#### 4. PNG hot-reload

- Same dimensions: `Texture2D.SetData` in-place. All existing references stay valid.
- Different dimensions: trigger `RestartScreen(hotReload: true)`.
- Requires the engine to track which textures were loaded from which files, or a registry pattern.

#### 5. TMX hot-reload

- Tile-only changes: replace tile data in existing layers, regenerate collision collections.
- Structural changes (layers added/removed, objects changed, map resized): trigger `RestartScreen(hotReload: true)`.
- Determining "tile-only vs structural" may be complex — could start conservative (always restart) and optimize later.

## Designer-Placed Spawn Markers (Landed)

> **Status: Complete.** `TileMap.CreateEntities<T>` with `Origin` enum and reflection-based property mapping. AutoEvalCoinHopperSample converted and runtime-tested. StandardTileset updated with visible entity marker tiles (Coin id 97, PlayerSpawn id 29, plus many others). Skills and template updated.

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

## CI: GitHub Action to Run Unit Tests as a Required Status Check on PRs to `main`
**Priority: Eventual** — Two pieces:
1. Add a `.github/workflows/` YAML action that runs `dotnet test tests/FlatRedBall2.Tests/` on every pull request targeting `main` (and ideally on direct pushes to `main` too). Should fail the check on any test failure or build error.
2. Configure the branch protection rule on `main` to mark this check as a **required status check** so PRs cannot be merged unless it passes.

Catches regressions before merge instead of after. No need immediately, but worth setting up before the contributor base expands.

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT
**Priority: Eventual** — currently targets MonoGame.Framework.DesktopGL only.

- Identify abstraction points for graphics init, fullscreen APIs, input, audio, content pipeline
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

