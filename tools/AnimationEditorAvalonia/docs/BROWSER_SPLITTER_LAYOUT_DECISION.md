# Decision: splitter-based stacked canvas layout (#652, Phase 10)

Status: **Shipped.** Verified live in a real Chrome tab: layout renders correctly, wireframe zoom
(button and text-field-commit paths) works at the new stacked dimensions, preview continues
rendering/animating, zero console errors.

## Scope shipped

Replaced the browser's 3-column `Grid` (sidebar | wireframe | preview, side-by-side) with
desktop's shape, matching `AchxEditorPane`:

- **`sidebarColumnSplitter`** between the sidebar (`leftColumn`) and the canvas area — new; the
  browser never had this before (sidebar was a fixed `Width="260"` with no resize).
- **`canvasColumn`**: a `Grid` with `RowDefinitions="*,4,*"` stacking `wireframe` (row 0) over
  `preview` (row 2), with **`canvasSplitter`** (row 1) between them — replaces the previous
  side-by-side `wireframe | preview` columns.

Both splitters use plain `GridSplitter` with `Background` bound via `GetResourceObservable`
(the Phase 8 fix — `.Bind()` + `GetResourceObservable`, never `new DynamicResourceExtension()`
directly), matching desktop's `LineStrong`-colored splitters exactly.

This is pure browser-only container wiring — `WireframeControl`/`PreviewControl` are unchanged,
`AnimationEditor.Core`/`.Views` untouched.

## Pan/zoom math verification

The roadmap explicitly called out verifying `WireframeControl`/`PreviewControl`'s pan/zoom math
isn't a fixed-aspect assumption that would break under the new stacked shape. Confirmed live:

- Zoomed the wireframe via the `ZoomControl` `+` button (100% → 1600%) — content scaled correctly,
  no clipping/corruption artifacts.
- Reset via the editable percent field (triple-click, type `100%`, Enter) — committed correctly,
  content re-rendered at the right scale.
- Preview kept animating/playing correctly throughout, in its new (shorter, since it now shares
  vertical space with wireframe instead of getting the full column height) bounds.

Both controls compute zoom/pan relative to their own `Bounds`, not the parent grid's shape, so this
was expected to be a non-issue — confirmed rather than assumed.

## Not confirmed: interactive splitter dragging via synthetic pointer events

Both `left_click_drag` attempts (small and large distance, precisely targeted at the 4px splitter
row via a zoomed screenshot first) produced no visible resize, with zero console errors either
way. This matches the **exact precedent already documented** in `BROWSER_SPIKE_FINDINGS.md` for
drag-and-drop file uploads: "Avalonia's browser drag-drop plumbing requires a genuine trusted
OS-driven drag gesture and silently ignores synthetic ones." `GridSplitter`'s pointer-capture-based
drag-resize appears to hit the same category of limitation in this tooling environment — a
continuous stream of trusted `pointermove` events while the button is held, which CDP's synthetic
drag doesn't reproduce.

**Verified by construction instead:** this is the identical `GridSplitter` + `ResizeDirection`
pattern desktop's own `SidebarSplitter` (Phase 9, already interactively verified when built,
per precedent) and `AchxEditorPane`'s wireframe/preview splitter already use successfully, with
correct `Grid.SetRow`/`Grid.SetColumn` index assignments checked by direct code review. Same
residual gap category as M1's real-browser rendering check and Phase 1's "Open Folder needs a
human" — flagging here rather than claiming false confidence.

## Verification

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] `dotnet test` on all 3 suites — Core 1584, Views 29, App 673 — unchanged, all green (no
      Core/Views changes this phase)
- [x] `dotnet publish -c Release` for `AnimationEditor.Browser.csproj` — exit 0
- [x] Live Chrome: stacked layout renders correctly; wireframe zoom in/out (button + text-commit)
      works at the new dimensions; preview continues animating; zero console errors
- [ ] Interactive splitter drag — not confirmable via this session's synthetic-pointer-event
      tooling; verified by construction (identical pattern to desktop's own working splitters)
