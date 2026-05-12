# FlatRedBall2 — Todo

**No speculative items.** Every entry must be either (a) ready to work on now or (b) ready to discuss now. "Eventually," "someday," "maybe if it comes up" do not belong here — they're noise that buries real work and never gets revisited. If an idea is interesting but not actionable, let it surface again organically when a real use case appears; don't pre-emptively log it.

Open work only. When an item ships, delete it — don't leave a "landed" breadcrumb. Design decisions and historical context that outlive a TODO belong in skill files, XML docs, or commit messages, not here.

## Font rendering quality on web at non-native resolutions

Gum renders text from pre-baked bitmap font (`.fnt`) atlases sized for a fixed design resolution. On the BlazorGL/KNI WASM target the browser viewport changes constantly — user resizes, devicePixelRatio differences, mobile rotations — so the atlas gets sampled at fractional scales and text turns blurry / aliased. Solitaire is the obvious repro: at most browser sizes the card-rank/suit labels and any HUD text look noticeably worse than on desktop.

Open questions before designing a fix: (a) bake atlases at multiple resolutions and pick the closest, (b) switch web text to SDF / MSDF fonts (one atlas, scales cleanly — but requires a different shader and Gum-side support), (c) re-rasterize atlases on the fly when the canvas size changes (cheap to author, costly per resize), or (d) switch to vector text rendering on web only (Skia/HarfBuzz path, much heavier dependency). Need to confirm whether the right layer for the fix is FRB2, Gum, or the runtime that ships the `.fnt` consumer — and whether desktop has the same problem at high-DPI displays even though it's less obvious.

## Render-state thrashing when Apos.Shapes, fonts, and sprites interleave

Each of the three pipelines wants its own GPU state — `Apos.Shapes` (the shapes library used by `ShapesBatch`) sets its own effect/buffers, bitmap-font text goes through `SpriteBatch`, sprite rendering goes through `SpriteBatch` (or batched variants) — and every transition between them is a flush + state swap. A frame that draws shapes → text → sprite → shapes → text triggers multiple flushes per layer, and the cost shows up as draw-call count and visible CPU time on the WASM target especially. Need to (a) measure where the actual cost lands (flush count vs. effect rebind vs. vertex-buffer churn) on a representative scene, (b) decide whether the fix is reordering within a layer (group by primitive type when z-order allows), unifying the shape/sprite/text path through one batcher, or batching across layers when the engine knows two adjacent layers can share state, and (c) confirm the win on web is meaningfully larger than on desktop, since that's where the problem bites first.

## Touch input on mobile web — clicks/pushes don't fire

Solitaire on a mobile browser shows the cards but tapping them does nothing — drags and pushes never register. `Cursor.UpdateTouch` already calls `TouchPanel.GetState()` and updates `_touchScreenPos` / `_touchActive` while a touch is held, but it never maps touch state transitions onto the primary-click pathway that gameplay code (and Gum Forms) listens on. So the cursor *position* tracks the finger, but `PrimaryClick` / `PrimaryPush` / `PrimaryRelease` stay false the whole time and nothing reacts to taps.

Likely fix: walk the `TouchCollection` for `TouchLocationState.Pressed` and `Released` and translate those into the same primary-button down/up events the mouse path emits, so click/push detection works without every gameplay system having to special-case touch. Open question — multitouch: do we report only the first finger as the primary cursor (current behavior, simplest) or expose secondary touches for two-finger gestures down the road? Solitaire only needs single-touch; defer multitouch unless a sample needs it.

Repro is mobile Solitaire; confirm the fix on at least one Android Chrome and one iOS Safari session since touch event quirks differ between them.

## Web load times for large Gum projects

Initial load is slow on the BlazorGL/KNI target when a `.gumx` references many components — fonts, control templates, generated runtime types all compile/initialize on the main thread before the first frame draws. Need to (a) measure where the time actually goes (Gum project load vs. runtime registration via `RegisterRuntimeType` module initializers vs. asset decode), (b) decide whether the fix is engine-side (lazy/deferred runtime registration, parallel asset decode) or Gum-side (incremental project load), and (c) confirm WASM-specific costs separate from the same load on desktop. Solitaire's `.gumx` is the readily-available large project to profile against.

## Multiple Gum screens and transitions

Today a screen calls `this.Add(new GameScreenGum())` once in `CustomInitialize` and that visual lives until the FRB Screen tears down. There's no first-class story for (a) hosting multiple Gum screens within one FRB Screen with a current/next swap, or (b) cross-fading / sliding between them. Game code can hand-roll this with two `FrameworkElement`s and a tween on alpha/position, but the absence of an idiomatic pattern means every consumer reinvents it. Decide: is this a sample-level recipe (document the tween-between-two-roots pattern), a Gum-level Forms feature (a `ScreenHost` control), or an FRB-level helper (`Screen.PushGumScreen` / `PopGumScreen` with a built-in transition arg)?

## SVG and Lottie support in Gum

Gum currently ships with raster-only visuals (`Sprite`, `NineSlice`, `ColoredRectangle`). Vector / animated-vector formats — SVG for static UI art, Lottie for animated UI (the After Effects → Bodymovin export pipeline) — would close a real gap for UI-heavy games. Open questions: Does this belong in Gum (new runtime types alongside `Sprite`) or in FRB2 (an `IRenderable` wrapper that hosts an SVG/Lottie renderer)? Which third-party renderer? — `Svg.Skia` and `SkiaSharp.Skottie` are the obvious starting points but pull SkiaSharp into the dependency graph, which has its own desktop/WASM size implications. Confirm WASM viability before committing.

## Rive support (lower priority than Lottie)

Rive is the interactive-animation alternative to Lottie — richer state-machine model, but a heavier runtime and a paid editor. Track this only as the secondary option after Lottie lands; if Lottie covers the actual UI animation use cases the engine sees, Rive may never be worth the integration cost. Decision criteria: does any sample or downstream user actually need Rive's interactive features (state machines triggered from game code), or are pre-rendered animation clips sufficient?

## Clean up build warnings to zero

Every recent build of Solitaire reports the same three warnings, and the engine project has more under nullability / missing-XML-doc rules. Currently these are background noise — but they hide *real* warnings that get introduced, and the eventual goal (per `src/FlatRedBall2.csproj`'s comment on `GenerateDocumentationFile`: "Promote to error before the first stable (non-preview) NuGet ships") is zero warnings on a clean build.

Known categories:
- **`CS1591` — missing XML doc on public members.** Engine-side. Currently surfaces on `GumRenderable.ToString()` and likely others. Fix: write the docs (most are obvious from the member name) or, where the type is genuinely an internal-leak that shouldn't be public, narrow the visibility.
- **`CS0108` / `CS0114` — generated Gum control classes hide inherited members** (`MenuItem.MenuItemCategoryState`, `PasswordBox.Placeholder`, others). These come out of Gum's codegen; fix is upstream in Gum (emit `new` / `override` keyword) or a codegen-template tweak the consumer can apply. Track separately from engine work.
- **Nullability warnings inside `tests/`.** The two `EntityVector2TweenTests`/`EntityColorTweenTests` `CS8600`/`CS8625` patterns in test code that aren't covered by `Nullable=enable`. Easy mechanical fix.
- **Gum source warnings** (when project-referenced, not packaged). Out of scope here — they only appear during in-place Gum debugging and aren't shipped.

Do this as a sweep, not piecemeal: a single PR that drives the engine and sample warning count to zero, then promotes warnings to errors in CI so regressions are caught.

## 3D transformations on entities (and Gum visuals attached to them)

We have no story for 3D effects — card flip, page turn, screen fold-out, perspective tilt — on either sprites or entity-attached Gum hierarchies. Today an entity has `Rotation` (Z-axis only) and `Position`; nothing exposes the full 3D world matrix needed for a perspective quad.

The shape of a fix is roughly known: render the entity's 2D contents into a `RenderTarget2D`, then draw that texture as a textured quad whose four corners are computed from a 3D world matrix (translate / rotate-X / rotate-Y / perspective-project). For sprites, capturing into a render target is overkill — you can just push 3D-transformed vertices through `BasicEffect` directly. But for Gum: Gum's pipeline draws an entire hierarchy of nested visuals through `GumBatch`, and there's no clean way to intercept that as 3D vertices without going through a render target.

Open questions before designing:
1. **API surface.** Add `Entity.Rotation3D` (a `Quaternion` or Euler triple)? Or a separate `Effect` / `Pose3D` wrapper component that opts an entity in, since most entities never need it and shouldn't pay the matrix cost?
2. **Render-target lifecycle.** Per-entity `RenderTarget2D` is too expensive to allocate naively (Solitaire would want 52 of them). Pool them? Allocate only on entities that opt in? Reuse a single shared RT and draw into it once per 3D-transformed entity per frame?
3. **Cursor hit-testing through 3D.** Gum's Forms input runs in 2D canvas space. A button on a card that's tilted 30° on the X-axis still needs to be clickable. That means inverse-projecting the cursor back through the entity's 3D matrix into canvas space before dispatch. Gum's `Cursor.TransformMatrix` is a 2D affine matrix — confirm whether it handles a perspective inverse correctly, or whether the engine needs to do the un-project itself before handing coords to Gum.
4. **Z-sorting and depth.** A 3D-transformed entity has parts at different screen-Z. Does the existing 2D render-list Z sort still work, or does the depth buffer need to come on for these passes? If yes, what does that mean for the rest of the 2D pipeline that currently assumes no depth test?

This is a meaningful chunk of engine work — likely starts with the simpler sprite-only path (no render target, just 3D vertices through `BasicEffect`) to validate the API shape, then layers in the render-target path for Gum hierarchies once cursor hit-testing under 3D is settled.

## Figma → Gum import path

Designers want to author UI in Figma and have it appear in Gum without manual recreation. No existing converter — the open question is whether to (a) write a Figma plugin that emits `.gucx`/`.gusx` directly, (b) export Figma as SVG and rely on an SVG-import path in Gum (which depends on the SVG/Lottie work above), or (c) export Figma to Lottie and lean on the Lottie path. Each has tradeoffs in fidelity (what Figma features survive the round-trip), maintenance burden (the plugin path requires keeping up with Figma's API), and ordering (Lottie/SVG support unblocks paths b and c). Decide the strategy before estimating effort.
