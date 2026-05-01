# FlatRedBall2 — Todo

**No speculative items.** Every entry must be either (a) ready to work on now or (b) ready to discuss now. "Eventually," "someday," "maybe if it comes up" do not belong here — they're noise that buries real work and never gets revisited. If an idea is interesting but not actionable, let it surface again organically when a real use case appears; don't pre-emptively log it.

Open work only. When an item ships, delete it — don't leave a "landed" breadcrumb. Design decisions and historical context that outlive a TODO belong in skill files, XML docs, or commit messages, not here.

## Mouse / cursor injection in automation mode

`AutomationMode.ProcessInputCommand` handles `"key"`, `"gamepad"` (button), and `"axis"` (gamepad), but not the cursor — there's no `InjectCursor` on `InputManager` or pathway through `Cursor` to override `WorldPosition`/`PrimaryDown`/`PrimaryPressed`. Mouse-driven samples (e.g. Solitaire) can't be automation-tested without it. Add a synthetic-cursor input source mirroring the keyboard/gamepad pattern, plus an NDJSON command — likely `{"cmd":"input","type":"cursor","x":...,"y":...,"primary":true}` with coords in either screen or world space (decide which, or accept both via a `space` field).

## Entity-attached Gum visuals are not in Gum's update tree

`Entity.Add(GraphicalUiElement)` and `Entity.Add(FrameworkElement)` wrap the visual in a `GumRenderable`, register it for rendering, and drive its position from the entity's `AbsoluteX/Y` each frame — but never add it as a child of any Gum root. Consequences:

1. **Forms controls aren't live.** A `Button` added to an entity renders but doesn't receive cursor/click events, because Gum Forms input handling walks `Root.Children` (or `MainRoot`) to dispatch. The user sees a button-shaped thing that does nothing.
2. **Hot-reload skips them.** `GumHotReloadManager.PerformReload` only rebuilds `root.Children`. Entity-attached visuals are invisible to it. Today the workaround is to subscribe to `FlatRedBallService.GumHotReloadCompleted` and `RestartScreen(HotReload)`, which works but throws out everything.
3. **Animations/state interpolations on entity visuals may not tick** if Gum's per-frame update walks `Root` rather than tracking elements globally.

Possible directions:
- Add a hidden "entity-attached" Gum sub-root that the engine maintains as a child of `Root`, parented under it for update/input/hot-reload purposes but with layout that doesn't disturb world-space-driven positioning. Visuals added via `Entity.Add(...)` go in there.
- Or reach into Gum's Forms input system to register entity-attached `FrameworkElement`s as input-eligible without re-parenting.
- Either way: fixing this also fixes per-element hot-reload for entity visuals (no screen restart needed), so it's worth doing properly rather than papering over each symptom.

## Window resize misaligns world-space cards in Solitaire

Resizing the Solitaire window causes every card to draw at an offset from its slot. Cards
are positioned via `SlotWorldCenter` which converts a Gum slot's `AbsoluteLeft/Top` (canvas
space) into world coords by subtracting half the camera's orthogonal extents — but on resize
the canvas-to-world relationship shifts (Pattern A stretch-to-viewport), and either the slot
absolutes, the camera extents, or the cached anchor positions go stale. Repro: launch
Solitaire desktop, resize the window, observe cards drift relative to the green-felt slot
graphics. Decide whether the fix belongs in the sample (re-run layout on resize), in the
engine's world↔canvas helper, or in Gum's resize signaling.

## Deterministic randoms under automation mode

When automation mode is active, `FlatRedBallService.Random` (and any other engine-owned `GameRandom` paths) should seed deterministically so that recorded automation runs reproduce exactly. Game code that constructs its own `Random` / `GameRandom` should still get a well-defined seed when it asks the engine for one, so non-determinism doesn't sneak in through `new Random()` calls. Open question: how the seed is communicated (env var, automation-mode init field, dedicated `AutomationOptions.Seed`?) — settle that before implementing.

## Web load times for large Gum projects

Initial load is slow on the BlazorGL/KNI target when a `.gumx` references many components — fonts, control templates, generated runtime types all compile/initialize on the main thread before the first frame draws. Need to (a) measure where the time actually goes (Gum project load vs. runtime registration via `RegisterRuntimeType` module initializers vs. asset decode), (b) decide whether the fix is engine-side (lazy/deferred runtime registration, parallel asset decode) or Gum-side (incremental project load), and (c) confirm WASM-specific costs separate from the same load on desktop. Solitaire's `.gumx` is the readily-available large project to profile against.

## Multiple Gum screens and transitions

Today a screen calls `this.Add(new GameScreenGum())` once in `CustomInitialize` and that visual lives until the FRB Screen tears down. There's no first-class story for (a) hosting multiple Gum screens within one FRB Screen with a current/next swap, or (b) cross-fading / sliding between them. Game code can hand-roll this with two `FrameworkElement`s and a tween on alpha/position, but the absence of an idiomatic pattern means every consumer reinvents it. Decide: is this a sample-level recipe (document the tween-between-two-roots pattern), a Gum-level Forms feature (a `ScreenHost` control), or an FRB-level helper (`Screen.PushGumScreen` / `PopGumScreen` with a built-in transition arg)?

## SVG and Lottie support in Gum

Gum currently ships with raster-only visuals (`Sprite`, `NineSlice`, `ColoredRectangle`). Vector / animated-vector formats — SVG for static UI art, Lottie for animated UI (the After Effects → Bodymovin export pipeline) — would close a real gap for UI-heavy games. Open questions: Does this belong in Gum (new runtime types alongside `Sprite`) or in FRB2 (an `IRenderable` wrapper that hosts an SVG/Lottie renderer)? Which third-party renderer? — `Svg.Skia` and `SkiaSharp.Skottie` are the obvious starting points but pull SkiaSharp into the dependency graph, which has its own desktop/WASM size implications. Confirm WASM viability before committing.

## Rive support (lower priority than Lottie)

Rive is the interactive-animation alternative to Lottie — richer state-machine model, but a heavier runtime and a paid editor. Track this only as the secondary option after Lottie lands; if Lottie covers the actual UI animation use cases the engine sees, Rive may never be worth the integration cost. Decision criteria: does any sample or downstream user actually need Rive's interactive features (state machines triggered from game code), or are pre-rendered animation clips sufficient?

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
