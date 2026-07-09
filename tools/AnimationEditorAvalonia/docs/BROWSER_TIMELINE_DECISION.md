# Decision: timeline/scrubber strip (#647, Phase 14)

Status: **Shipped** (pending live-Chrome verification on merge).

## Scope shipped

- New portable control `AnimationEditor.Views/Controls/TimelineStripControl.axaml(.cs)` — lifts
  desktop's inline `MainWindow.axaml` timeline `ItemsControl` template into a shared
  `UserControl`, driven by the already-tested Core helpers:
  - `TimelineBuilder` (frame cell widths, duration label)
  - `TimelineScrubMapper` (content-space X → frame index + fraction)
  - `TimelineStripSignature` (skip rebuild on pure selection/scrub changes)
  - `ThumbnailService.GetFrameThumbnail` (per-frame texture crops, 22×18)
- Browser wiring in `AnimationEditor.Browser/App.axaml.cs`:
  - `previewBlock` grid (`RowDefinitions="*,52"`) below Phase 10's stacked wireframe/preview
  - Transport column (Play/Pause + speed pill) beside the strip, matching desktop's row-2 layout
  - `FrameScrubbed` → `PreviewControl.ScrubToFrame`; playback tick/index hooks drive the
    accent playhead offset
- **Deferred:** desktop's multi-chain `GroupTimelineTracks` comparison view — single-chain
  scrubbing only, per the roadmap.

## TDD

`AnimationEditor.Views.Tests/TimelineStripControlTests.cs` (3 tests):

1. `SetChain_WithThreeFrames_FrameCountMatchesChain`
2. `ResolveScrubAt_ContentXInSecondCell_SelectsFrameOne`
3. `ScrubAt_ContentXInSecondCell_RaisesFrameScrubbedWithIndexOne`

Browser `App.axaml.cs` wiring is untested glue (established Phase 1–13 convention).

## Verification

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] Core / Views / App test suites — all green (Views +3)
- [x] `dotnet publish -c Release` for `AnimationEditor.Browser.csproj` — exit 0
- [ ] Live Chrome: timeline cells + thumbnails render; click/drag scrubs playhead and selects
  frame; Play/Pause + speed control playback; zero console errors

## Residual gaps

- Interactive scrub drag in headless/CDP tooling may be unreliable (same category as Phase 10's
  splitter-drag limitation documented in `BROWSER_SPLITTER_LAYOUT_DECISION.md`) — verify scrub
  via live Chrome, not synthetic pointer streams alone.
- Space-bar Play/Pause accelerator not wired in browser (desktop has it on `PlayPauseBtn` tooltip
  only; browser transport is click-only for now).
