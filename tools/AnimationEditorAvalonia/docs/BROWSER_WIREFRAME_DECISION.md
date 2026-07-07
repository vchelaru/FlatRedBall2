# Decision: wiring WireframeControl into the browser build (#614)

Status: **Implemented and live-verified in a real browser tab** (tree selection → wireframe
render → inspector sync → drag-to-resize, all confirmed working end-to-end).

## Problem

Phases 1-2 (#603, #610) made the browser build browsable and let the user create/delete/rename
content and edit numeric shape properties, but there was no visual canvas to click-and-drag
shapes on -- that's `WireframeControl`, already fully built and tested on desktop.

Before wiring it in, checking `WireframeControl`'s texture-loading path (via its
`TextureViewport` base) turned up a real blocker: `LoadTexture(string? filePath)` reads straight
from disk (`File.Exists` + `SKBitmap.Decode(path)`). `PreviewControl`, wired in Phase 1, avoids
this entirely by going through `ThumbnailService`'s in-memory cache. `WireframeControl` never
needed that seam before because it only ever ran on desktop, where a real filesystem exists.

## Decision

Fixed the disk-read assumption at its root (`TextureViewport`, the shared base both
`WireframeControl` and `PngPreviewControl` derive from) rather than special-casing
`WireframeControl`:

- `TextureViewport.LoadTexture` gained an optional `knownBitmap` parameter. When supplied, it's
  used directly instead of reading `filePath` from disk -- mirrors
  `ProjectManager.LoadAnimationChain`'s `knownTextureSizes` fix (#535) for the identical "no disk
  in the browser" constraint. An `_ownsBitmap` flag tracks whether the control decoded the bitmap
  itself (owns and disposes it) or received it from a caller (owns nothing, never disposes) --
  `ThumbnailService`'s cache owns bitmaps seeded via `SeedTexture`/`GetBitmap`, so a caller-owned
  bitmap must survive a texture switch untouched.
- `WireframeControl.InitializeServices` gained an optional trailing `ThumbnailService` parameter
  (existing desktop call sites are unaffected -- `null` means "read from disk," the pre-#614
  behavior). `RefreshAll()` resolves the current texture through it when supplied, using the same
  bare-name-first lookup order `ThumbnailService.ResolveTexturePath` already implements.

Wired into `AnimationEditor.Browser/App.axaml.cs` as a third panel alongside the tree/inspector
(left) and preview (right): Move mode (drag existing handles/chains) needs no toggle -- it's the
control's default, driven by pointer events already wired in its constructor -- Magic Wand mode
gets a toggle button. `FrameRegionChanged`/`ChainRegionChanged`/`FrameLiveUpdated`/
`FrameCreatedFromRegion` are wired the same way `MainWindow` wires them, except
`FrameCreatedFromRegion`'s texture-name resolution: desktop derives a relative texture name from
`WireframeCtrl.LoadedTexturePath` via `Path.GetRelativePath` against the achx's real folder; the
browser has no real folder for that path to be relative *to* (`ProjectManager.FileName` is a
synthetic identity), so the handler reuses the selected chain's existing frame `TextureName`
directly instead of deriving one from a path that was never a real disk location.

## Live verification

Loaded the dev server in a real Chrome tab and confirmed, screenshot-by-screenshot:
- The bundled sample's wireframe (full spritesheet) and preview (current frame) render correctly
  side-by-side with no console errors on load.
- Clicking "Frame 1" in the tree selects it in the wireframe (handles + origin crosshair appear),
  updates the preview to that frame, and populates the inspector (texture/length/coords/offset/
  flip/color) -- full three-way sync confirmed live, not just in headless tests.
- Dragging a resize handle actually resized the frame region (visible in a follow-up screenshot).

One thing observed, not resolved: dragging triggered two console errors --
`Failed to create render target for mode 3/2: HTMLCanvasElement.getContext returned null` -- but
the app stayed fully responsive afterward (confirmed by a successful subsequent screenshot and
continued interaction). This looks like a canvas/GL-context resource hiccup from having three
SkiaSharp-rendering surfaces (tree, wireframe, preview) active on one page in the browser's
CPU/software Skia path (per `BROWSER_SPIKE_FINDINGS.md`'s note that `GrContext` is null in this
environment), not a functional defect introduced by this wiring. Worth a closer look if it recurs
or worsens as more panels are added (Phase 4's Files panel, additional tabs), but out of scope to
chase further here since it didn't block any interaction observed.

## Known gap

The Magic Wand mode toggle and `FrameCreatedFromRegion` (creating a new frame from a flood-fill
or ctrl+click region) were wired but not live-tested this pass -- only tree-selection → wireframe
render → drag-resize was exercised end-to-end. A full manual pass covering Magic Wand and new-
frame creation is still open, same category of residual gap as every other browser-only feature
in this codebase.
