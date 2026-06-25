---
name: animation-editor
description: FlatRedBall2 Animation Editor (Avalonia) — where the source lives, project layout, and the two-panel model. Triggers: AnimationEditor, AnimationEditorAvalonia, .achx editing, wireframe/preview panels, animationeditor label.
---

# Animation Editor — Location & Layout

The Animation Editor is the desktop tool that lets users edit `.achx` animation chain files (frames, regions, shapes, onion-skinning, preview playback). It is being rewritten on top of Avalonia and lives **inside this repository** at:

```
tools/AnimationEditorAvalonia/
```

> The legacy WinForms version (`FlatRedBall.AnimationEditorForms`) lives in the separate `FlatRedBall` (FRB1) repo at `FRBDK/FlatRedBall.AnimationEditorForms/`. Do **not** edit it for FRB2 issues — that codebase is being replaced. Issues filed in `vchelaru/FlatRedBall2` always refer to the Avalonia version.

For writing tests against the editor — headless Avalonia, service wiring, the `[AvaloniaFact]` deadlock pitfall — see the **`animation-editor-testing`** skill.

## `.achx` is a general-purpose format — the editor authors, runtimes interpret

`.achx` is **not** an FRB2 file. It is a general-purpose animation/atlas format consumed by several runtimes that each render it their own way: Gum (across its Skia, raylib, and sokol.net backends), MonoGame/KNI/FNA, FRB1 (custom-shader rendering), and FRB2 (`SpriteBatch`). The editor authors the *format*; each runtime decides what to do with the data. This frames every feature decision here:

- **A field the editor exposes does not obligate any runtime to apply it.** Store the data in the format; whether a given runtime renders it is that runtime's choice. Do not gate adding a frame field on FRB2 (or any single runtime) implementing it — e.g. per-frame `Red`/`Green`/`Blue` are authored and stored for game code to consume, while FRB2's `SpriteBatch` path never applies them itself.
- **The preview is a reference rendering, not a per-runtime contract.** The bottom panel renders with SkiaSharp (`PreviewControl`, `SKCanvas`/`SKColorFilter` in `DrawFrameCore`), so it will diverge from what a MonoGame/FNA/FRB1 runtime produces for the same file. That divergence is inherent to a general-purpose tool and is not a bug — pick a sensible canonical interpretation. "The preview might not match a runtime" is never a reason to withhold an authoring feature.

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

## Build

```
dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx
```

Test commands and headless-test discipline live in the `animation-editor-testing` skill.

## Two-panel mental model

- **Wireframe (top)** — the texture editor. User loads a sprite sheet, draws/edits frame regions on it. State: pan, zoom, selected frame, snap-to-grid.
- **Preview (bottom)** — the animation player. Plays the selected `AnimationChain` at runtime speed; supports onion skin and origin guides. State: pan, zoom, playback timer, speed multiplier.

Both panels have their own zoom combo box wired in `MainWindow.axaml.cs` (`ZoomCombo` ↔ `WireframeCtrl`, `PreviewZoomCombo` ↔ `PreviewCtrl`). Each control raises a `ZoomChanged` event that `MainWindow` syncs back into the combo using a suppression flag to break the feedback loop. Mirror that pattern for any new bidirectional control ↔ combo wiring.

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
