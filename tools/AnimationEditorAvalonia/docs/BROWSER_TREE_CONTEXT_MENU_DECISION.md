# Decision: tree right-click context menu (#754, Phase 2)

Status: **Shipped**, pending live-browser manual click-through. Headless tests cover menu shape
and every action's effect (delete, duplicate, copy/cut/paste round-trip, rename, copy-texture-path).

## Scope shipped

`AnimationTreeControl` (browser's only tree — desktop's `MainWindow` keeps its own separate
`TreeView`) now builds its right-click menu from the same `TreeMenuPlanBuilder.Build(...)` plan
desktop's `OnTreeContextMenuOpening` consumes (Phase 1, #758). A right-click selects the row
under the pointer first (Tunnel-phase `PointerPressed`, mirroring desktop's `OnTreePointerPressed`
right-click branch — Avalonia doesn't move `TreeView.SelectedItem` on right-click by itself), then
`OnTreeContextMenuOpening` clears and rebuilds `Tree.ContextMenu.Items` from the plan.

Copy/Cut/Paste/Duplicate/Delete are new host-supplied `TreeMenuActions` callbacks, implemented as
thinner versions of `MainWindow`'s `HandleCopyCoreAsync`/`HandleCutCoreAsync`/
`HandlePasteCoreAsync`/`HandleDuplicate`/`HandleDelete`:

- Clipboard I/O goes through `TopLevel.GetTopLevel(this)?.Clipboard` — the same Avalonia
  `IClipboard` abstraction desktop uses, portable to the browser backend with no extra plumbing.
- No `IsTextInputFocused()` gate: unlike desktop's Ctrl+C/X/V hotkeys, a context-menu click is
  never in competition with a focused `TextBox`.
- No manual tree-refresh/selection-resync after a mutation: every `AppCommands` mutation already
  raises `AnimationChainsChanged`, which `App.axaml.cs` already wires to `animationTree.Refresh`
  (Phase 1, #603/#610) — so paste/duplicate/delete just work without extra bookkeeping here.
- `SelectedChains`/`SelectedFrames`/`SelectedRectangles`/`SelectedCircles` on `ISelectedState`
  already fall back to the singular `Selected*` property when the multi-select bag is empty (see
  `SelectedState`), so `SelectionCopyContext`/`HandleDelete`'s dispatch logic works unchanged even
  though `AnimationTreeControl`'s `TreeView` is `SelectionMode="Single"` today.

**Rename** (rectangle/circle/chain) reuses the existing `TreeNodeVm.BeginEdit()`/`CommitRename`
inline-edit mechanism `EnableRename` already wired for chain double-tap-rename (Phase 2, #610) —
`CommitRename` is extended here to also call `AppCommands.SetRectProps`/`SetCircleProps` for
shape nodes, which the browser tree had never needed until a menu could ask for it.

## Remap decision: "View Texture in Explorer" → "Copy Texture Path"

Desktop's version shells out to the OS file explorer — no filesystem in the browser, same
category of gap as Phase 11's "Open Containing Folder" (`BROWSER_TABSTRIP_CONTEXT_MENU_DECISION.md`).
Remapped to "Copy Texture Path" (writes `AnimationFrameSave.TextureName` to the clipboard),
mirroring that same decision doc's "Copy Full Path" precedent — low ceremony, and gives the user
*something* actionable with the texture reference instead of a dropped item.

The other three host slots (`AdjustFrameTime`, `AddMultipleFrames`, `AdjustOffsets` — all
dialog-based, chain-only) are left unhandled by `AnimationTreeControl`'s `AddHostSlotItem`, so
they're silently omitted from the browser menu. Tracked separately, issue #756 (no browser dialog
pattern exists yet).

## Verification

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] `dotnet test` on all suites — green, including 14 new
      `AnimationEditor.Views.Tests.AnimationTreeControlContextMenuTests` (menu shape for
      chain/frame/rect/circle nodes, delete, duplicate + flip variants, copy→paste and cut→paste
      round-trips through a real headless `IClipboard`, rename-via-menu committing a new shape
      name, copy-texture-path, and the right-click-selects-first pointer behavior)
- [ ] Live Chrome: right-click a chain and a frame, confirm the menu is populated and each item
      fires with no console errors — not yet exercised in a real browser tab in this session: a
      screenshot of the running app's DevTools/menu was captured for partial confidence, but a
      human still needs to click through Copy/Cut/Paste/Rename/Delete interactively before this
      ships (same category of residual gap as prior browser phases needing a human pass).
