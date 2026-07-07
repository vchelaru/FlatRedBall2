# Decision: read-only chain/frame/shape browsing on the browser build (Phase 1 of #588 follow-up)

Status: **Decided and implemented** (see `AnimationEditor.Views/Controls/AnimationTreeControl.axaml`
and `InspectorControl.axaml`). Related: [#603](https://github.com/vchelaru/FlatRedBall2/issues/603),
[#535](https://github.com/vchelaru/FlatRedBall2/issues/535), `docs/BROWSER_SPIKE_FINDINGS.md`.

## Problem

PR #588 (M1-M4) shipped a browser build that loads and plays exactly one animation chain —
`AnimationChainListSave.AnimationChains[0]` — with no way to see or select anything else in the
file. Its own "Known gaps" section flagged this explicitly: "no chain selector — would need
addressing before this becomes a real editing surface."

The desktop app's equivalent (`MainWindow.axaml`'s ANIMATIONS tree + Inspector tab) is a mature,
~900-line surface: zebra striping, inline rename, drag-reorder, multi-select, an "Add Frame"
hover button, cut-pending visuals, editable property fields wired to `AppCommands`/`UndoManager`.

## Options considered

### Option 1 — port `MainWindow`'s tree/inspector as-is (rejected for Phase 1)

The desktop implementation is imperative `x:Name`-based code-behind
(`FindControl<NumericUpDown>("PropPixelX")`-style field pokes) tightly coupled to that one
window's layout and to editing features (`AppCommands` mutation, `UndoManager`, multi-select,
drag-reorder) that are explicitly out of scope for this phase — those land in the next phase,
once `AppCommands`/`UndoManager` (already fully built and tested in Core) are wired up.

Porting it now would mean either dragging in App-only concerns unrelated to read-only browsing,
or forking a large surface area to strip them back out — more risk for a phase whose actual goal
is proving out a new capability: `AnimationEditor.Views` had **zero `.axaml` files** before this
phase (`WireframeControl` and `PreviewControl` are both plain `Control` subclasses built with
imperative C#, no XAML at all).

### Option 2 — small, new, read-only controls built on the existing portable Core view-models (chosen)

`TreeBuilder`/`TreeNodeVm` (`AnimationEditor.Core/ViewModels/`) already do 100% of the tree
construction/selection-routing logic desktop uses, are Avalonia-free, and are already unit-tested
(`TreeBuilderTests.cs`, `TreeNodeVmTests.cs`). The only genuinely new work this phase needs is the
Avalonia wiring around them — a `TreeView` bound to `ObservableCollection<TreeNodeVm>`, and a
plain-text inspector panel keyed off `ISelectedState`.

Building `AnimationTreeControl` and `InspectorControl` as new, deliberately minimal
`UserControl`s (their first `.axaml` in this project) keeps the phase's real unknown — "can
`AnimationEditor.Views` compile and render `.axaml` at all?" — isolated from any editing-feature
risk. That question was answered empirically before writing either control: a throwaway probe
`UserControl` was added to `AnimationEditor.Views`, built successfully with zero warnings, and
removed. No package/SDK changes were needed — `AnimationEditor.Views.csproj`'s existing
`Avalonia` package reference already brings the XAML compiler; visual theming (`FluentTheme`) is
supplied by the hosting `Application` (`AnimationEditor.Browser/App.axaml`), not by this project.

## Decision

Implemented Option 2:

- **`AnimationTreeControl`** (`AnimationEditor.Views/Controls/AnimationTreeControl.axaml(.cs)`) —
  a `TreeView` built from `TreeBuilder.BuildTree(acls)`, single-select only. Row selection calls
  `TreeBuilder.RouteNodeSelection(vm.Data, selectedState, acls)` — the exact routing logic
  desktop's `OnTreeSelectionChanged` uses, unmodified.
- **`InspectorControl`** (`AnimationEditor.Views/Controls/InspectorControl.axaml(.cs)`) — three
  read-only panels (frame / rectangle / circle), each a handful of `TextBlock`s populated
  imperatively on `ISelectedState.SelectionChanged`. Deliberately not data-bound to the model's
  raw fields (`AnimationFrameSave`/`AARectSave`/`CircleSave` are plain non-notifying fields, not
  `INotifyPropertyChanged` properties) — imperative refresh-on-selection-change is simpler and
  more explicit than binding to fields that never change out from under a fixed selection anyway.
  When a shape is selected, its panel takes precedence over the owning frame's (matches what the
  user actually clicked); no selection shows a placeholder.
- Both controls are independent of each other and of `PreviewControl` — any of the three can
  drive `ISelectedState`, and the others react via `SelectionChanged`, proven by a test
  (`SelectionChangedDirectlyThroughSelectedState_UpdatesInspector_NotJustViaTreeClicks`) that
  drives selection with no tree involved at all.
- New test project **`AnimationEditor.Views.Tests`** (mirrors `AnimationEditor.App.Tests`'s
  `Avalonia.Headless.XUnit` setup) is the first real test coverage for `AnimationEditor.Views` —
  it needed its own minimal `TestApp : Application` (just `Styles.Add(new FluentTheme())`) since
  this project, unlike `AnimationEditor.App`, has no `Application` of its own to reuse.

## Explicitly out of scope for this phase

No mutation (add/delete/rename/duplicate/reorder/flip/paste on chains, frames, or shapes), no
Undo/Redo UI, no drag-reorder/multi-select/inline-rename/context-menus, no editable inspector
fields, no tree thumbnails/zebra-striping, no wireframe shape editing, no settings persistence,
no search/filter wiring. All are real desktop features, deferred to their own later phase rather
than bolted onto this one — see the phased roadmap tracked alongside #588's follow-up work.

## Consequences

- A multi-chain `.achx` (e.g. the `ShmupSpace.achx` manual-test fixture from #588) is now fully
  browsable in the browser build for the first time — previously only `AnimationChains[0]` was
  ever reachable.
- Not yet verified end-to-end in a live browser tab: automated headless tests cover the tree's
  selection-routing and the inspector's panel-switching logic directly, but a real Chrome tab
  pass (load `ShmupSpace.achx` via Open Folder, click through several chains/frames/shapes,
  confirm the preview swaps animation and the inspector updates) still needs a human, same
  category of gap as Open Folder/Save As/drag-drop in `BROWSER_SPIKE_FINDINGS.md`.
