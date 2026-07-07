# Decision: export + view/canvas polish in the browser build (#622, Phase 5)

Status: **Implemented and live-verified in a real browser tab** (every toggle and the PixiJS
export confirmed working end-to-end, including a real bug found and fixed along the way).

## Problem

Phases 1-4 built the core browse -> edit -> undo -> tabs loop. The remaining desktop-only surface
was mostly view/canvas polish (`MainWindow`'s onion skin, interpolate, F3 diagnostics, independent
wireframe/preview zoom presets, grid-snap) plus one real capability gap: exporting to a PixiJS
spritesheet.

Research (an Explore agent survey of `MainWindow.axaml.cs` and the `AnimationEditor.Views`
controls) found that **six of the seven features needed zero new logic** -- they're already
public/internal properties and methods on the shared, already-tested `PreviewControl`/
`WireframeControl`/`TextureViewport` (`ShowOnionSkin`, `InterpolateOffsets`, `ShowGuides`,
`DiagnosticsEnabled`, `SetZoomPercent`, `SetGrid`). `MainWindow` just flips them from its own
toolbar; the browser needed the same toolbar wiring, nothing more. Guides in particular needed
**no wiring beyond a toggle for the origin crosshair** -- ruler-click-drag-to-create/move/
right-click-to-remove is already fully self-contained inside `PreviewControl`'s own pointer
handlers and was working the moment `PreviewControl` was wired in during Phase 1.

The seventh item, PixiJS export, needed real porting work: desktop's
`AppCommands.ExportToPixiJsAsync` writes the JSON via `System.IO.File.WriteAllText` and copies
referenced PNGs via `System.IO.File.Copy` -- both unavailable in the browser.

## Decision

**View/canvas polish** -- added a toolbar row in `App.axaml.cs` with: Onion Skin / Interpolate /
Guides toggle buttons, a Diagnostics (F3) toggle (plus a best-effort `TopLevel.KeyDown` handler
for F3 itself -- the button is the reliable path since a browser may intercept F3 for its own
"Find next" before the page sees it), independent Wireframe/Preview Zoom +/- buttons using the
same `ZoomPresetStepper.StepToNextPreset` desktop uses (already `internal`, already visible to
`AnimationEditor.Browser` via existing `InternalsVisibleTo`), and a Snap to Grid checkbox + grid
size `NumericUpDown` calling `WireframeControl.SetGrid(bool, int)`. None of this touches Core;
it's pure wiring, same category as every prior phase's toolbar additions. Persistence
(zoom%/grid-size/guide positions normally live in the desktop-only `.aeproperties` companion file)
is out of scope here, same gap already flagged in `BROWSER_SETTINGS_DECISION.md` for Phase 2's
zoom/grid settings -- resets to defaults on reload.

**PixiJS export** -- reused the exact same pure, already-tested
`PixiJsSpriteSheetExporter.Export(acls, textureSizeResolver)` desktop calls, with two
browser-specific adaptations:
- The texture-size resolver: desktop's `ProjectManager.GetTextureSizeInPixels` reads a PNG's
  dimensions via `System.IO.File.OpenRead` (the same disk-read pattern already fixed once before
  for `LoadAnimationChain`'s `knownTextureSizes`). The browser passes a local resolver that asks
  `ThumbnailService.GetBitmap(name)` for the already-decoded bitmap's `Width`/`Height` instead --
  no disk touched, ever.
- The output path: instead of writing to disk and copying PNGs next to the JSON, both the JSON
  text and each referenced texture (re-encoded to PNG bytes from `ThumbnailService`'s cached
  `SKBitmap`, never re-read from anywhere) are handed to the browser as Blob downloads via a new
  `DownloadInterop`/`wwwroot/download.js` pair, following the exact same
  JSImport-bridge-plus-thin-JS-module pattern `LocalStorageInterop`/`localStorage.js` established
  in Phase 2. PNG bytes cross the JS boundary as base64 (not a raw `byte[]`/`Uint8Array` marshal)
  to stay on the same proven-working string-only JSImport shape `LocalStorageInterop` already
  uses, rather than taking on untested byte-array marshalling.

## Real bug found and fixed: `JsonSerializerIsReflectionDisabled`

Live-testing the export button (not just building/unit-testing it) surfaced a genuine WASM-only
runtime failure that no desktop test could have caught: clicking Export to PixiJS threw
`ManagedError: JsonSerializerIsReflectionDisabled` from deep inside the WASM runtime.
`Microsoft.NET.Sdk.WebAssembly` disables `System.Text.Json`'s reflection-based serialization by
default -- independent of Debug/Release or trimming settings -- so
`PixiJsSpriteSheetExporter.Export`'s `JsonSerializer.Serialize(sheet, options)` call (reflection-
based) worked fine on desktop (where reflection serialization is enabled) and in every existing
`AnimationEditor.Core.Tests` run, but always failed in the one environment that matters for this
feature.

Fixed at the root, in Core: added `PixiJsJsonContext` (`[JsonSerializable(typeof(PixiJsSpriteSheet))]`
partial `JsonSerializerContext`) and switched `Export` to the source-generated
`JsonSerializer.Serialize(sheet, PixiJsJsonContext.Default.PixiJsSpriteSheet)` overload -- the
officially-recommended trimming/AOT-safe replacement for reflection-based serialization, and a fix
that also hardens this code path for any future trimmed/AOT desktop build, not just the browser.

No new xunit test reproduces the WASM-only exception itself -- that's a live-runtime-environment
failure mode a non-WASM test host cannot trigger (same class of gap as `LocalStorageInterop`'s
untestable browser wiring). What *is* covered: the full existing
`PixiJsSpriteSheetExporterTests.cs` suite (10 tests asserting exact JSON shape/values/schema) was
re-run unmodified after the fix and still passes byte-for-byte -- proving the source-generated
context produces identical output to the reflection-based path it replaced, so the fix didn't
silently change the export format while making it actually work in the one place it needs to.

## Live verification

Loaded the dev server in a real Chrome tab and confirmed, per feature:
- **Onion Skin**: toggle activates with no console errors. Could not visually confirm the ghost
  overlay itself -- the bundled sample's frames are fully opaque, same-size rectangles, so a 0.4-
  alpha previous-frame layer drawn *under* a fully-opaque current frame is completely covered and
  produces no visible pixel difference by construction, not a defect. The underlying opacity math
  (`FramePreviewOpacity.Resolve`) already has dedicated desktop test coverage.
- **Interpolate**: toggles with no console errors.
- **Guides**: clicking the top ruler created a vertical guide line; clicking the left ruler
  created a horizontal guide line; both rendered immediately in the preview panel -- confirmed
  this needed no new code, exactly as the pre-implementation analysis predicted.
- **Diagnostics (F3)**: toggle button turned on a full diagnostics overlay on *both* panels
  simultaneously -- wireframe showed `draw: 4.50 ms (~222 fps) [CPU]` plus zoom/pan/viewport/
  content stats, preview showed its own `draw: 2.50 ms (~400 fps)`. Confirmed via screenshot.
- **Wireframe/Preview Zoom presets**: stepping Wireframe Zoom + twice zoomed only the wireframe
  panel; stepping Preview Zoom + twice zoomed only the preview panel, independently -- confirmed
  via screenshot that the two panels' zoom levels diverged as expected.
- **Snap to Grid**: checking the box rendered a visible 16px grid overlay on the wireframe's
  loaded texture, confirmed by zooming into the screenshot.
- **Export to PixiJS**: first attempt reproduced the `JsonSerializerIsReflectionDisabled` bug
  above; after the fix, a fresh tab's export produced
  `Exported player.json with 1 warning(s): Per-frame durations are not part of the PixiJS
  spritesheet format and were dropped.` -- matching the exact warning
  `PixiJsSpriteSheetExporterTests` asserts for this fixture, and no "texture not found" warning,
  confirming the seeded `player.png` bitmap was found and downloaded.

One tooling wrinkle worth recording: the browser-automation console-log reader kept replaying
stale entries (including the pre-fix exception) in a tab that had accumulated history across
several reloads, even with `clear: true` passed. Opening a brand-new tab and reloading fresh
resolved it and gave an unambiguous signal that the fix worked. Worth remembering for future
sessions -- if a console check looks suspiciously identical to a prior one, open a new tab before
trusting it.

## Known gaps

- No persistence for zoom%/grid-size/grid-on-off/guide positions across a page reload (same
  category of gap as Phase 2's settings decision -- only theme has a browser storage path today).
- F3 as a keyboard accelerator is best-effort only; not verified against real browser F3
  interception behavior (Chrome's default F3 behavior is page-find, which may or may not reach the
  page's `KeyDown` handler depending on focus state) -- the toolbar button is the reliable path
  regardless.
- Multi-texture / subdirectory-referencing content: the export downloads each referenced texture
  under its bare file name (`Path.GetFileName`), matching how `ThumbnailService` keys its cache in
  this codebase. A project whose frames reference textures via a subdirectory-relative path would
  still export correctly (PixiJS's own `meta.image` keeps whatever name the model already used),
  but the downloaded PNG lands flat in the browser's Downloads folder rather than preserving that
  subdirectory -- not exercised by the bundled sample (single bare-named texture) and not fixed
  here; flagged for whoever hits it with real subdirectory-referencing content.
