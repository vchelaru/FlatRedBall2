---
name: animation-editor-testing
description: Writing headless tests for the Animation Editor (Avalonia). Triggers: AnimationEditor.App.Tests, [AvaloniaFact], TestServices, CreateMainWindow, service wiring in tests, UI-thread RunJobs.
---

# Animation Editor — Testing

Headless-test discipline for the Avalonia Animation Editor. Tool layout, the two-panel mental model, and the `FilePath` path-handling rule live in the **`animation-editor`** skill.

```
dotnet test tools/AnimationEditorAvalonia/tests/AnimationEditor.App.Tests/
dotnet test tools/AnimationEditorAvalonia/tests/AnimationEditor.Core.Tests/
```

## `[AvaloniaFact]` is a last resort

`[AvaloniaFact]` (from `Avalonia.Headless.XUnit`) runs the test on a headless Avalonia UI thread. It is slow and **deadlocks** on anything that blocks the UI thread waiting for the UI — a code path reaching `Window.ShowDialog` hangs forever with nothing to close the dialog. The `MainWindow` constructor also overwrites injected delegates (`AppCommands.ConfirmAsync`, `PromptStringAsync`, `FileDialogService`), so a stub installed *before* construction is silently lost.

Default to a plain `[Fact]` against `AnimationEditor.Core` (`AppCommands`, commands, `SelectedState`, `AppState`). Reach for `[AvaloniaFact]` only when the behavior under test genuinely *is* UI — layout, control templates, input routing, visual tree. Logic reachable only by reflecting into a private `MainWindow` method is a signal to move it into Core, not to write an `[AvaloniaFact]`.

Tests that do need it construct `MainWindow` and drive it with `Dispatcher.UIThread.RunJobs()` between actions — see `WireframePanZoomTests.cs` for the established pattern.

## Reflection-invoking a handler proves the logic, not that input reaches it

`GetMethod(..., NonPublic).Invoke(window, [sender, fakeArgs])` calls a handler directly, bypassing Avalonia's routed-event dispatch — it proves the handler's body is correct, not that real input ever reaches it. This missed a real bug (#716): `OnAnimTreeDoubleTapped` was Bubble-registered on `AnimTree.DoubleTapped` and looked correct under reflection, but a real double-click on a `TreeViewItem` row never fired it — the control's own Tunnel-phase pointer handling toggles `IsExpanded` on the second click first, and that native behavior isn't visible from reading the handler's source. The fix had to move into `OnTreePointerPressed`'s existing Tunnel-phase `PointerPressed` branch instead.

When the question is *whether* a gesture reaches a handler — not just what it does once it's there — drive it for real: `window.MouseDown(point, MouseButton.Left)` / `MouseUp` (twice for a double-click, both from `Avalonia.Headless`), with `point` computed from a real control's `Bounds` via `TranslatePoint`. Reflection is fine for asserting the body once the real path is confirmed; it does not substitute for confirming that path exists.

The same trap applies to directly assigning `ISelectedState` properties (`ctx.SelectedState.SelectedChain = chain`) instead of clicking the tree: a real click also runs `MainWindow.OnTreeSelectionChanged`, which syncs `AnimTree.SelectedItems` into `SelectedState.SelectedNodes` as a side effect — so `SelectedChains` is never empty after a real click, even a plain single-click single-selection. A follow-up bug on #716 slipped through exactly this way: a direct-assignment test asserted the reveal fired correctly, but a real click left every frame `IsSelected=false` because the selection-derived `SelectedChains` (populated only by the real click's `SelectedNodes` sync, not by direct assignment) fed a branch the direct-assignment test never exercised. When a test's setup is "select this thing," prefer clicking the real `TreeViewItem`/header label over setting the model field directly — the click's side effects on sibling `SelectedState` fields are often exactly what's under test.

A third #716 facet: two `window.MouseDown`/`MouseUp` pairs at the same point back-to-back register as `ClickCount==2` (a double-click), not two independent single clicks — `Thread.Sleep` past the OS double-click window (or click a different point) between them if the test needs two genuine single clicks on the same target. Separately, any content-based dedup keyed off "did the selected *set* change" (e.g. comparing frame lists for a reveal-restart check) silently swallows a click that reselects the *same* already-selected item, since the resulting set is identical — even though the click itself is a real, distinct user action that should still fire whatever "just happened" behavior (a replay, a re-focus). If a feature must fire on every click regardless of whether the underlying model value actually changed, drive it from the click handler directly, not from a diff against previous selection state.

A fourth: `window.MouseDown`/`MouseUp` fully pump any `Dispatcher.UIThread.InvokeAsync`-queued continuation before returning (no `RunJobs()` needed) — so a test cannot freeze-frame the gap between a synchronous Tunnel-phase click handler and an async `SelectionChanged` reaction the way a real 60fps render loop can catch mid-flight (exactly the #716 flash: an unconditional reveal-restart call at the click site reset progress on the *previous* selection's still-rendered frames a beat before the async catch-up moved the highlight). Guard fixes for this class of race by asserting the *condition* that prevents the early call (e.g. "only replay when the clicked item already equals the current selection"), not by trying to observe the intermediate state headless can't expose. Also: `Console.WriteLine` from application code is invisible in `dotnet test` output even on failure; `System.IO.File.AppendAllText` to a scratch path is the reliable way to trace through a headless run when reasoning alone isn't converging.

## Undo labels vs screenshots

- **Correctness of a command's `Description`:** assert on the command (or `UndoManager.UndoHistory` after `AppCommands`) in Core.Tests — see `CommandDescriptionTests` / `FeatureDemosTests`.
- **"Show me the History panel":** DocScreenshots / browser verify — **`animation-editor-screenshots`** and **`animation-editor-browser-verify`**. Those drive the same `FeatureDemos` helpers; they do not replace unit asserts.

Never seed History UI models with hand-written strings to "prove" a label.
## Service wiring in tests

Services (`ProjectManager`, `SelectedState`, `AppCommands`, `AppState`, `ApplicationEvents`, `IoManager`, `ObjectFinder`, `UndoManager`) are constructor-injected — no static `Self` accessors, no global state. Production wires them through a `Microsoft.Extensions.DependencyInjection` container in `App.axaml.cs`.

Tests build their own fresh graph per test via `TestHelpers.BuildServices()` (App) / `TestHelpers.SetupFreshAcls()` (Core), which returns a `TestServices` context exposing every service. Tests then address services through that context (`ctx.AppCommands.Foo()`) rather than statics. Each test gets a brand-new graph, so cross-test selection leakage is impossible.

When constructing an Avalonia control directly (`WireframeControl` / `PreviewControl`) — because Avalonia requires a parameterless constructor for XAML — call `ctx.CreateWireframeControl()` / `ctx.CreatePreviewControl()`, which wraps `new WireframeControl()` and `InitializeServices(...)` so the control's injected fields are populated before any method runs.

Assign `ProjectManager.AnimationChainListSave` **after** `window.Show()` (+ a `RunJobs()`), not before: `MainWindow.OnOpened` resets it to a fresh empty `AnimationChainListSave` when there's no CLI file / saved tabs, silently discarding a project set before the window opened. A pre-`Show` assignment leaves your chain orphaned, so `SelectedState.SelectedFrame`'s parent-chain lookup (`FindChainForFrame`) returns null and `SelectedChain` comes back null even though the frame is in the chain. Selecting only `SelectedChain` masks this (it doesn't consult the project). Assigning the project repopulates the model but not the tree view; for tree-dependent behavior (selection routing, the `SyncTreeSelection`→`OnTreeSelectionChanged` re-entrancy) reflect-invoke the private `RebuildTreeView` afterward — see `TimelineStripRebuildTests`.

## Names inside an extracted `UserControl` are invisible to `window.FindControl`

A `UserControl` (e.g. `ZoomControl`) defines its **own** namescope, so `window.FindControl<T>("Combo")` — from a test or from `MainWindow` — cannot resolve a control named *inside* it; only names in the same namescope resolve. Test an extracted control through its **public surface**: fetch the control itself (`window.FindControl<ZoomControl>("PngZoom")`) and assert on public members it exposes (`ZoomControl.Text`, `.StepUp()`, `.StepDown()`). This is the tax on pulling a widget into a reusable control — any test that reached it by an inner element's name must re-target the wrapper's public API.

## Tests must never write the developer's real settings

`MainWindow` persists app settings (recent files, open tabs, theme) to `%APPDATA%\AnimationEditor\AESettings.json` in its `Closed` handler. A headless test that constructs and closes a window would otherwise overwrite the developer's real settings with test fixtures. The application-data root is a `MainWindow` constructor parameter precisely so tests can redirect it: `ctx.CreateMainWindow()` passes `ctx.SettingsRoot` (a unique temp dir), while production (`App.axaml.cs`) passes `Environment.GetFolderPath(SpecialFolder.ApplicationData)`. Build the window through `ctx.CreateMainWindow()` — never reconstruct one with the production root in a test. General rule: any component that reads or writes a real per-user location (config, registry, recent-files) takes its root as an injected dependency, so tests land in temp and never on real user data.
