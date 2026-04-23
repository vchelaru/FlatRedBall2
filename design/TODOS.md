# FlatRedBall2 — Todo

**No speculative items.** Every entry must be either (a) ready to work on now or (b) ready to discuss now. "Eventually," "someday," "maybe if it comes up" do not belong here — they're noise that buries real work and never gets revisited. If an idea is interesting but not actionable, let it surface again organically when a real use case appears; don't pre-emptively log it.

Open work only. When an item ships, delete it — don't leave a "landed" breadcrumb. Design decisions and historical context that outlive a TODO belong in skill files, XML docs, or commit messages, not here.

## Engine-Wide XML Docs Pass
**Priority: Soon — gates NuGet release polish.** Sweep every public type and member in `src/` and bring XML docs up to the project's bar: succinct, adds clarification beyond the name, avoids redundancy, calls out gotchas. Many APIs were added incrementally and either lack docs or have stale ones. This matters extra for the NuGet release — once the package ships, XML docs are what IntelliSense surfaces to consumers who never read the skill files. Coordinate with the docs site TODO: if we go DocFX, the XML docs become the API reference verbatim, so quality here is doubly load-bearing. Approach: probably one subsystem at a time (Collision, Rendering, Input, Screens, Entities, Tweening, etc.) so review diffs stay digestible.

## KNI BlazorGL — Explicit Resolution Path
**Priority: Eventual — verify before first browser-targeted game.** `ActivateScreen` skips `ApplyWindowSettings` when `Window.AllowUserResizing == true` AND the screen has no `PreferredDisplaySettings` (added 2026-04-23 to fix cursor offset on KNI BlazorGL — the engine was clamping the back buffer to its design resolution while the browser canvas stayed at 100vw/100vh, producing a coordinate-space mismatch in `Camera.ScreenToWorld`). The no-`PreferredDisplaySettings` branch is exercised by `samples/auto/AutoEvalKniBlazorSample`; the **explicit-resolution branch** on KNI is not. If a screen sets `PreferredDisplaySettings` with a fixed `PreferredWindowWidth/Height`, `ApplyWindowSettings` still runs and resizes the back buffer — same mismatch as before, just now opt-in. Open questions: should KNI honor the request and force the canvas DOM to match (CSS or JS interop)? Letterbox the design resolution inside the larger canvas? Ignore the request entirely and warn? Decide when the first KNI game actually needs a fixed resolution.

## "Fire and Forget" Entities
**Priority: Discussion — not starting yet.** Concept placeholder. Short-lived entities spawned purely for visual effect — particles, hit sparks, dust puffs, explosion debris, floating damage numbers, muzzle flashes — that the spawner doesn't want a reference to and doesn't need to query. They exist, play out their animation/tween/lifetime, and self-destruct. Goal: make this pattern ergonomic so gameplay code can say "spawn a poof here" in one line without hand-rolling lifetime bookkeeping every time, and without polluting factories/collision relationships meant for gameplay-relevant entities.

Open questions for the future discussion:
- Dedicated base class / interface vs. just a convention on top of existing `Entity` + `Timing`?
- Pooling story (particles are the canonical case where allocation churn matters)?
- Should these participate in collision at all, or be pure visual?
- Relationship to a future particle system vs. one-off effect entities — same abstraction or different?
- Spawn API shape: extension on `Screen`/`Entity`? A `FireAndForgetFactory<T>`? A helper like `Effects.Spawn<T>(x, y)`?

## Tween from Mid-Curve ("Pulse/Bump" from Rest)
**Priority: Eventual** — Use case: a circle sits at its resting radius, gets poked, and should "bump" — grow past rest and settle back via elastic-out — without first snapping to a smaller value. Today an elastic-out tween from `rest → rest+10` starts at `rest` and overshoots *above* `rest+10`, not below it. What the user wants is the *tail half* of an elastic curve: as if the animation had already played the wind-up and is catching the second half of the oscillation. Conceptually this is "start a tween at t=0.5 (or some other phase) of its curve," with the visible value beginning exactly at the current rest value. Surfaced by `AutoEvalCollisionEnterExitSample` 2026-04-22 while designing the damage-tile pulse reaction.

Open questions:
- Do we expose a `startPhase` / `startT` parameter on `Entity.Tween` / `Screen.Tween` (0..1, default 0) that samples the easing curve starting at that offset? Internally the tween would still run full-duration but offset its `t` by `startPhase`.
- Alternative: a dedicated `Pulse` / `Bump` helper that takes `(from, peak, backTo, duration, curve)` and composes two tweens (linear-out to peak, elastic-out back) — less general but more discoverable for the common case.
- Which curves does "start mid-phase" even make sense for? Elastic and bounce have well-defined pre-settle oscillation; ease-in-out mid-phase is just a different ease-out. Maybe the parameter only applies to oscillating curves.
- Does this compose with the existing tween-stacking semantics (last-started wins the property setter)?

## AnimationFrame Pivot / Origin Support
**Priority: Eventual** — `AdobeAnimateAtlasSave` parses `pivotX`/`pivotY` per-SubTexture but discards them because `AnimationFrame` has no pivot field. Adobe Animate exports use pivots to keep a character's anchor (e.g. feet) stable across frames of different sizes. Overlaps semantically with the existing `RelativeX`/`RelativeY`, so pick one model: either have the Adobe importer convert pivot → `RelativeX/Y` at load time (no new field; sprites already obey RelativeX/Y) or add true per-frame pivot. The conversion path is probably simpler. Revisit when the first real Adobe-Animate-authored entity lands.

## HSV → RGB Color Helper
**Priority: Eventual** — Every juice-heavy sample (tween popups, particle flashes, randomized enemies) needs vivid varied colors. MonoGame's `Color` is RGBA-only. We keep inlining a ~15-line HsvToRgb in each sample. Candidate: `Color.FromHsv(h, s, v)` (or extension in `FlatRedBall2.Rendering.ColorExtensions`). Surfaced by `AutoEvalTweeningSample` 2026-04-21. Low effort, high reuse.

## Pre-Init vs Reactive-Property Tension
**Priority: Design discussion** — The `entities-and-factories` skill mandates reactive properties (no configure-then-init). But many init-only fields (spawn color, variant index, starting size) have no meaningful reactive behavior — the property setter after spawn is a no-op unless the author wires it up to whatever the value was used to construct. `AutoEvalTweeningSample` 2026-04-21 hit this: `Pop.FillColor` was used inside `CustomInitialize` to build a `Circle`, and the spawn call site ran `Create()` *before* assigning `FillColor`, so every circle came out white. The fix (reactive property that updates the already-created Circle) works, but the footgun is that *not* writing the reactive version silently fails — no compile error, no runtime exception, just wrong output.

Possible directions:
- **Factory.Create overload** that takes an `Action<T>` configure callback, invoked before `CustomInitialize`. Entity reads init-only data inside `CustomInitialize` with guaranteed-set values. `_popFactory.Create(p => p.FillColor = color)`.
- **Lifecycle hook for "after spawn parameters set":** something like `CustomInitializeAfterAssignment` or a two-phase init. Heavier.
- **Status quo + better skill guidance:** push harder on the reactive rule, call out the "silent wrong output" failure mode.

Decision needed before picking. The Factory.Create overload feels cleanest but adds surface area.

## Tweening — Vector2 / Color Helpers
**Priority: Eventual** — Add `Tween` overloads for `Vector2` and `Color` once real sample code shows the two-tweener-per-value pattern getting verbose. Today users compose two float tweens for a position/color change; not painful enough to justify the API surface yet.

## `TileMap.GetCellWorldPosition(int col, int row)` Helper
**Priority: Eventual** — Gameplay code that wants "where in world space is tile (col, row)?" currently repeats the arithmetic (tile size × index + map origin, Y-flip for Y+ up). A single helper on `TileMap` collapses every call site.


## Climbing slot — fall back to AirMovement.JumpVelocity when ClimbingMovement.JumpVelocity == 0?
**Priority: Eventual — wait for use cases.** Today, `ClimbingMovement.JumpVelocity` is a hard `float` defaulting to 0; "field omitted" is indistinguishable from "explicitly 0" once parsed. A user who authors a `climbing` slot without `JumpVelocity` gets a jump-off that drops the player straight down — the kind of "silent wrong output" failure mode the project explicitly calls out as anti-pattern (see Pre-Init vs Reactive-Property Tension). Documented in the `platformer-movement` skill and JSON template as "AUTHOR THIS." Possible future fix: when `ClimbingMovement.JumpVelocity == 0`, fall back to `AirMovement.JumpVelocity` so the obvious-default feel ("press jump → leave ladder with the same hop as in air") is automatic. Tradeoff: a user who genuinely wants a 0-velocity drop-off has to pick a tiny non-zero value or use a different escape hatch. Defer until real games hit the footgun.

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
- AOT blockers: reflection-based code (`Activator.CreateInstance`, `MakeGenericMethod`, etc.) must be replaced — note that `TileMap.CreateEntities` uses reflection to map Tiled custom properties; will need a source-generator or explicit-mapping path
- Flag any new reflection-heavy or AOT-hostile code for future cleanup
- **KNI / web (Blazor WASM) target.** KNI is the primary motivator here — it's the backend that unlocks running FRB2 games in the browser. Web deployment is a major distribution story for 2D games (itch.io, jam submissions, embeddable demos) and should be treated as a first-class target alongside desktop once the abstraction layer exists. Open questions: content pipeline story for WASM (mgcb output vs runtime loading), input differences (no gamepad polling guarantees, touch), audio latency, and how hot-reload interacts with a browser-hosted runtime.

## First Preview NuGet Release
**Priority: Soon — target version 0.1.0.** Ship FlatRedBall2 as a preview NuGet package so external users can consume the engine without cloning the repo. Today everyone building on FRB2 must reference the `.csproj` directly.

**Packaging shape — DECIDED: two packages.** Ship `FlatRedBall2.MonoGame` and `FlatRedBall2.Kni` as separate packages from day one. A single multi-TFM package would work today only because the backends happen to live on different TFMs (`net8.0` KNI, `net10.0` MonoGame), and that distinction collapses the moment KNI supports net10.0 — at which point single-package consumers face a forced reshaping (package ID rename or consumption-model change). Two packages give the same story forever: no migration, no breaking change for early adopters when KNI catches up. The cost (one CI matrix, two `dotnet pack` invocations) is small.

Open questions to resolve before publishing:
- **Package IDs.** `FlatRedBall2.MonoGame` and `FlatRedBall2.Kni`. Name `FlatRedBall2` is locked in (decided previously — keep continuity with FRB1 brand, accept the `2` suffix).
- **Version.** Start at `0.1.0` (per user). Decide whether to tag as `0.1.0` or `0.1.0-preview.1` — the `-preview.N` suffix signals "expect breaking changes" via NuGet's pre-release flag and keeps the package off default search until stable.
- **What ships in the package.** Main `FlatRedBall2.dll`. Do Gum-integration, TMX loading, and other optional pieces ship as sub-packages (`FlatRedBall2.Gum`, `FlatRedBall2.Tiled`) or all-in-one? Current project is monolithic — splitting is a bigger refactor and probably post-0.1.
- **Content pipeline assets.** Does the NuGet include the StandardTileset PNG/TSX? A `.targets` file that wires MGCB references? Consumers will hit "where does the tileset live" friction immediately otherwise. Likely needs a `build/FlatRedBall2.targets` shipped in the package.
- **XML docs in the package.** Set `<GenerateDocumentationFile>true</GenerateDocumentationFile>` so the `.xml` ships alongside the `.dll` and IntelliSense lights up in consumer IDEs. Gates on the engine-wide XML docs pass landing first.
- **Symbols / source link.** Enable `PublishRepositoryUrl` + `EmbedUntrackedSources` + `IncludeSymbols` + `SymbolPackageFormat=snupkg` so consumers can step into engine code while debugging.
- **README.md in the package.** Set `<PackageReadmeFile>` — nuget.org renders it. Minimal "getting started" pointing to the docs site.
- **License + metadata.** Confirm `LICENSE` is referenced via `<PackageLicenseFile>`, plus `<Authors>`, `<Description>`, `<RepositoryUrl>`, `<PackageTags>`, `<PackageProjectUrl>`.
- **CI publish pipeline (this is the YAML part).** GitHub Actions workflow under `.github/workflows/publish.yml` on tag push (`v*`) → `dotnet pack -c Release` → `dotnet nuget push *.nupkg --api-key $NUGET_API_KEY`. Needs `NUGET_API_KEY` secret in repo settings.

## Documentation Site
**Priority: Soon** — Stand up a public docs site for FlatRedBall2. Today all guidance lives in skill files (AI-facing, in-repo) and inline XML docs; a human-facing site is the missing third leg.

Open questions:
- **Platform.** DocFX (generates from XML docs + markdown, good C# ecosystem fit), MkDocs Material (prettier, Markdown-only), or Docusaurus (more web-dev-flavored)? DocFX is probably the right default for a .NET library because it reads XML docs directly.
- **Content sourcing.** Large portions of the skill files are high-quality prose that could seed the docs (e.g. "entities and factories," "collision relationships," "content-boundary"). Tension: skills are AI-optimized (terse, bullet-heavy), docs are human-optimized (more narrative, more examples). Do we fork the content, cross-reference, or generate one from the other?
- **Where it's hosted.** GitHub Pages from the repo (free, simple) vs a dedicated domain. Starting with GitHub Pages is the low-friction path.
- **API reference.** Generated from XML docs. DocFX does this well; MkDocs does not natively.
- **Samples.** Link to the `Samples/` directory? Embed runnable examples? WASM-hosted demos once KNI lands?
- **Versioning.** Docs site pinned to the latest preview NuGet vs `main` branch. Probably `main` during preview and switch to per-release after 1.0.

