---
name: animation-editor-screenshots
description: Generating headless documentation screenshots of the Animation Editor UI — not correctness tests. Triggers: "take a screenshot", DocScreenshots, ScreenshotCapture, DocScreenshotManifest, illustrating a doc page.
---

# Animation Editor — Documentation Screenshots

Headless PNG capture of the Animation Editor's UI, for illustrating documentation pages (Timing, Offsets, Collision, etc.) — not for verifying behavior. For correctness tests, use the **`animation-editor-testing`** skill instead. The two share test infrastructure (`TestServices`, `CreateMainWindow`, `[AvaloniaFact]`, `Dispatcher.UIThread.RunJobs()`) but serve different purposes and audiences — keep scenario code in the project matching its purpose, not the one matching its plumbing.

## Where, and why it's a separate project

`tools/AnimationEditorAvalonia/tests/AnimationEditor.DocScreenshots/` — a **separate** headless project from `AnimationEditor.App.Tests`, not a folder inside it. Full rationale in `tools/AnimationEditorAvalonia/docs/DOC_SCREENSHOT_HARNESS_DECISION.md`.

**Landmine — it must stay separate.** Real pixel capture needs `AvaloniaHeadlessPlatformOptions.UseHeadlessDrawing = false` (see this project's `TestAppBuilder.cs`), set at the assembly level. `AnimationEditor.App.Tests` defaults that to `true` (a no-op drawing recorder, for speed) across its ~90 files, none of which need real pixels. Moving screenshot code into that project, or flipping its default, would either silently produce blank PNGs there or slow down the whole correctness suite.

## Capturing

`ScreenshotCapture.Capture(visual, outputPath)` — pass a `Window`/dialog (`TopLevel`) for full chrome, or any `Control` (e.g. `window.FindControl<Control>("AnimTree")`) to crop to just that control's bounds. Built on `TopLevel.CaptureRenderedFrame()`, not a hand-rolled `RenderTargetBitmap.Render(visual)` — the latter silently writes an empty PNG under headless (see the decision doc).

**Default to capturing the whole `window`, not a cropped control.** A screenshot showing only one panel strips the viewer's frame of reference for where that panel sits in the app. Crop to a specific control only when the user explicitly asks to see just that panel.

## Driving a scenario shares `animation-editor-testing`'s gotchas

Building chain/frame/selection state to screenshot uses the same `TestServices`/`CreateMainWindow` pattern as correctness tests — read `animation-editor-testing`'s "Service wiring in tests" section first, especially: assign `ProjectManager.AnimationChainListSave` (and add chains/frames) *after* `window.Show()` + `Dispatcher.UIThread.RunJobs()`, never before. `MainWindow.OnOpened` replaces it with a blank instance otherwise, silently discarding anything set up earlier.

**Critical, no exceptions — create chains/frames through `AppCommands`, not by `new`-ing model objects by hand.** This applies to every screenshot, including one-off ad hoc requests (see below) — there is no "just this once, it's throwaway" exception. A screenshot's whole point is to show what a real user would actually see; `new AnimationFrameSave { TextureName = "x.png" }` leaves every other field at its bare type default (`FrameLength` = `0f`), which is a state the app itself is incapable of producing — a real "Add Frame" always goes through `AppCommands.AddFrame`, which sets `FrameLength = 0.1f`, full-texture UVs, and a `ShapesSave`. Same principle for chains: use `AppCommands.AddAnimationChainWithName`, not `new AnimationChainSave`. These commands also fire the same tree-refresh/selection events a live click would, so a `Dispatcher.UIThread.RunJobs()` after each is usually all the wiring you need — no reflecting into private `MainWindow` methods. A hand-built chain also leaves the tree/inspector empty even when the wireframe itself renders correctly (nothing was ever added to `ProjectManager.AnimationChainListSave`) — an empty tree next to real content is itself a sign the scenario was hand-built instead of driven through commands.

This only covers *creation*. Once a chain/frame exists with real defaults, hand-setting a specific field the scenario needs to illustrate (`frame.RelativeX = 4`, `frame.FlipHorizontal = true`) is normal and expected — `AppCommands` has no method for every property, and the point is realistic *defaults*, not that every field must trace back to a command. Prefer a command when one exists for the edit itself (e.g. `AppCommands.MoveChain` over reordering the list by hand); fall back to direct field assignment for whatever a command doesn't cover.

**Landmine — `ProjectManager.FileName` must point at a real directory once you're driving state through selection instead of calling `WireframeCtrl.LoadTexture` directly.** Selecting a chain/frame (`SelectedState.SelectedChain`/`SelectedFrame`) routes through `MainWindow.SyncTextureCombo`, which only resolves a relative `TextureName` into an absolute path — and loads it — when `ProjectManager.FileName` is non-empty (`achxFolder = Path.GetDirectoryName(FileName)`); with `FileName` left `null`, the wireframe silently blanks instead of erroring. Set `ProjectManager.FileName = Path.Combine(dir, "demo.achx")` where `dir` is the folder actually holding the texture (the established test pattern — see `GridRenderTests`/`ChainSwitchTests`). Don't point `FileName` at `manual-test-content` directly: `AddChainCommand`/`AddFrameCommand` call `SaveCurrentAnimationChainList()` on every add, which writes a real `.achx` to `FileName`'s folder — copy the fixture PNG into a scratch temp dir instead and point `FileName` there.

## Use real texture fixtures, not a generated placeholder

`tools/AnimationEditorAvalonia/manual-test-content/characters/characters.png` — a real, good-looking 736×128 sprite sheet (32×32 grid, 23 cols × 4 rows: row 0 monk, row 1 knight, row 2 rogue, row 3 slime — see that folder's README for exact cell coordinates). Prefer this over generating a solid-color placeholder square for a texture — screenshots should look like something a real game would actually use. **Every frame cropped from it must be exactly one 32×32 cell unless the scenario explicitly calls for a different crop** — don't cross cell boundaries or crop mid-cell. Add more fixtures under `manual-test-content/` (following its existing README convention) as scenarios need different content (tilesets, larger sheets, transparency).

## Ad hoc "take a screenshot of X" requests

For a one-off request (not a permanent doc-page scenario), hand-edit a scratch file like `_ScratchCapture.cs` in this project — a single `[AvaloniaFact]` test that builds the scenario and calls `ScreenshotCapture.Capture`. Don't add one-off requests to `DocScreenshotManifest` (`DocScreenshotGeneratorTests.cs`) — that manifest is for scenarios a real doc page will regenerate repeatedly. "Ad hoc" relaxes *where the test lives*, not *how the scenario is built* — the `AppCommands`/`FileName` landmines above still apply in full.

Iterate fast: `dotnet build tests/AnimationEditor.DocScreenshots/...csproj`, then run the built `.exe -method "AnimationEditor.DocScreenshots._ScratchCapture.Capture"` directly — faster than `dotnet test` for one scenario, and its `Console.WriteLine` output is visible, unlike `dotnet test`'s VSTest adapter which swallows it on failure.

Write output via **`ScreenshotOutput.ResolveFeatureDir("<feature>")`** → `tools/AnimationEditorAvalonia/tests/_out/<feature>/` (not a temp dir you delete). Open with `Invoke-Item`, then `Read` the PNG yourself before showing the user.

## Feature proof (History / undo labels / similar)

When the ask is "prove the panel shows X" — not a unit assert:

1. Put the drive script in **`AnimationEditor.Core.Demo.FeatureDemos`** (internal, test/DocScreenshots only). Call `FeatureDemos.TryRun(...)` from `_ScratchCapture` — do **not** hand-build history rows or fork a second script, and do **not** wire demos into shipping `App`/`MainWindow`.
2. Drive real **`AppCommands` / `UndoManager.Execute`**. Never assign fake `HistoryEntryVm` text.
3. Select **History** (`SidebarTabs` → `HistoryTab`), optionally enlarge `LeftPanelGrid` row 2 so more labels fit, capture `window` + `HistoryScrollViewer`, write `labels.txt` from `UndoManager.UndoHistory`.
4. Optional browser PNG: **`animation-editor-browser-verify`** (temporary local hook only — revert before merge).
