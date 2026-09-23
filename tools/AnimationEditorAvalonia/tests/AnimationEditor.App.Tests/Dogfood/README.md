# Dogfooding the Animation Editor headlessly

This folder is about one thing: the Avalonia Animation Editor as a user works it. It drives the
real `MainWindow` with simulated clicks, double-clicks, right-clicks, drags, wheel and key input
inside an in-process headless window. Nothing reaches the desktop, so it is safe to run while the
machine is in use. Use it to find bugs the way a user would hit them, pin each one with a test,
fix it, and keep the scenario as the regression guard.

The rest of `AnimationEditor.App.Tests` mostly tests one control or one handler at a time, often
by calling the control's `Simulate*` methods or reflecting into a private method. The scenarios
here go through the window instead, so pointer routing, focus, dialogs, auto-save and the tab
strip are all part of what is tested.

## Run it

```
dotnet test tools/AnimationEditorAvalonia/tests/AnimationEditor.App.Tests --filter "FullyQualifiedName~Dogfood"
```

About 145 scenarios, roughly 55 seconds.

## The pieces

| File | Role |
|---|---|
| `AnimationEditorHarness.cs` | Hosts a real `MainWindow` on a fresh `TestServices` graph over a temp project folder. Fixtures, gestures, lookups (tree rows, tabs, wireframe geometry, inspector fields), undo labels, notifications. |
| `ScriptedDialogs.cs` | Answers the dialogs the editor opens through its seams (confirm, string prompt, Save / Don't Save / Cancel, open and save file pickers). An unanswered dialog fails the scenario at the next `Layout()`. |
| `*ScenarioTests.cs` | One file per area: chain list, chain menu and multi-select, frames, wireframe, grid and magic wand, shapes, tabs, playback, rename and search, external changes, history, untitled documents, everyday editing, edge cases, exploratory QA. |

## Write a scenario

Every scenario follows the same shape:

```csharp
[AvaloniaFact]
public async Task DeleteKey_RemovesTheSelectedChain_AndCtrlZBringsItBack()
{
    using AnimationEditorHarness editor = new AnimationEditorHarness();
    editor.WritePng("sheet.png", 64, 64);
    string path = editor.WriteAchx("hero.achx",
        AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
        AnimationEditorHarness.Chain("Run", "sheet.png", (32, 0, 16, 16)));
    await editor.OpenAsync(path);
    AnimationChainSave run = editor.ChainNamed("Run");
    editor.ClickRow(run);

    editor.Press(Key.Delete);

    editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk" });
    editor.DeletedToastText.ShouldBe("\"Run\" deleted");
    editor.Press(Key.Z, RawInputModifiers.Control);
    editor.Project.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Walk", "Run" });
}
```

Rules that keep scenarios honest:

- Open fixtures through the real load path (`OpenAsync`), then look the loaded objects up
  (`ChainNamed`, `Project`): the editor loads fresh objects from disk, so a fixture's own
  instances are never the ones the tree shows.
- Drive the gesture the user would use, not the view model or a `Simulate*` method: `ClickRow`,
  `DoubleClickRow`, `RightClickRow` + `PickTreeMenuItem`, `Expand`, `RowButton`, `Click`,
  `ClickAt`, `DoubleClickAt`, `Drag`, `Wheel`, `Press`, `Type`, `TypeNumber`, `TypeFlanker`,
  `TypeText`, `ClickMenu`, `ClickTab`, `CloseTab`. `WireframeRectOf` and `WireframePointAt` give
  window points on the texture panel; `PixelRectOf` reads a frame back in pixels.
- Queue every dialog answer before the gesture that opens it: `Dialogs.AnswerNextConfirm`,
  `AnswerNextPrompt`, `AnswerNextSaveDiscardCancel`, `AnswerNextSaveFile`, `AnswerNextOpenFile`.
- Assert on more than one surface where they apply: the model (`Project`, `Services.SelectedState`),
  the controls (`AnimTree`, `Nodes`, `VisibleChainHeaders`, `Wireframe`, `Preview`, `TabLabels`,
  `HistoryRows`, any control by name through `Control<T>`), the undo stack (`UndoLabels`,
  `UndoManager`), the notifications (`ErrorBannerText`, `ToastText`, `DeletedToastText`) and the
  saved file (`ReadSaved`). A bug often shows in only one of them.
- Anything that needs a timer to tick (preview playback, the smooth wheel zoom, toast auto-hide)
  goes through `WaitAsync` / `WaitUntilAsync`. The synchronous `Wait` / `WaitUntil` only pump
  queued dispatcher jobs; see Gotchas.
- Call `ThrowIfErrorShown` after a gesture that could fail inside a guarded action: copy, cut and
  paste route exceptions into the error banner, which otherwise looks like a silent no-op.

## Fix loop

1. Add the scenario for a gesture or situation nobody has tried. Run it.
2. When it fails, decide whether the harness, your expectation, or the editor is wrong. Read the
   handler before deciding: three of the first eleven failures here were expectations (the editor
   auto-saves file-backed documents, a chain-selected wireframe click grabs the chain for a drag, a
   selected frame hides its siblings' boxes).
3. For an editor bug, write the red test at the layer that owns it: pure logic in
   `AnimationEditor.Core.Tests`, a single control in `AnimationEditor.Views.Tests`, window wiring
   stays covered by the scenario here.
4. Fix, run the scenario, then run this folder and the whole assembly to catch leaked state.
5. Commit the scenario and the fix together.

## First-pass triage (September 2026)

The first 56 scenarios failed 11 times on their first run. Only two were editor bugs; the rest
were the harness or the author's expectations, and every scenario survived, corrected. Kept here
so the next pass knows which "failures" to expect from the editor's real behaviour.

| Scenario that failed | Cause | Outcome |
|---|---|---|
| Enable Hot Reload menu toggle | **Editor bug**: the MenuItem had no `ToggleType`, a click never unchecked it | Fixed (`ToggleType="CheckBox"`); scenario is the guard |
| Save As on a file-backed tab | **Editor bug**: the tab kept the old path while the document moved | Fixed (`SaveAsCompleted` renames the tab); scenario is the guard |
| Close dirty tab, Don't Save / Cancel | Expectation: file-backed documents auto-save, so no prompt | Rewritten around auto-save, plus three Untitled-tab prompt scenarios |
| Click a frame box with the chain selected | Expectation: that press grabs the chain for a drag (#719) | Rewritten; double-click and no-undo scenarios added |
| Ctrl+wheel zoom, play button, loop off | Harness: `DispatcherTimer`s tick only while the test awaits | `WaitAsync` / `WaitUntilAsync` added |
| Search box filter | Expectation: filtered rows are hidden (`PinnedVisible`), not removed | `VisibleChainHeaders` added |
| History rows after inspector edits | Author error: the History tab hides the inspector | Reordered; `TypeFlanker` now fails loudly on a hidden field |
| Double-click a chain label to rename | Harness limit: text is not hit-testable headlessly | Replaced by the row double-click (fit to view) scenario |

## Third-pass findings (bug hunt, September 2026)

Twenty scenarios written to ask "what should it do" and "what should it not do" rather than to
cover code. Three failed; one was fixed, two are recorded here as open questions because the
editor is consistent about them and changing them is a design call.

| Finding | Kind | Outcome |
|---|---|---|
| "Match Frame Size" on a rectangle moved it to the frame's offset but never sized it | **Should, but didn't** | Fixed: it now also sets the scale to half the frame's pixel size (Core tests in `AppCommandsShapeTests`); the size is left alone when the texture cannot be read |
| Typing a pixel X of 500 on a 64 px texture puts the frame entirely off the texture (UV 8.06); the inspector allows up to 16384 and the handle drag does not clamp either | Does, arguably shouldn't | Open: both paths agree, so clamping is a design decision, not a one-line fix. Scenario removed |
| Renaming a chain to another chain's name in a different case ("run" beside "Run") is accepted | Does, arguably shouldn't | Open: the runtime lookup is ordinal, so the names are distinct today; a case-insensitive file or lookup would collide. Scenario removed |

Behaviours the same pass confirmed as correct: negative frame lengths are refused, a zero
frame length neither hangs playback nor errors, a zero pixel width keeps one pixel, a
whitespace-only name is refused with a banner, a name with spaces and non-ASCII characters
round-trips through save, deleting the open file on disk shows a toast and the next edit
recreates it, cut/paste moves a frame between chains, copy/paste moves a chain between tabs,
Ctrl+Y and Ctrl+Shift+Z both redo, Delete Frame acts on the right-clicked row, Ctrl+D on a
rectangle duplicates it with its own name, Flip Vertically and Invert Frame Order undo one at a
time, the preview toggles follow the toolbar, and the recent-files menu focuses an open tab
instead of opening it twice.

## Fourth-pass findings (exploratory QA, September 2026)

Twenty-seven scenarios in the "what would a real tester try" spirit: actions in odd orders,
hotkeys with focus in the wrong place, junk typed into fields, keys hammered, edits during
playback, a drag cancelled with Escape, a corrupt file, a missing texture, a locked chain, a
tiny window, Tab-key traversal, ten tab switches in a row.

| Finding | Kind | Outcome |
|---|---|---|
| A locked chain's context menu still offered "Add Frame" and "Add Multiple Frames…", both silently inert (the row's + button is hidden, and `AddFrame` refuses) | Does, but shouldn't | Fixed in `TreeMenuPlanBuilder` (red Core test first): a locked chain's menu offers neither |
| An `.achx` whose PNG is missing takes edits but never saves them; the toast and status say "Auto Save Failed" and why | Looked like a bug, is by design | A pixel-coordinate file cannot be written without the texture size; scenario now asserts the refusal is explained and the file is untouched |
| The first frame added to an empty chain has no texture, even when the project has one the wireframe is already showing | Should, arguably | Open: `AddFrame` inherits from a sibling frame or the canvas; on an empty chain with the canvas borrowing a texture it still lands empty. Small UX win if the canvas texture were used |
| Ctrl+Z right after a menu action (Save As) did nothing headlessly | Harness | Focus stayed on the closed popup's item, whose key events go to the popup's own top level; `ClickMenu` now hands focus back to a tree row. Worth one manual check on the desktop that Ctrl+Z works immediately after a menu action |

Confirmed correct: two Ctrl+N give distinct untitled names, undo/redo with nothing to do is
silent, Delete inside an inspector box edits text not the frame, Delete twice deletes two chains
and two undos restore both, deleting the playing chain stops the preview, three duplicates get
three names, Escape during a handle drag leaves either the original or an undoable edit, junk in
the frame-length box changes nothing and records no undo, a grid size of 0 does not break the
wireframe, re-opening the open file focuses its tab and keeps the edit and its undo history, an
empty clipboard pastes nothing quietly, a name with a trailing space is not a rename, renaming
during playback keeps playing, the row's + button on an unselected chain targets that chain,
Space in the search box types a space, tab switching keeps per-tab selection and undo stacks,
undo in one tab never touches another, 20 wheel notches each way stay finite, Alt+Up at the top
records nothing, an empty chain plays/scrubs/takes a frame, a locked chain refuses inspector
edits and pasted frames, a corrupt file is reported and the editor stays usable, Save As keeps
the undo history, Tab moves between inspector fields, and a 400x300 window still takes edits.

## Fifth-pass findings (panels, group preview, races, September 2026)

Nineteen scenarios on surfaces nobody had driven: the Project folder panel and its preview
tabs, the group preview, the PNG tab, the Shortcuts tab, the theme across a restart, Close
Project, bulk inspector edits, the colour fields, edits racing a hot reload, the search filter
against selection and rename, deep copies, sixty frames, very long names, Space on a focused
toggle.

| Finding | Kind | Outcome |
|---|---|---|
| A cross-tab cut/paste removed the chain from the source document in memory but never wrote the source file; closing, reopening or hot-reloading that tab brought the chain back, so the project ended up with it twice | **Should, but didn't** | Fixed: the paste that completes a cross-document cut now saves the source document to its own file in its own disk format (`IProjectManager.SaveAnimationChainList(document, path, format)`, `IAppCommands.SaveDocument`); red Core tests in `ProjectManagerSaveTests` and `AppCommandsSaveDocumentTests`, scenario covers the paste path |
| Adding frames with the row's + button auto-scrolls the tree to the new frame, so after a dozen frames the + button has scrolled out from under the pointer | Does, arguably shouldn't | Open UX note; the harness scrolls a row into view before clicking it, as a user would |
| A rename does not re-apply the search filter; the renamed row keeps its old visibility until the box changes | Does, arguably shouldn't | Open, harmless: the renamed row is selected and a selected row is always shown anyway |
| Cut looked like it did nothing | Expectation | Cut is a pending move, as in a file manager: the source stays until the paste lands |
| Delete after typing a filter looked like it deleted a hidden row | Expectation | A selected row stays visible under any filter (by design), and with focus still in the search box Delete edits the text; the scenario now clicks the row first |
| The Shortcuts list looked short | Expectation | It is grouped by category; 5 groups holding all the hotkeys |

Confirmed correct: the Project panel opens a single-clicked file as a preview tab, replaces
that preview on the next click, keeps it on double-click and promotes it on the first edit;
Ctrl+click on two chains shows the group preview and a plain click leaves it; the PNG tab and
the editor pane swap cleanly; the theme menu applies at once and survives a restart; Close
Project clears tabs and the panel; a bulk frame-length edit sets every selected frame in one undo
step and mixed values show "(mixed)"; colour mode and red reach the file; an inline rename racing
a hot reload ends consistent with disk; undo across a reload leaves the tree matching the model;
a duplicated chain owns its shapes; sixty Add Frame clicks all land; a 300-character name
round-trips; Space on a focused toggle button toggles the button, not playback.

## Sixth-pass findings (dialogs, September 2026)

The dialogs that open through `EditorDialogs` got a scripting seam: `MainWindow` takes an
optional `IEditorDialogHost` (production passes nothing and keeps its window host), and
`ScriptedDialogs` implements it by mounting the dialog's content in a headless window, handing
it to the scenario's `AnswerNextEditorDialog` callback, then pressing OK or Cancel. Eight
scenarios: Adjust Frame Time (Keep Proportional, Set All Frames Same, and cancelled after live
edits), Add Multiple Frames (three with Increment UV, and cancelled), Adjust Offsets (Justify
Bottom, Adjust All absolute then relative), and the Files tab. All eight passed on the first run:
live edits preview and roll back on Cancel with no undo entry, a confirmed dialog collapses to
one undo step, the batch add lands as one step, and the offsets land on every frame.

The seam is the only test-motivated change to production code on this branch, and it follows
the window's existing injection of its settings root and updater; no `*ForTest` methods or
`Simulate*` hooks were added.

## Seventh-pass findings (tsx, achj, timeline strip, September 2026)

The last document kinds and the last panel nobody had driven. Nine scenarios, all passed on the
first run: a Tiled `.tsx` opens natively with its two-frame animation on the tileset image, its
grid cannot be turned off, the inspector hides the transform and colour sections and disables
Loop, the context menus offer no flips, shapes or offsets, a frame-length edit writes
`duration="250"` back into the tileset, resizing one frame gives every frame of the chain the same
footprint (and one undo restores them all), deleting the chain and undoing round-trips through
the tileset, and typing a free owner tile moves the animation to that tile. An `.achj` opens,
takes a rectangle and a colour, and auto-saves as JSON; Save As from `.achx` to `.achj` writes
JSON and later edits go there. The timeline strip's playhead follows a scrub at either end.

With this pass every headless-reachable surface of the editor has at least one scenario. What
remains is on the real-window list below.

## Needs real input: the non-headless list

Things this harness cannot exercise, or can only approximate, kept here so a real-window pass
knows where to look. Add to it whenever a scenario has to route around something.

| Area | Why headless cannot see it | What a real-window pass should do |
|---|---|---|
| Double-tap on a row's text label (inline rename) | Text is not hit-testable headlessly; the press lands on the row | Double-click chain labels, frame labels (must centre, not rename), shape labels |
| Focus after a popup or menu closes | The closed popup's item kept the keyboard; `ClickMenu` hands focus back by hand | Pick any menu item, press Ctrl+Z / Delete / Space immediately |
| Double-click timing, drag threshold, pointer capture lost mid-drag | Headless raises events with no OS timing; capture-lost never happens | Slow double-clicks, tiny drags below the threshold, alt-tab mid-drag |
| Smooth zoom, selection reveal, toast auto-hide, playback cadence | Timers tick only while the test awaits; no 60 fps loop | Wheel-zoom feel, reveal animation, toast racing a click on its button |
| Anything drawn | `UseHeadlessDrawing = true`: no pixels; nothing here asserts on rendering | Handles, overlapping-frame highlight, onion skin, guides, grid, PNG diff view, timeline thumbnails, theme colours |
| Tree drag-and-drop reorder (chains, frames), tab reorder by drag | Avalonia `DragDrop` needs a platform drag source | Drag rows above/below/into, drag tabs, drop a PNG from Explorer onto the wireframe and onto the tree |
| Native dialogs: File > Load, Resize Texture, About, Settings | Load calls `StorageProvider` directly; the others build their own `Window` and `ShowDialog` it, bypassing `IEditorDialogHost` (Adjust Frame Time, Add Multiple Frames and Adjust Offsets do go through the host and are scripted with `AnswerNextEditorDialog`) | Open each, Enter/Escape, Tab order inside them, cancel leaves nothing changed; Resize Texture rewrites the PNG, so check the UVs afterwards |
| Clipboard with other apps, file association, single-instance handoff, Velopack update, crash recovery on next launch | Stubbed or process-level | Paste from another editor instance, double-click an `.achx` in Explorer with the editor open, kill and relaunch |
| DPI scaling, multi-monitor, window restore position, macOS Dock/menu | Platform | Move between monitors with different scaling, restart |
| Cursor changes (add-frame cursor on Ctrl-hover, handle cursors, hand over tabs) | Cursor is set but never observed | Hover every handle and the tabs with and without Ctrl |
| Keyboard navigation feel: arrow keys in the tree with collapsed nodes, Home/End, typing to select | Only Down is covered | Walk the tree with the keyboard only |

## Gotchas

- **Hit-testing is a render-time thing.** Pointer input is hit-tested against the composition
  tree, which updates only on a render tick. `Layout()` calls
  `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` after `UpdateLayout` for that reason; without
  it a row realized since the last tick hit-tests as its scroll presenter and a click "does not
  land". Call `Layout()` (every gesture does) before computing a point from `Bounds`.
- **Text is not hit-testable.** A `TextBlock` with no background hit-tests as nothing, so a
  click at a row label's centre lands on the row's panel. That is fine for selecting, and the
  chain-row double-click (fit to view) is reachable, but the label-only double-tap that starts an
  inline rename is not: drive rename through F2, the context menu's "Rename…", or the existing
  `MainWindow.HandleHeaderTextDoubleTap` seam. Confirmed with real Skia drawing too, so it is not
  the no-op drawing mode.
- **Dispatcher timers tick only while the test yields.** Under `Avalonia.Headless.XUnit` 12,
  `Thread.Sleep` + `Dispatcher.UIThread.RunJobs()` runs queued jobs but never fires a
  `DispatcherTimer`; `await Task.Delay` does (the test body runs on the dispatcher, which is free
  during the await). Scenarios are `async Task` for that reason; `WaitAsync` / `WaitUntilAsync`
  loop on `Task.Delay(10)`.
- **File-backed documents auto-save on every edit** (`StatusSaveLabel` shows "Auto Save On").
  Closing such a tab never prompts and Ctrl+S is a no-op for them; only an Untitled tab with
  content asks Save / Don't Save / Cancel. Use File > New with the save dialog cancelled
  (`AnswerNextSaveFile(null)`) to get a document that is not auto-saved.
- **In memory the coordinates are UV.** A pixel-coordinate `.achx` (what `WriteAchx` writes)
  loads as 0..1 UV and converts back on save; `PixelRectOf` does the arithmetic against the
  wireframe's bitmap size.
- **File > Load cannot be scripted.** `MainWindow.LoadAsync` calls `StorageProvider` directly
  rather than `IFileDialogService`, so Ctrl+L opens nothing headlessly. Open files with
  `OpenAsync`; Save As, File > New and the close-tab prompt do go through the seams.
- **The sidebar tabs replace each other.** Clicking the History tab hides the inspector, so
  `TypeNumber` / `TypeFlanker` throw with "not visible" until the Inspector tab is clicked again.
- **The window's constructor wires the production dialogs**, replacing anything assigned to
  `AppCommands.ConfirmAsync` / `PromptStringAsync` / `FileDialogService` beforehand. The harness
  installs `ScriptedDialogs` after `Show()`; do the same for any extra seam.
- **Dialogs opened straight through `EditorDialogs`** (Adjust Frame Time, Add Multiple Frames,
  Adjust Offsets, About, Settings) do not go through a seam and would open a real headless window
  that nothing closes. There is no scripted path for them yet; a scenario that needs one should
  add a seam first.
- Keep per-user state out of the developer's profile: the harness passes its own temp
  `SettingsRoot`; give a second harness the same root to simulate a restart.
