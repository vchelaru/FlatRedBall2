---
name: animation-editor
description: "Location and layout of the FlatRedBall2 Animation Editor (Avalonia rewrite). Use when an issue or task references the Animation Editor, AnimationEditor, AnimationEditorAvalonia, .achx editing, the wireframe/preview panels, or anything filed under the `animationeditor` GitHub label. Covers where the source lives, the project layout, and the test setup."
---

# Animation Editor — Location & Layout

The Animation Editor is the desktop tool that lets users edit `.achx` animation chain files (frames, regions, shapes, onion-skinning, preview playback). It is being rewritten on top of Avalonia and lives **inside this repository** at:

```
FRBDK/AnimationEditorAvalonia/
```

> The legacy WinForms version (`FlatRedBall.AnimationEditorForms`) lives in the separate `FlatRedBall` (FRB1) repo at `FRBDK/FlatRedBall.AnimationEditorForms/`. Do **not** edit it for FRB2 issues — that codebase is being replaced. Issues filed in `vchelaru/FlatRedBall2` always refer to the Avalonia version.

> The `FRBDK/` folder is expected to move to a top-level `tools/` folder later. If `FRBDK/AnimationEditorAvalonia/` no longer exists, look under `tools/`.

## Project layout

```
FRBDK/AnimationEditorAvalonia/
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
dotnet build FRBDK/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx
dotnet test  FRBDK/AnimationEditorAvalonia/tests/AnimationEditor.App.Tests/
dotnet test  FRBDK/AnimationEditorAvalonia/tests/AnimationEditor.Core.Tests/
```

App tests use `[AvaloniaFact]` from `Avalonia.Headless.XUnit`; they construct `MainWindow` and drive it with `Dispatcher.UIThread.RunJobs()` between actions. See `WireframePanZoomTests.cs` for the established pattern (singletons reset, tmp-dir PNG fixture, `FindCtrl<T>` helper).

## Two-panel mental model

- **Wireframe (top)** — the texture editor. User loads a sprite sheet, draws/edits frame regions on it. State: pan, zoom, selected frame, snap-to-grid.
- **Preview (bottom)** — the animation player. Plays the selected `AnimationChain` at runtime speed; supports onion skin and origin guides. State: pan, zoom, playback timer, speed multiplier.

Both panels have their own zoom combo box wired in `MainWindow.axaml.cs` (`ZoomCombo` ↔ `WireframeCtrl`, `PreviewZoomCombo` ↔ `PreviewCtrl`). Each control raises a `ZoomChanged` event that `MainWindow` syncs back into the combo using a suppression flag to break the feedback loop. Mirror that pattern for any new bidirectional control ↔ combo wiring.

## Singleton reset (test gotcha)

`ProjectManager.Self`, `SelectedState.Self`, `AppCommands.Self`, `AppState.Self`, and `ApplicationEvents.Self` are process-wide singletons. Headless tests must reset them in a helper at the top of each test (`ResetSingletons()`) — otherwise the previous test's selection leaks in and you'll chase phantom failures.
