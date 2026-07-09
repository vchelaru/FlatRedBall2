# Decision: toasts/banners/remaining affordances (#647, Phase 15)

Status: **Shipped** (pending live-Chrome verification on merge).

## Scope shipped

- **`EditorNotificationOverlay`** (`AnimationEditor.Views/Controls/`) — portable overlay with
  desktop's three notification surfaces:
  - Item-deleted undo toast (4s auto-hide + Undo button)
  - Generic bottom toast (dismiss ✕, optional Retry, 6s auto-hide)
  - Top-centre error banner (dismiss ✕, 8s auto-hide, theme-independent red framing)
- **Browser wiring** (`App.axaml.cs`):
  - `appCommands.ItemsDeleted` → undo toast
  - Export / save / texture-reload success → generic toast (status bar text kept too)
  - Export/save with nothing loaded → error banner
  - Overlay stacked above the editor shell via `Grid` + `ZIndex`
- **`AnimationTreeControl`** — hover-reveal inline add-frame button on chain rows (`.add-frame-btn`
  + `.plus` from `ThemeStyles.axaml`), wired to `IAppCommands.AddFrame`

## Not in scope / already covered

- **`.compact` / disabled-opacity** — `ThemeStyles.axaml` is already merged in
  `AnimationEditor.Browser/App.axaml`; `Button:disabled` opacity and `.flanker`/`.plus` classes
  apply repo-wide. No additional browser-only gaps found during this sweep.
- **Multi-chain group timeline, tree drag-reorder, context menus** — remain deferred (functional
  parity gaps, not Phase 15 sweep-up).

## TDD

`AnimationEditor.Views.Tests/EditorNotificationOverlayTests.cs` (4 tests) — overlay visibility
for error banner, item-deleted toast, generic toast, and retry button.

`AnimationEditor.Views.Tests/AnimationTreeControlTests.AddFrameBtn_Click_AddsFrameToChain` —
add-frame seam via `RaiseAddFrameForTest`.

Browser `App.axaml.cs` wiring is untested glue (established convention).

## Verification

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] Views test suite — all green (+5 tests)
- [x] `dotnet publish -c Release` for `AnimationEditor.Browser.csproj` — exit 0
- [ ] Live Chrome: delete a frame → undo toast appears and Undo restores it; export/save toasts
  show; error banner on empty export/save; hover chain row → + button appears and adds a frame
