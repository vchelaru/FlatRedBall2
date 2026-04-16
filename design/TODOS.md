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

### Coefficients layer (was "JSON-Driven PlatformerValues")
Externalize `PlatformerValues` into the config file. Canonical application of the `content-boundary` philosophy.

- Fills the fixed slots FRB2 already has: `movement.ground` → `PlatformerBehavior.GroundMovement`, `movement.air` → `AirMovement`, future `movement.afterDoubleJump` → the not-yet-wired double-jump slot. **Not arbitrary user-named profiles** — slot names are fixed and known to the behavior.
- Each movement slot is a nullable-field `PlatformerValues` DTO; omitted fields fall back to struct defaults.
- **Jump config in each slot supports two mutually-exclusive input modes:**
  - Derived: `minJumpHeight` + optional `maxJumpHeight` → calls `PlatformerValues.SetJumpHeights(...)`. Preferred mode.
  - Raw: `JumpVelocity` + `JumpApplyLength` + `JumpApplyByButtonHold` (direct-set escape hatch).
  - Loader errors if both modes are specified for the same slot.
- If a game needs "ice physics" or other movement variations, that's game-code swapping the whole `PlatformerValues` assignment — not an engine-level profile concept.
- Hot-reload support (watch file, re-deserialize on change) is a nice-to-have for tuning without recompiling.

### Shared schema
All three top-level sections (`suffixes`, `movement`, `animations`) are **optional** — a file can provide any subset. Single loader entry point (e.g., `PlatformerConfig.FromJson(path)`). Cross-reference `content-boundary` skill.

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

