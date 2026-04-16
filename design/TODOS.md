# FlatRedBall2 — Todo

See `Done.md` for completed items.

## Tiled Collision Objects — Non-goals

Phases 1 (polygon tiles), 2 (sub-cell `<object>` rectangles, flip flags), and sub-cell rect adjacency (rect↔rect, rect↔full-cell, rect↔polygon) are complete. `SlopesSample` demonstrates all of it end-to-end. Remaining out-of-scope items:

- `TilemapEllipseObject` stays out of scope: FRB2 has `Circle` with uniform radius only, and Tiled ellipses allow `rx != ry`; no realistic tile-collision use case justifies the approximation work.

## Platformer Docs Audit (FRB1 → FRB2)
**Priority: Soon** — Manual pass through FRB1's platformer documentation (wiki, plugin README, CSV column names, PlatformerValues fields, predefined profiles, behavior hooks) to inventory every feature and flag gaps vs FRB2. Produce a checklist of what's ported, what's intentionally dropped, and what's still missing. Likely surfaces: climbing/ladders, moving-platform `groundHorizontalVelocity`, `IsUsingCustomDeceleration`, `MaxClimbingSpeed`, animation controller hooks, CSV-driven values.

## Implement `OneWayDirection` Down / Left / Right
**Priority: Eventual** — Currently only `None` and `Up` are implemented; the other three throw `NotImplementedException`. `Down` supports ceiling-only / uppercut-style barriers; `Left`/`Right` support Yoshi's-Island-style one-way doors.

## Polygon Snagging in Top-Down (Standard mode)
**Priority: Eventual** — `TileShapeCollection` in `SlopeCollisionMode.Standard` can still snag at seams between adjacent polygon tiles when used for top-down angled walls.

- `Polygon.SuppressedEdges` bitfield exists but isn't wired into SAT correctly (opposite edges share the same axis, so edge-level suppression during SAT doesn't eliminate the axis)
- Consider a post-process MTV filter analogous to rectangles' `ComputeDirectionalSeparation`, or a different approach entirely
- Tests removed when we deferred this; re-add when addressed

## JSON-Driven PlatformerValues
**Priority: Eventual** — Allow `PlatformerValues` profiles to be defined in JSON files and loaded at runtime, rather than hard-coded in C#. This mirrors FRB1's CSV-driven platformer values workflow.

- Define a JSON schema for `PlatformerValues` (one file = one or more named profiles, e.g. `"Walk"`, `"Run"`, `"Ice"`)
- Add a loader (e.g. `PlatformerValues.FromJson(string path)` or via `ContentManagerService`) that deserializes into the existing struct
- Hot-reload support (watch file, re-deserialize on change) would be a nice-to-have for tuning without recompiling
- Consider whether profiles should live one-per-file or as a named dictionary in a single file

## Aseprite → .achx Conversion Tool
**Priority: Eventual** — Build a CLI tool (or post-build step) that converts Aseprite `.ase`/`.aseprite` files into FRB2's `.achx` animation chain format. FRB1 has prior art here worth borrowing from. Key concerns:

- Parse Aseprite frame tags → `AnimationChain` entries (name, frame list, loop flag)
- Map Aseprite frame durations → per-frame `FrameLength` values
- Export the sprite sheet (or reference existing exported PNG) and wire up UV rects per frame
- Preserve layer/slice metadata if useful for hitbox or attachment-point data
- Decide: standalone CLI tool vs. content pipeline extension vs. both

## Multi-Backend Support (MonoGame / FNA / KNI) and Native AOT
**Priority: Eventual** — currently targets MonoGame.Framework.DesktopGL only.

- Identify abstraction points for graphics init, fullscreen APIs, input, audio, content pipeline
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced
- Flag any new reflection-heavy or AOT-hostile code for future cleanup

