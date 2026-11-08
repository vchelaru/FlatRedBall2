---
name: animation-editor
description: "FlatRedBall2 AnimationEditor (Avalonia) — where the source lives, project layout, and the two-panel model. Triggers: AnimationEditor, AnimationEditorAvalonia, .achx editing, wireframe/preview panels, animationeditor label."
---

# AnimationEditor — Location & Layout

The AnimationEditor is the desktop tool that lets users edit `.achx` animation chain files (frames, regions, shapes, onion-skinning, preview playback). It is being rewritten on top of Avalonia and lives **inside this repository** at:

```
tools/AnimationEditorAvalonia/
```

**The product name is always `AnimationEditor`, one word**, in UI strings, docs, comments, and PR text. Never write "Animation Editor".

> The legacy WinForms version (`FlatRedBall.AnimationEditorForms`) lives in the separate `FlatRedBall` (FRB1) repo at `FRBDK/FlatRedBall.AnimationEditorForms/`. Do **not** edit it for FRB2 issues — that codebase is being replaced. Issues filed in `vchelaru/FlatRedBall2` always refer to the Avalonia version.

For writing tests against the editor — headless Avalonia, service wiring, the `[AvaloniaFact]` deadlock pitfall — see the **`animation-editor-testing`** skill. Before changing editor behaviour, dogfood it headlessly through the harness in `tests/AnimationEditor.App.Tests/Dogfood/` (README there); it drives the real window with simulated input and never touches the desktop. Any visual change needs before/after screenshots shown in the chat; the **`animation-editor-screenshots`** skill covers that and doc-page screenshots. For SkiaSharp rendering internals and performance debugging, see **`animation-editor-rendering`**. For Tiled `.tsx` support, see **`animation-editor-tiled`**.

## `.achx` is a general-purpose format — the editor authors, runtimes interpret

`.achx` is **not** an FRB2 file. It is a general-purpose animation/atlas format consumed by several runtimes that each render it their own way: Gum (across its Skia, raylib, and sokol.net backends), MonoGame/KNI/FNA, FRB1 (custom-shader rendering), and FRB2 (`SpriteBatch`). The editor authors the *format*; each runtime decides what to do with the data. This frames every feature decision here:

- **A field the editor exposes does not obligate any runtime to apply it.** Store the data in the format; whether a given runtime renders it is that runtime's choice. Do not gate adding a frame field on FRB2 (or any single runtime) implementing it — e.g. per-frame `Events` are stored in the format, and Gum and FRB1 ignore them.
- **The preview is a reference rendering, not a per-runtime contract.** The bottom panel renders with SkiaSharp (`PreviewControl`, `SKCanvas`/`SKColorFilter` in `DrawFrameCore`), so it will diverge from what a MonoGame/FNA/FRB1 runtime produces for the same file. That divergence is inherent to a general-purpose tool and is not a bug — pick a sensible canonical interpretation. "The preview might not match a runtime" is never a reason to withhold an authoring feature.

## Negative R/G/B is allowed — Multiply clamps it, Add uses it as subtract

`Red`/`Green`/`Blue` are plain `int?` (`AnimationChain.Common/AnimationFrameSave.cs`) with no load-time range check; FRB2's `SpriteFrameColor` clamps at draw time the same way the preview does. A `null` channel or `ColorOperation` inherits the most recent earlier frame in the chain that sets it, resolved by `EffectiveFrameColor` in `AnimationChain.Common`, which the editor, the engine, and the MonoGame/KNI packages all share. The inspector's `PropRed`/`PropGreen`/`PropBlue` `NumericUpDown`s (`MainWindow.axaml`) allow -255..255; `PropAlpha` stays 0..255 since alpha is a separate straight-opacity value, not part of a `ColorOperation`. In `FrameColorFilter.Create` (`AnimationEditor.Views/FrameColorFilter.cs`), **Add**'s Skia color-matrix offset is signed, so a negative value subtracts and clamps at the final pixel for free. **Multiply** packs the channel into a `byte` for `SKColor`, so it explicitly `Math.Clamp`s to 0 first — an unclamped negative `int`→`byte` cast wraps (`-10` becomes `246`) instead of darkening. Keep that clamp if you touch this method.

## Project layout

```
tools/AnimationEditorAvalonia/
├── AnimationEditorAvalonia.slnx
├── docs/
│   ├── DEVELOPMENT.md            ← read first when starting work
│   └── FEATURE_COVERAGE_REPORT.md
├── src/
│   ├── AnimationEditor.App/      ← Avalonia host: MainWindow.axaml(.cs), TabPanes/, Models/, Services/, Settings/, and App-only Controls/ (e.g. FilesPanelControl)
│   ├── AnimationEditor.Views/    ← the SkiaSharp controls, consumed by App
│   │   └── Controls/
│   │       ├── WireframeControl.cs, TextureViewport.cs    ← top panel (texture + frame regions)
│   │       ├── PreviewControl.cs, PngPreviewControl.cs    ← bottom panel (playback) + PNG diff viewer
│   │       └── ZoomControl.axaml(.cs), IZoomTarget.cs     ← reusable zoom widget (see "Two-panel mental model")
│   └── AnimationEditor.Core/     ← UI-independent logic (no SkiaSharp)
│       ├── CommandsAndState/     ← AppState, AppCommands, ApplicationEvents
│       ├── Data/, IO/, Rendering/, ViewModels/
│       └── ProjectManager.cs, SelectedState.cs
└── tests/
    ├── AnimationEditor.App.Tests/    ← headless Avalonia; covers App + Views
    └── AnimationEditor.Core.Tests/   ← pure logic
```

> Controls that physically live in `AnimationEditor.Views` still use the namespace `AnimationEditor.App.Controls` (folder ≠ namespace) — locate them by type name, not by namespace path.

## Build

```
dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx
```

Test commands and headless-test discipline live in the `animation-editor-testing` skill.

## Two-panel mental model

This is the `.achx` tab. `.png` and `.seqj` tabs show their own panes (see "Tab panes and hotkey scopes").

- **Wireframe (top)** — the texture editor. User loads a sprite sheet, draws/edits frame regions on it. State: pan, zoom, selected frame, snap-to-grid.
- **Preview (bottom)** — the animation player. Plays the selected `AnimationChain` at runtime speed; supports onion skin and origin guides. State: pan, zoom, playback timer, speed multiplier.

All three zoom surfaces — wireframe toolbar, preview toolbar, and the PNG diff bar — mount the same reusable **`ZoomControl`** (`AnimationEditor.Views/Controls/`), the `[−][editable %][+]` widget. Wire it in code with `zoomControl.Attach(target)`, where `target` is an **`IZoomTarget`** (exposes live `Zoom`, `SetZoomPercent`, `ZoomChanged`, `WheelZoomPresets`); `Attach` installs the wheel presets, follows `ZoomChanged` to display the live percent, and routes edits/steps back into the target. The suppression flag that breaks the echo loop lives inside `ZoomControl` — callers don't manage it.

**Landmine — the zoom hosts share no base class.** `IZoomTarget` exists only because `TextureViewport` (wireframe + PNG viewer) and `PreviewControl` are unrelated types. To share any *other* viewport behavior across both, extend `IZoomTarget` (or add a sibling interface); there is no common base to hang it on.

**Scan for an existing control before adding one to a second surface; extract on the second copy.** `ZoomControl` exists because the widget was first duplicated as raw XAML plus per-host event wiring across three toolbars. When a control *and its wiring* would be copied a second time, factor it into a reusable `UserControl` — duplicated markup and its feedback-loop plumbing drift apart otherwise. (Testing an extracted `UserControl` has a namescope gotcha — see `animation-editor-testing`.)

**Number fields never pad trailing zeros.** Every `FlankerNumericField`/`NumericUpDown` `FormatString` uses optional decimals (`0.###`, not `0.000` or `0.0#`), so 0.1 shows as `0.1`; `NumberFieldFormatTests` enforces this for `MainWindow`. Position-like values (offsets, pixel rects, color channels) use a plain `NumericUpDown` with short `X`/`Y` labels, and stepped scalars (length, speed, grid size, a chain's start and cycles) use `FlankerNumericField`; the sequence Inspector's TRACK section copies the `.achx` frame TRANSFORM section, and `SequenceInspectorParityScenarioTests` compares the two live.

## Cross-platform path operations — use `FilePath`, not `System.IO.Path`

**Never use `System.IO.Path.GetFileName`, `Path.GetDirectoryName`, or `Path.Combine` on paths stored in `ProjectManager.FileName` or any user-supplied path.** These methods are OS-native: on Linux they only recognise `/` as a separator, so a Windows-authored `C:\foo\bar.achx` path would be returned whole by `Path.GetFileName`.

`FilePath` (`AnimationEditor.Core.Paths.FilePath`) normalises both `\` and `/` regardless of host OS. Use its properties instead:

| Need | Use |
|---|---|
| Filename only (no directory) | `new FilePath(path).NoPath` |
| Directory of a file | `new FilePath(path).GetDirectoryContainingThis()` |
| Extension (lower-case, no dot) | `new FilePath(path).Extension` |
| Equality / comparison | `new FilePath(a) == new FilePath(b)` |

Tests that exercise path logic **must** use Windows-style backslash literals (e.g. `@"C:\projects\MyAnim.achx"`) to prove the cross-platform handling works — not `Path.Combine`, which would only exercise the current OS's separator.

## One animation tree per tab

**Landmine — the ANIMATIONS `TreeView` is built per tab (`MainWindow.TabTree`, kept on `TabEntry.CachedTreeState`), only the active tab's is visible.** `MainWindow.AnimTree` is the visible one. The trees come from the `AnimTreeViewTemplate` resource, so none is in the window's name scope and `FindControl<TreeView>("AnimTree")` returns null: use `window.AnimTree`. A `TreeView` or roots reference held across a tab switch or close points at a hidden or discarded tree. `RebuildTreeView` shows a tab's kept tree only while `TreeBuilder.RootsMatchModel` says it still matches the document, and rebuilds it otherwise.

## Tree reorder: chains, frames, and shapes within their type

Drag-and-drop tree reorder covers chains, frames, and shapes (pure resolvers `ChainDropResolver` / `FrameDropResolver` / `ShapeDropResolver`, wired in `MainWindow`). **Shapes only reorder among shapes of their own type in the same frame (#1285):** `.achx` and `.achj` save shapes grouped by type (rects, polygons, circles), so a cross-type order is lost on save and reload. `ShapesSave.Move`/`SetOrder` enforce the grouping; dropping on another type shows the `ShapeReorderNotice` toast. The drop commits through `AppCommands.MoveShapeToIndex`.

## Grid mode: double-click resizes; click/drag only repositions

Grid-mode click-to-place and handle-drag preserve the frame's existing size — only
double-click resizes it to the full grid cell (`GridPlacementCalculator.SnapToCell`,
called only from `WireframeControl.SnapSelectedFrameToGridCell`). This is deliberate:
a fresh PNG drop creates one frame sized to the whole sheet, and double-click-to-carve-
a-cell is how that gets sized down without dragging edge handles by hand. Don't collapse
the two gestures onto one shared size-preserving helper again — see
`GridPlacementCalculator`'s doc comment for why that was tried and reverted.

## Flipping a frame animates the preview, never the data

`FlipCommand` changes the flags and offset at once, then raises `IApplicationEvents.FramesFlipped` with each frame's before and after `FlipPose`. `PreviewControl` plays `FlipTransition` (`AnimationEditor.Core/Rendering/`) from the old pose: a straight blend of the two flip matrices, which squashes the sprite flat on its mirror axis halfway through, plus a slide of the offset. Only the frame on screen animates, export always draws the final pose, and tests step it with `AdvanceFlipAnimation` instead of waiting on the timer.

## Keyframe curves: Transform and Color play what the file stores

**Reverse and PingPong never reach the evaluator.** `PlaybackController` and `ChainClock` map their time onto the forward timeline with `PlaybackTimeline` (AnimationChain.Common), and `FrameElapsed` is forward time into the frame. Sampling, the keyframe lanes, the playhead and `SeekToFrame` all work in forward time, so none of them needs a direction.

The preview and the GIF/WebP export sample a chain through `KeyframeSampler` (`AnimationEditor.Core/Keyframes/`), which wraps the shared `KeyframeEvaluator`; there is no per-session Interpolate toggle. The inspector's diamond and eight-icon picker are `KeyframeControl` (`AnimationEditor.Views/Controls/`), mapped to stored (type, easing) names by `KeyframeCurvePicker`. A stored curve outside the eight reads as "nothing selected" and is never rewritten except by an explicit pick. A frame with its keyframe off ignores its own values, so edits to those fields go only to frames that are keyframes, and the read-only fields show `KeyframeInspectorState.DisplayOffset`/`DisplayColor`.

Shapes use the same machinery per shape (`ShapeKeyframeGroup.Offset`/`Size`, `KeyframeInspectorState.ForShapes`, `AppCommands.SetShapeKeyframe`/`SetShapeKeyframeCurve`, one `KeyframeControl` per group in each shape panel). A shape's keyframe state is relative to its name: the first frame holding the name is always a keyframe, so the diamond is disabled there. Offset and size edits go only to shapes whose group is a keyframe (`MainWindow.ShapeKeyframes`); a skipped shape's fields show the computed value and must never be written back as its own. Polygon points are a third group (`ShapeKeyframeGroup.Points`, polygons only; `PointsInterpolation`/`PointsEasing`/`SkipPointsKeyframe`): point i glides to point i, a differing count snaps, and with the keyframe off the vertex rows show `DisplayShapePoints` read-only and the preview refuses vertex gestures (`PreviewControl.IsPolygonVertexLocked`). A selected polygon's timeline shows Offset + Points lanes (no Size). The preview samples shapes through `KeyframeSampler.SampleShape`/`SampleShapePoints`, but dragging or resizing a shape in the preview still edits its stored values, not the glided ones.

## Tab panes and hotkey scopes

Each `TabKind` (`.achx`, `.png`, `.seqj`) has one `ITabPane` in `AnimationEditor.App/TabPanes/`. `MainWindow` routes tab lifecycle through `PaneFor`; a new kind is an `ITabPane` implementation plus a case in `PaneFor`. Hotkeys (`HotkeyDefinition.Scope`) and main-menu items carry a `HotkeyScope`: `Achx` (the default), `Document`, or `App`. A pane lists the scopes it takes in `Accepts`, and refused scopes do nothing (menu items are disabled). So a new hotkey or menu item works only in `.achx` tabs unless it is set to `Document` or `App`; for menu items that means adding it to `DocumentScopedMenuItems` or `AppScopedMenuItems` in `MainWindow.axaml.cs`.

## Sequence tabs reload live through their own watcher

`SequenceTabPane` owns a second `HotReloadWatcher` (not the achx tab's one on `IAppCommands`) that runs only while a `.seqj` tab is in front, watching the file (`SequenceDocument.ReloadFromDisk`, which clears the shared undo stack like `ReloadAchxFromDisk`), every source via `UpdateSourceList` (`ReloadChangedSources`) and the sources' textures (`ThumbnailService.InvalidatePath`). The document's saves must go through `SequenceDocument.SaveRunner` (the watcher's `RunOwnSave`) or its own writes reload it, and a view that caches what it drew from a source must key on `SequenceDocument.SourcesVersion`, as `SequenceLibraryControl.Show` does, since the source list alone does not change when a source's chains do. A hidden tab has no watcher, so `LoadAsync` catches up on switch-back with `SequenceDocument.ReloadIfChangedOnDisk` (a content hash of the file as last read or saved, so own saves and identical rewrites are no change), and File > Reload from Disk (`ITabPane.ReloadFromDisk`, a Document-scoped item) calls `ReloadFromDisk` directly; both discard in-memory edits the file lacks, with no prompt. A source whose folder does not exist yet is not live-watched (the folder is only watched once it exists), only caught up on switch-back.

## Sequence tracks have no Z: lane order is draw order

`SequenceTrackSave` has no depth field. The top lane is in front and the bottom lane is at the back, like a layers panel: the first track of the list covers the rest, so `SequenceScene.At` returns sprites in reverse list order (back to front). A new track is appended, so it lands at the back. Reordering lanes (`SequenceDocument.MoveTrack`) only reorders the list. Old `.seqj` files still hold a `"z"` per track: the reader drops it (it is listed in `ReadTrack`'s known keys so it is not kept as an unknown property) and the next save writes none. Do not add a depth value back.

## Sequence timeline snapping is one mechanism

Resizing a chain's right edge, moving a chain (its start and its end both snap) and dropping a library chip all go through `TimelineSnap` (`AnimationChain.Common`, pure) with targets from `TimelineSnapTargets` (Core). The snap distance is in pixels, and targets closer than twice that are thinned by `SnapKind` priority, so extend that enum and `Thin` rather than adding a second magnet or grid. The targets are taken when the drag starts, so a moved chain never snaps to its own edges or to a neighbour end it is cutting. Alt bypasses; the ruler scrub is deliberately not snapped.

Dragging a chain (or selection) onto the new-track row shares the chip drop's highlight and ghost through `TimelineDropPlan.ForMove`, but creates nothing until release: the release runs `TimelineNewTrackMove.Apply` (one new track per source track, named after its first chain) inside the move gesture, so Escape and the single undo entry need no special case.

## Sequence timeline selection and clipboard

Selection is `TimelineSelection` (chains or track headers, never both; view state, no undo entry); the rubber band is `TimelineMarquee`/`TimelineMarqueeSelector`, and dragging several chains is planned by `TimelineMultiMove`, all in `AnimationEditor.Core/Sequences`. Copy, Cut, Paste, Duplicate and Delete go through the sequence tab pane (`MainWindow.SequenceTabPane.cs`: `CopySelection`, `CutSelection`, `PasteSelection`, `DuplicateSelection`, `DeleteSelection`), which acts on the sequence list or the timeline by whichever held the focus last. The clipboard is JSON with three `kind`s: `frb2-animation-editor-sequence` (`SequenceClipboard`, list) and `-chains` / `-tracks` (`TimelineClipboard`, timeline). Copy follows the focused pane, but **Paste follows the clipboard** (`PasteRoute.For`, Core): sequences go to the list, chains or tracks to the selected sequence's timeline (which then takes the focus), whichever pane held it. A paste that cannot apply (empty clipboard, ordinary text, no sequence in the file) shows a "Nothing to paste" toast; a damaged copy shows the error banner. Never make a paste return silently.

**Chains paste depends on the selection (`SequenceDocument.PasteChains`).** With nothing selected, each chain returns to the track of the same name, and a missing track is created. With a chain or track header selected, the first copied track lands on that track and the other copied tracks keep their lane distance from it. Tracks paste right below the lowest selected track, or at the end.

## Several sequences selected in the list play together in the preview

The sequence list is multi-select like the `.achx` tree: `SequenceDocument.Selection` (`SequenceSelection`, by name, in selection order, view state only) holds them, and `SequenceDocument.SelectedSequence` stays the primary (first selected, until deselected), which the timeline, Inspector and Loop toggle act on alone. Rename is available only with a single selection: the row menu's Rename and the Inspector name box are disabled, and the inline box does not open, while several are selected (`SequenceListMenuLayout.CanRename`). Row menu labels count the selection like the undo labels (`SequenceListMenuLayout.Header`). Delete, duplicate, copy, cut and paste act on the whole selection (`SequenceDocument.SelectedIndices`), each as one undo entry whose undo restores positions and selection: a delete selects the survivor taking the first deleted one's place, duplicate and paste insert a block after the last selected and select the new set (first one primary), and several copied sequences travel as a version 2 `SequenceClipboard` envelope with a `sequences` array. Each selected sequence runs on its own clock and loop flag (`SequencePlayback`; no shared length, scrub moves the primary only), and `SequenceScene.At(document, IEnumerable<SequenceTime>)` composites them: sequences back to front in selection order (first selected at the back), then in reverse track order inside each (the top lane in front). Multi-selection is not remembered across restarts (as in `.achx`); `SequenceSelectionMemory` keeps only the primary.

Dragging a list row reorders the file's sequences: `SequenceListDragController` (App/Controls) is a pointer-capture drag like the timeline's track reorder, not OS `DragDrop`, so the Dogfood harness can drive it. It moves the whole selection as one block through `SequenceReorder` (Core, pure) and `SequenceDocument.MoveSequences` (one undo entry, selection kept by name), and records no Back entry. The library's source groups reorder the same way: `LibraryGroupDragController` (sibling, one group, reuses `SequenceReorder`) drags a group by its header and `SequenceDocument.MoveSources` rewrites `Sources` only; a press on a header collapses on release, so a drag never toggles it, and `CollapsedSourceSet` is keyed by source path so the state follows the move.

## Back/forward navigation (mouse buttons 4/5, Alt+Left/Right, View menu)

`NavigationHistory` (Core) holds two stacks of `NavigationLocation` (tab path plus its selection); the current place is never stored, `MainWindow.CaptureNavigationLocation` reads it fresh. The history is global: `NavigationTracker` (Core) wraps every user action in `BeginUserAction()`, and when the outermost one ends with a changed location it pushes the place it started from, filtered by `NavigationCoalescing` (frame moves within one chain collapse unless the user paused `QuietWindow`; tab, chain, sequence or track changes always record). User actions are every pointer press/release and non-chord key press (window-level Tunnel handlers, open until the dispatcher is idle) plus `ActivateTabAsync`, `LoadAnimationFileAsync`, `CloseTabAsync` and `OpenAndSelectChainAsync`. Anything outside an action records nothing; Back/Forward and undo/redo (`MainWindow.Undo`/`Redo`) wrap in `Suppress()`, which also cancels an open action. Route new undo/redo call sites through those wrappers, and never push from selection handlers. A new selection kind needs a field on `NavigationLocation` plus capture, restore and `IsStillPresent` handling in `MainWindow.Navigation.cs`. The macOS system menu bar is a live mirror of the in-window `MainMenu` (`NativeMenuBuilder`, from `MainWindow.CreateNativeMenu()`): headers, nesting, enabled and checked state and gestures (Ctrl becomes Cmd) all come from the in-window items, so add a menu item only in `MainWindow.axaml`. `NativeMenuParityScenarioTests` fails when the two differ. A hotkey whose Mac gesture differs from its first (Redo: Ctrl+Y, Cmd+Shift+Z on Mac) sets `HotkeyDefinition.MacPreferredGesture`; menus and the shortcuts panel pick it through `DisplayTextFor(isMacOS)`. Save As is the `save-as` hotkey (Ctrl/Cmd+Shift+S); `save` forbids Shift so the two do not collide.
