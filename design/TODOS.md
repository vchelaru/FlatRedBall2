# FlatRedBall2 — Todo

**No speculative items.** Every entry must be either (a) ready to work on now or (b) ready to discuss now. "Eventually," "someday," "maybe if it comes up" do not belong here — they're noise that buries real work and never gets revisited. If an idea is interesting but not actionable, let it surface again organically when a real use case appears; don't pre-emptively log it.

Open work only. When an item ships, delete it — don't leave a "landed" breadcrumb. Design decisions and historical context that outlive a TODO belong in skill files, XML docs, or commit messages, not here.

## AnimationFrame Pivot / Origin Support
**Priority: Eventual** — `AdobeAnimateAtlasSave` parses `pivotX`/`pivotY` per-SubTexture but discards them because `AnimationFrame` has no pivot field. Adobe Animate exports use pivots to keep a character's anchor (e.g. feet) stable across frames of different sizes. Overlaps semantically with the existing `RelativeX`/`RelativeY`, so pick one model: either have the Adobe importer convert pivot → `RelativeX/Y` at load time (no new field; sprites already obey RelativeX/Y) or add true per-frame pivot. The conversion path is probably simpler. Revisit when the first real Adobe-Animate-authored entity lands.

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

## Documentation Site
**Priority: Soon** — Stand up a public docs site for FlatRedBall2. Today all guidance lives in skill files (AI-facing, in-repo) and inline XML docs; a human-facing site is the missing third leg.

Open questions:
- **Platform.** DocFX (generates from XML docs + markdown, good C# ecosystem fit), MkDocs Material (prettier, Markdown-only), or Docusaurus (more web-dev-flavored)? DocFX is probably the right default for a .NET library because it reads XML docs directly.
- **Content sourcing.** Large portions of the skill files are high-quality prose that could seed the docs (e.g. "entities and factories," "collision relationships," "content-boundary"). Tension: skills are AI-optimized (terse, bullet-heavy), docs are human-optimized (more narrative, more examples). Do we fork the content, cross-reference, or generate one from the other?
- **Where it's hosted.** GitHub Pages from the repo (free, simple) vs a dedicated domain. Starting with GitHub Pages is the low-friction path.
- **API reference.** Generated from XML docs. DocFX does this well; MkDocs does not natively.
- **Samples.** Link to the `Samples/` directory? Embed runnable examples? WASM-hosted demos once KNI lands?
- **Versioning.** Docs site pinned to the latest preview NuGet vs `main` branch. Probably `main` during preview and switch to per-release after 1.0.

