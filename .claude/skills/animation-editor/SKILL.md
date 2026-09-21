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

For writing tests against the editor — headless Avalonia, service wiring, the `[AvaloniaFact]` deadlock pitfall — see the **`animation-editor-testing`** skill. For generating headless documentation screenshots of the UI, see the **`animation-editor-screenshots`** skill. For WASM/`?demo=` visual proof in the browser host, see **`animation-editor-browser-verify`**. For SkiaSharp rendering internals and performance debugging, see **`animation-editor-rendering`**.

## `.achx` is a general-purpose format — the editor authors, runtimes interpret

`.achx` is **not** an FRB2 file. It is a general-purpose animation/atlas format consumed by several runtimes that each render it their own way: Gum (across its Skia, raylib, and sokol.net backends), MonoGame/KNI/FNA, FRB1 (custom-shader rendering), and FRB2 (`SpriteBatch`). The editor authors the *format*; each runtime decides what to do with the data. This frames every feature decision here:

- **A field the editor exposes does not obligate any runtime to apply it.** Store the data in the format; whether a given runtime renders it is that runtime's choice. Do not gate adding a frame field on FRB2 (or any single runtime) implementing it — e.g. per-frame `Red`/`Green`/`Blue` are authored and stored for game code to consume, while FRB2's `SpriteBatch` path never applies them itself.
- **The preview is a reference rendering, not a per-runtime contract.** The bottom panel renders with SkiaSharp (`PreviewControl`, `SKCanvas`/`SKColorFilter` in `DrawFrameCore`), so it will diverge from what a MonoGame/FNA/FRB1 runtime produces for the same file. That divergence is inherent to a general-purpose tool and is not a bug — pick a sensible canonical interpretation. "The preview might not match a runtime" is never a reason to withhold an authoring feature.

## Negative R/G/B is allowed — Multiply clamps it, Add uses it as subtract

`Red`/`Green`/`Blue` are plain `int?` (`AnimationChain.Common/AnimationFrameSave.cs`) with no engine-side range check, and the FRB2 runtime never applies these fields at all (see above), so the only place a range matters is the preview. The inspector's `PropRed`/`PropGreen`/`PropBlue` `NumericUpDown`s (`MainWindow.axaml`) allow -255..255; `PropAlpha` stays 0..255 since alpha is a separate straight-opacity value, not part of a `ColorOperation`. In `FrameColorFilter.Create` (`AnimationEditor.Views/FrameColorFilter.cs`), **Add**'s Skia color-matrix offset is signed, so a negative value subtracts and clamps at the final pixel for free. **Multiply** packs the channel into a `byte` for `SKColor`, so it explicitly `Math.Clamp`s to 0 first — an unclamped negative `int`→`byte` cast wraps (`-10` becomes `246`) instead of darkening. Keep that clamp if you touch this method.

## Project layout

```
tools/AnimationEditorAvalonia/
├── AnimationEditorAvalonia.slnx
├── docs/
│   ├── DEVELOPMENT.md            ← read first when starting work
│   └── FEATURE_COVERAGE_REPORT.md
├── src/
│   ├── AnimationEditor.App/      ← Avalonia host: MainWindow.axaml(.cs), Models/, Services/, Settings/, and App-only Controls/ (e.g. FilesPanelControl)
│   ├── AnimationEditor.Views/    ← the SkiaSharp controls (App and Browser both consume it)
│   │   └── Controls/
│   │       ├── WireframeControl.cs, TextureViewport.cs    ← top panel (texture + frame regions)
│   │       ├── PreviewControl.cs, PngPreviewControl.cs    ← bottom panel (playback) + PNG diff viewer
│   │       └── ZoomControl.axaml(.cs), IZoomTarget.cs     ← reusable zoom widget (see "Two-panel mental model")
│   ├── AnimationEditor.Core/     ← UI-independent logic (no SkiaSharp)
│   │   ├── CommandsAndState/     ← AppState, AppCommands, ApplicationEvents
│   │   ├── Data/, IO/, Rendering/, ViewModels/
│   │   └── ProjectManager.cs, SelectedState.cs
│   └── AnimationEditor.Browser/  ← WASM (BlazorGL/KNI) head
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

- **Wireframe (top)** — the texture editor. User loads a sprite sheet, draws/edits frame regions on it. State: pan, zoom, selected frame, snap-to-grid.
- **Preview (bottom)** — the animation player. Plays the selected `AnimationChain` at runtime speed; supports onion skin and origin guides. State: pan, zoom, playback timer, speed multiplier.

All three zoom surfaces — wireframe toolbar, preview toolbar, and the PNG diff bar — mount the same reusable **`ZoomControl`** (`AnimationEditor.Views/Controls/`), the `[−][editable %][+]` widget. Wire it in code with `zoomControl.Attach(target)`, where `target` is an **`IZoomTarget`** (exposes live `Zoom`, `SetZoomPercent`, `ZoomChanged`, `WheelZoomPresets`); `Attach` installs the wheel presets, follows `ZoomChanged` to display the live percent, and routes edits/steps back into the target. The suppression flag that breaks the echo loop lives inside `ZoomControl` — callers don't manage it.

**Landmine — the zoom hosts share no base class.** `IZoomTarget` exists only because `TextureViewport` (wireframe + PNG viewer) and `PreviewControl` are unrelated types. To share any *other* viewport behavior across both, extend `IZoomTarget` (or add a sibling interface); there is no common base to hang it on.

**Scan for an existing control before adding one to a second surface; extract on the second copy.** `ZoomControl` exists because the widget was first duplicated as raw XAML plus per-host event wiring across three toolbars. When a control *and its wiring* would be copied a second time, factor it into a reusable `UserControl` — duplicated markup and its feedback-loop plumbing drift apart otherwise. (Testing an extracted `UserControl` has a namescope gotcha — see `animation-editor-testing`.)

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

## Tree reorder — chains and frames; shape order is fixed

Drag-and-drop tree reorder covers **chains and frames** (pure resolvers `ChainDropResolver` / `FrameDropResolver`, wired in `MainWindow`). **Do not add shape DnD reorder:** collision shapes in `.achx` keep a **fixed list order** for FRB1 runtime compatibility — order is meaningful to legacy consumers, not a cosmetic tree sort. Menu/Alt+Arrow shape reorder exists in `AppCommands.MoveShape` today; treat new reorder UX as chain/frame-only unless an issue explicitly revisits shape ordering across runtimes.

## Grid mode: double-click resizes; click/drag only repositions

Grid-mode click-to-place and handle-drag preserve the frame's existing size — only
double-click resizes it to the full grid cell (`GridPlacementCalculator.SnapToCell`,
called only from `WireframeControl.SnapSelectedFrameToGridCell`). This is deliberate:
a fresh PNG drop creates one frame sized to the whole sheet, and double-click-to-carve-
a-cell is how that gets sized down without dragging edge handles by hand. Don't collapse
the two gestures onto one shared size-preserving helper again — see
`GridPlacementCalculator`'s doc comment for why that was tried and reverted.
