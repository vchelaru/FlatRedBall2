# Decision: wiring multi-file tabs into the browser build (#620, Phase 4)

Status: **Implemented, unit-tested, and partially live-verified** (single-tab strip render and
no-op re-click confirmed in a real browser tab; opening a second tab was not exercisable live --
see Known gap).

## Problem

Phases 1-3 gave the browser build a full single-file editing loop (browse, mutate, undo, edit
shapes) but every load path (bundled sample, Open Folder, drag-drop) overwrote the same
in-memory project state -- there was no way to have two files open at once, unlike desktop's
`MainWindow`, which has used `TabManager`/`TabEditorCache` for this since before the browser port
started.

## Decision

`TabManager` and `TabEditorCache` are pure in-memory, already fully built and unit-tested in
`AnimationEditor.Core`, and neither has any disk dependency in its actual code path:
`TabEditorCache.HasFreshCache` treats a tab as fresh whenever its cached disk-write-time is
`null`, and the private `TryReadDiskWriteTimeUtc` naturally returns `null` for any path that
doesn't exist on disk -- which is every browser tab's path, since there is no real filesystem.
So browser tabs are already correctly "always trusted" with zero new code in Core; this phase is
wiring only, in `AnimationEditor.Browser/App.axaml.cs`:

- A `TabManager` is constructed alongside the other services, and the bundled sample is
  registered as tab 1 via `OpenOrFocus`.
- A tab strip (`StackPanel` of buttons, rebuilt on every change) sits above the existing toolbar.
  Clicking a tab calls `SwitchToTab`, which mirrors `MainWindow`'s own sequence: capture the
  outgoing tab's project/undo state (`TabEditorCache.CaptureFromProject` +
  `UndoManager.TakeSnapshot`), activate the target (`TabManager.Activate` +
  `AppCommands.TryActivateTabFromCache`), then restore its undo history
  (`UndoManager.RestoreSnapshot`). Unlike desktop's `ActivateTabContentAsync`, there is no
  disk-reload fallback for a stale cache -- browser tabs never hit that path, so
  `TryActivateTabFromCache`'s `false` return (which only happens for a genuinely stale cache) is
  not specially handled here.
- `TryActivateTabFromCache` already raises `RefreshWireframeRequested` /
  `RefreshAnimationFrameDisplayRequested`, which `WireframeControl`/`PreviewControl` already
  subscribe to in their own `InitializeServices` (Phase 1/3) -- so switching tabs repaints both
  without any new event wiring. The tree is rebuilt explicitly
  (`animationTree.InitializeServices(...)`), matching the existing convention every other load
  path in this file already uses (Open Folder/drag-drop also call `InitializeServices` directly
  rather than subscribing to `RebuildTreeViewRequested`).
- Opening a new file (Open Folder or drag-drop, after `BrowserProjectLoader.TryLoadAsync`
  succeeds) calls a new `OpenNewTabForLoadedProject(displayName)` helper: capture the outgoing
  tab, `TabManager.OpenOrFocus` the new file's identity (`ProjectManager.FileName`, the same
  synthetic non-disk identity `BrowserProjectLoader` already establishes), capture the freshly
  loaded project into the new tab's cache, and reset undo history (a newly loaded file starts
  with a clean slate, matching desktop's `FinishLoadIntoEditor`).

## Known gap

Only a single tab (the bundled sample) was exercised live end-to-end: the tab strip renders,
shows the active tab in bold, and re-clicking the same (only) tab correctly no-ops without
disturbing the current selection/wireframe/inspector state (confirmed via screenshot + console).

Opening a **second** tab was not verified live. `Open Folder` uses `IStorageProvider`'s native
OS folder-picker dialog, which -- like the console-error investigation from Phase 3 -- is outside
what the browser-automation tool can see or interact with (it's an OS-level dialog, not page
content); clicking the button produced no visible error but no way to drive the picker either.
Drag-and-drop of real files has the same limitation (synthetic OS-level file drop isn't something
this tool can generate). This is a tooling reach limit, not a code issue: `TabManager`'s own
95%+ existing unit test suite in `AnimationEditor.Core.Tests` already covers `OpenOrFocus`
opening vs. focusing, and `TryActivateTabFromCache`/`RestoreSnapshot` are exercised by
`TabSwitchCacheTests.cs`. A full manual pass (a human dragging a second `.achx` + PNG onto the
page, or using the folder picker interactively) is the honest remaining gap, same category as
Phase 3's Magic Wand/frame-creation gap.

The Files panel (browsing/dragging textures from a watched folder) named in issue #620's original
scope was not started this phase -- `FilesPanelControl.cs` lives in the desktop-only
`AnimationEditor.App/Controls`, not `AnimationEditor.Views`, and has real disk-browsing
assumptions that need their own research pass. Deferred to a follow-up issue rather than rushed
into this phase.
