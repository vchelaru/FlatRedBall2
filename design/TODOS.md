# FlatRedBall2 — Todo

## HSV → RGB Color Helper
**Priority: Eventual** — Every juice-heavy sample (tween popups, particle flashes, randomized enemies) needs vivid varied colors. MonoGame's `Color` is RGBA-only. We keep inlining a ~15-line HsvToRgb in each sample. Candidate: `Color.FromHsv(h, s, v)` (or extension in `FlatRedBall2.Rendering.ColorExtensions`). Surfaced by `AutoEvalTweeningSample` 2026-04-21. Low effort, high reuse.

## Pre-Init vs Reactive-Property Tension
**Priority: Design discussion** — The `entities-and-factories` skill mandates reactive properties (no configure-then-init). But many init-only fields (spawn color, variant index, starting size) have no meaningful reactive behavior — the property setter after spawn is a no-op unless the author wires it up to whatever the value was used to construct. `AutoEvalTweeningSample` 2026-04-21 hit this: `Pop.FillColor` was used inside `CustomInitialize` to build a `Circle`, and the spawn call site ran `Create()` *before* assigning `FillColor`, so every circle came out white. The fix (reactive property that updates the already-created Circle) works, but the footgun is that *not* writing the reactive version silently fails — no compile error, no runtime exception, just wrong output.

Possible directions:
- **Factory.Create overload** that takes an `Action<T>` configure callback, invoked before `CustomInitialize`. Entity reads init-only data inside `CustomInitialize` with guaranteed-set values. `_popFactory.Create(p => p.FillColor = color)`.
- **Lifecycle hook for "after spawn parameters set":** something like `CustomInitializeAfterAssignment` or a two-phase init. Heavier.
- **Status quo + better skill guidance:** push harder on the reactive rule, call out the "silent wrong output" failure mode.

Decision needed before picking. The Factory.Create overload feels cleanest but adds surface area.

## Tweening / Interpolation
**Status: v1 landed.** `FlatRedBall.InterpolationCore` NuGet wired; `FlatRedBall2.Tweening` namespace ships `Entity.Tween(...)` (primary, dies with entity) and `Screen.Tween(...)` (secondary). Float-only. Pause-aware via the existing `Screen.IsPaused` branch plus a finer-grained `ShouldAdvanceTweens` override on both Entity and Screen. 10 tests covering lifecycle, completion, destroy-cleanup, concurrent tweens, Stop(), and the pause hook.

Deferred:
- **Vector2 / Color helpers.** Add once real sample code shows the two-tweener pattern getting verbose.

Rejected:
- **FRB2-owned `InterpolationType` / `Easing` / `Tween` wrappers.** A wrapper-enum version was committed and reverted. Gum already pulls `FlatRedBall.InterpolationCore` transitively via `FlatRedBall.GumCommon`, so the `FlatRedBall.Glue.StateInterpolation` namespace is present in every FRB2 project whether tweens are used or not. FRB2 wrappers with the same names (even in a different namespace) create IntelliSense auto-import ambiguity — the wrong import silently compiles with the wrong types. One source of truth, even with an ugly namespace, beats two sets of near-identical types. Call sites accept the two-`using` cost. Renames (`TweenCurve`/`Ease`) were considered and also rejected as unnecessary churn.

Long-term idea (not planned):
- **Extract a shared `FlatRedBall2.Tweening` library that Gum and FRB2 both consume.** Would let us own the type names (cleaner than `FlatRedBall.Glue.StateInterpolation`) via a single deliberate cross-project migration instead of coexisting wrappers. Only worth it if there's an independent reason to restructure the Gum/InterpolationCore dependency.

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

> **Status: Increments 1 & 2 landed.** Death/retry, hot-reload mode, and user `Save`/`RestoreHotReloadState` hooks are in. Engine-managed automatic preservation is open — see below.

**Engine-managed automatic preservation — open, deferred indefinitely until a real pattern emerges.** The original TODO described automatic preservation of camera position and tracked-entity kinematics so most games would get non-jarring hot-reload "for free." A naive `Camera.X/Y/Zoom` preservation was tried and reverted: in any game using `CameraControllingEntity` (the common case), that entity slams `Camera.X/Y` to the player position on the first frame after restart, clobbering any preserved value. The actually-useful preservation is **player position** — once the player is back where they were, `CameraControllingEntity` follows on frame 1 and the camera lands correctly automatically. But the engine doesn't know which entity is "the player." Future ideas worth exploring if friction warrants:
- **Tagged entities for preservation.** Entities (or entity types) opt in via attribute or interface (e.g. `IHotReloadPreserved`); engine auto-saves their `X/Y/VelocityX/VelocityY/AccelerationX/AccelerationY`. Solves the identification problem with a small annotation cost.
- **`Player` / `CameraControllingEntity`-specific opt-in.** Same idea but only for these two well-known concepts.

Until then, the user `Save`/`RestoreHotReloadState` hooks handle this cleanly: write `state.Set("playerX", _player.X)` and the matching restore. CameraControllingEntity follows on frame 1 → camera lands at the right place. Documenting this manual pattern as the canonical hot-reload recipe in the `screens` skill is the current state of the art.

**Edge case:** Restoring player position after a TMX structural change could place the player inside new geometry. This is acceptable — collision pushes them out on the next frame, which is better than teleporting to spawn.

#### 2. `ContentWatcher` — generic file watch infrastructure

> **Status: Landed.** `Screen.WatchContent(sourcePath, onChanged, destinationPath?)` and `Screen.WatchContentDirectory(sourceDir, onChanged, destinationDir?)` both in. Auto source-root detection via csproj walk-up, copy-on-change, global debouncing, shipping-build no-op. `content-hot-reload` skill rewritten around directory watching.

#### 2b. Allowlist for newly-added content files
**Priority: Eventual** — Today the hot-reload watcher only fires for files that already exist in the build output (filters editor temp files). Side effect: brand-new content files require one rebuild before they're picked up. That's fine for one-off additions, but workflows like "add PNGs to a Gum project," "drop a new font file in," or "add an animation chain frame" suffer — the user wants the new file to flow into the running game without rebuilding.

Possible direction: extension-based allowlist. If a watched directory is registered with an allowlist (e.g. `[".png", ".ttf", ".gumx"]`), the engine treats matching new files as additions to track — copies to dest and invokes the callback even when dest doesn't exist yet. Other extensions still require the dest-exists check.

```csharp
WatchContentDirectory("Content", relPath => ..., newFileExtensions: [".png", ".ttf"]);
```

Open questions when we get there: should the engine update the .csproj `<Content Include>` list too, or assume the user's MSBuild rules already include the new file by glob? How does this interact with Gum's own hot-reload pipeline (Gum already supports hot-reload natively — may not need engine help inside `.gumx` projects)?

#### 3. JSON hot-reload — first consumer

Simplest case. `ContentWatcher` detects change → deserialize → apply values in-place. No screen restart needed.

```csharp
var watcher = new ContentWatcher("Content/player.platformer.json", () => {
    var config = PlatformerConfig.FromJson("Content/player.platformer.json");
    config.ApplyTo(_player.Platformer);
});
```

#### 4. PNG hot-reload

> **Status: Landed.** `Engine.Content.Load<Texture2D>(path)` now routes on extension — path-with-extension loads via `Texture2D.FromFile` and registers for reload; bare names still go through the xnb pipeline. `Engine.Content.TryReload(path)` applies same-dimension changes via `SetData`; dimension mismatch returns `false` so the caller restarts. `AnimationChainListSave.ToAnimationChainList` now takes only `ContentManagerService` and routes frame textures through the same unified path. AutoEvalCoinHopperSample wired with a floating Bear.png sprite for end-to-end validation.

#### 5. TMX hot-reload — in-place tile data updates

> **Status: Landed.** `TileMap.TryReloadFrom(path)` applies tile-data changes in place; structural changes return `false` for a fallback restart. AutoEvalCoinHopperSample wired and end-to-end verified.

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

