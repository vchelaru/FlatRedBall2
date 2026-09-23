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

About 55 scenarios, roughly 25 seconds.

## The pieces

| File | Role |
|---|---|
| `AnimationEditorHarness.cs` | Hosts a real `MainWindow` on a fresh `TestServices` graph over a temp project folder. Fixtures, gestures, lookups (tree rows, tabs, wireframe geometry, inspector fields), undo labels, notifications. |
| `ScriptedDialogs.cs` | Answers the dialogs the editor opens through its seams (confirm, string prompt, Save / Don't Save / Cancel, open and save file pickers). An unanswered dialog fails the scenario at the next `Layout()`. |
| `*ScenarioTests.cs` | One file per area: chain list, frames, wireframe, shapes, tabs, playback, rename and search, external changes, history, untitled documents. |

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
