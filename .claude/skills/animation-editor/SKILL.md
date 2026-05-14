---
name: animation-editor
description: "Location and layout of the FlatRedBall2 Animation Editor (Avalonia rewrite). Use when an issue or task references the Animation Editor, AnimationEditor, AnimationEditorAvalonia, .achx editing, the wireframe/preview panels, or anything filed under the `animationeditor` GitHub label. Covers where the source lives, the project layout, and the test setup."
---

# Animation Editor — Location & Layout

The Animation Editor is the desktop tool that lets users edit `.achx` animation chain files (frames, regions, shapes, onion-skinning, preview playback). It is being rewritten on top of Avalonia and lives **inside this repository** at:

```
tools/AnimationEditorAvalonia/
```

> The legacy WinForms version (`FlatRedBall.AnimationEditorForms`) lives in the separate `FlatRedBall` (FRB1) repo at `FRBDK/FlatRedBall.AnimationEditorForms/`. Do **not** edit it for FRB2 issues — that codebase is being replaced. Issues filed in `vchelaru/FlatRedBall2` always refer to the Avalonia version.

## Project layout

```
tools/AnimationEditorAvalonia/
├── AnimationEditorAvalonia.slnx
├── docs/
│   ├── DEVELOPMENT.md            ← read first when starting work
│   └── FEATURE_COVERAGE_REPORT.md
├── src/
│   ├── AnimationEditor.App/      ← Avalonia UI (windows, controls, axaml)
│   │   ├── MainWindow.axaml(.cs)
│   │   ├── Controls/
│   │   │   ├── WireframeControl.cs   ← top panel (texture + frame regions)
│   │   │   └── PreviewControl.cs     ← bottom panel (animation playback)
│   │   ├── Models/, Services/, Assets/
│   └── AnimationEditor.Core/     ← UI-independent logic
│       ├── CommandsAndState/     ← AppState, AppCommands, ApplicationEvents
│       ├── Data/, IO/, Rendering/, ViewModels/
│       └── ProjectManager.cs, SelectedState.cs
└── tests/
    ├── AnimationEditor.App.Tests/    ← headless Avalonia (Avalonia.Headless.XUnit)
    └── AnimationEditor.Core.Tests/   ← pure logic
```

## Build & test

```
dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx
dotnet test  tools/AnimationEditorAvalonia/tests/AnimationEditor.App.Tests/
dotnet test  tools/AnimationEditorAvalonia/tests/AnimationEditor.Core.Tests/
```

## `[AvaloniaFact]` is a last resort

`[AvaloniaFact]` (from `Avalonia.Headless.XUnit`) runs the test on a headless Avalonia UI thread. It is slow and **deadlocks** on anything that blocks the UI thread waiting for the UI — a code path reaching `Window.ShowDialog` hangs forever with nothing to close the dialog. The `MainWindow` constructor also overwrites injected delegates (`AppCommands.ConfirmAsync`, `PromptStringAsync`, `FileDialogService`), so a stub installed *before* construction is silently lost.

Default to a plain `[Fact]` against `AnimationEditor.Core` (`AppCommands`, commands, `SelectedState`, `AppState`). Reach for `[AvaloniaFact]` only when the behavior under test genuinely *is* UI — layout, control templates, input routing, visual tree. Logic reachable only by reflecting into a private `MainWindow` method is a signal to move it into Core, not to write an `[AvaloniaFact]`.

Tests that do need it construct `MainWindow` and drive it with `Dispatcher.UIThread.RunJobs()` between actions — see `WireframePanZoomTests.cs` for the established pattern.

## Two-panel mental model

- **Wireframe (top)** — the texture editor. User loads a sprite sheet, draws/edits frame regions on it. State: pan, zoom, selected frame, snap-to-grid.
- **Preview (bottom)** — the animation player. Plays the selected `AnimationChain` at runtime speed; supports onion skin and origin guides. State: pan, zoom, playback timer, speed multiplier.

Both panels have their own zoom combo box wired in `MainWindow.axaml.cs` (`ZoomCombo` ↔ `WireframeCtrl`, `PreviewZoomCombo` ↔ `PreviewCtrl`). Each control raises a `ZoomChanged` event that `MainWindow` syncs back into the combo using a suppression flag to break the feedback loop. Mirror that pattern for any new bidirectional control ↔ combo wiring.

## Service wiring in tests

Services (`ProjectManager`, `SelectedState`, `AppCommands`, `AppState`, `ApplicationEvents`, `IoManager`, `ObjectFinder`, `UndoManager`) are constructor-injected — no static `Self` accessors, no global state. Production wires them through a `Microsoft.Extensions.DependencyInjection` container in `App.axaml.cs`.

Tests build their own fresh graph per test via `TestHelpers.BuildServices()` (App) / `TestHelpers.SetupFreshAcls()` (Core), which returns a `TestServices` context exposing every service. Tests then address services through that context (`ctx.AppCommands.Foo()`) rather than statics. Each test gets a brand-new graph, so cross-test selection leakage is impossible.

When constructing an Avalonia control directly (`WireframeControl` / `PreviewControl`) — because Avalonia requires a parameterless constructor for XAML — call `ctx.CreateWireframeControl()` / `ctx.CreatePreviewControl()`, which wraps `new WireframeControl()` and `InitializeServices(...)` so the control's injected fields are populated before any method runs.
