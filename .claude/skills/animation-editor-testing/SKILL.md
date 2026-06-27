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

## Service wiring in tests

Services (`ProjectManager`, `SelectedState`, `AppCommands`, `AppState`, `ApplicationEvents`, `IoManager`, `ObjectFinder`, `UndoManager`) are constructor-injected — no static `Self` accessors, no global state. Production wires them through a `Microsoft.Extensions.DependencyInjection` container in `App.axaml.cs`.

Tests build their own fresh graph per test via `TestHelpers.BuildServices()` (App) / `TestHelpers.SetupFreshAcls()` (Core), which returns a `TestServices` context exposing every service. Tests then address services through that context (`ctx.AppCommands.Foo()`) rather than statics. Each test gets a brand-new graph, so cross-test selection leakage is impossible.

When constructing an Avalonia control directly (`WireframeControl` / `PreviewControl`) — because Avalonia requires a parameterless constructor for XAML — call `ctx.CreateWireframeControl()` / `ctx.CreatePreviewControl()`, which wraps `new WireframeControl()` and `InitializeServices(...)` so the control's injected fields are populated before any method runs.

Assign `ProjectManager.AnimationChainListSave` **after** `window.Show()` (+ a `RunJobs()`), not before: `MainWindow.OnOpened` resets it to a fresh empty `AnimationChainListSave` when there's no CLI file / saved tabs, silently discarding a project set before the window opened. A pre-`Show` assignment leaves your chain orphaned, so `SelectedState.SelectedFrame`'s parent-chain lookup (`FindChainForFrame`) returns null and `SelectedChain` comes back null even though the frame is in the chain. Selecting only `SelectedChain` masks this (it doesn't consult the project).

## Tests must never write the developer's real settings

`MainWindow` persists app settings (recent files, open tabs, theme) to `%APPDATA%\AnimationEditor\AESettings.json` in its `Closed` handler. A headless test that constructs and closes a window would otherwise overwrite the developer's real settings with test fixtures (issue #438). The application-data root is a `MainWindow` constructor parameter precisely so tests can redirect it: `ctx.CreateMainWindow()` passes `ctx.SettingsRoot` (a unique temp dir), while production (`App.axaml.cs`) passes `Environment.GetFolderPath(SpecialFolder.ApplicationData)`. Build the window through `ctx.CreateMainWindow()` — never reconstruct one with the production root in a test. General rule: any component that reads or writes a real per-user location (config, registry, recent-files) takes its root as an injected dependency, so tests land in temp and never on real user data.
