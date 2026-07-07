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

## Rendering & performance

Both panels render through a SkiaSharp `ICustomDrawOperation` on Avalonia's render thread (top: `WireframeControl.DrawOp.Render`; bottom: `PreviewControl.DrawFrameCore`). `lease.GrContext != null` means the GPU (ANGLE) path; null means CPU (software) — the two behave differently, so always know which you're on before reasoning about cost.

**A "used to be smooth, now it's slow" report is a git signal, not an architecture signal.** Before theorizing about the pipeline, `git log` the render files — a recent commit that changed *how an image is drawn* is far more often the cause than a long-standing pattern suddenly biting. Chasing the architecture first wastes rounds.

**Measure before guessing.** An on-canvas draw-time overlay (rolling ms/frame + a GPU/CPU tag) toggles with **F3** (`DiagnosticsEnabled` on each control, rendered by `DrawTimeOverlay`). Turn it on first: the ms reading plus the GPU/CPU tag localize the cost and rule out whole categories of hypothesis immediately.

**Landmine — a raster `SKImage` re-uploads to the GPU every frame.** An `SKImage` from `SKImage.FromBitmap` is CPU-resident; on the GPU path Skia re-uploads the *visible source region* on each draw, so cost scales inversely with zoom — **zoomed out is slower**, which misdirects toward mipmaps/filtering. Fix: let Skia keep the texture cached by raising the GPU resource-cache budget once per lease (`GRContext.SetResourceCacheLimit`), sized to hold the image. Do **not** hand-manage a GPU copy via `SKImage.ToTextureImage` held across frames — opening a menu/popup purges the `GRContext`, leaving that cached texture dangling so it draws nothing (blank/flicker of *only* the image, while vector draws in the same pass survive). Skia's own cache re-uploads correctly after a purge; a hand-held texture does not.

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
