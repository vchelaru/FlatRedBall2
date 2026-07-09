# Decision: tabbed sidebar (Inspector/History) + sectioned Inspector (#649, Phase 9)

Status: **Shipped.** Verified live in a real Chrome tab: tab switching, sectioned inspector with
real data, tab-foreground swap on selection, and status-bar animation count all confirmed working.

## Scope shipped

1. **Tree always visible, tabbed panel below.** Replaced the browser's fixed tree → inspector →
   history stacked `Grid` with desktop's shape: `AnimationTreeControl` fills row 0, a
   `GridSplitter` in row 1, and a `TabControl` in row 2 with **Inspector** (default) and
   **History** tabs. `RowDefinitions="2*,4,3*"` matches desktop's `LeftPanelGrid` proportions
   exactly.
2. **Files tab deferred to Phase 12**, per the roadmap — not started here.
3. **History panel moved into its own tab** (was always-visible below the inspector before);
   content unchanged (`ListBox` + `HistoryRowVm` template).
4. **Tab styling matches desktop's `SidebarTabs`**: 11px SemiBold, 36px height, `InkMid` unselected
   / `Ink` selected foreground. Desktop does this with a `TabItem`/`TabItem:selected` XAML style
   selector; the browser build has exactly two fixed tabs, so plain instance-level
   `GetResourceObservable` rebinding on selection change was simpler than constructing an
   `Avalonia.Styling.Style` object in C#.
5. **Sectioned `InspectorControl.axaml`** with COORDINATES/TIMING/TRANSFORM/TEXTURE/COLOR headers
   (Frame) and a single TRANSFORM section (Rect/Circle), matching desktop's `SectionName` styling
   (11px SemiBold, `InkMid`, bordered `LineBrush` dividers).

## Correction to the plan: `InspectorControl` is not actually shared with desktop

The original roadmap text says to "verify desktop still renders correctly" after editing
`InspectorControl.axaml` because it's "shared with desktop." **This is not true today** — confirmed
by grepping `AnimationEditor.App` for `InspectorControl`: the only matches are in compiled
`bin/`/`obj/` binaries, zero source references. Desktop's actual inspector is a completely separate,
much richer implementation built directly inline in `MainWindow.axaml` (`PropFramePanel` etc., with
editable pixel/UV coordinate grids, sprite-sheet tile index fields, and more) that was never
extracted into `AnimationEditor.Views`. `InspectorControl` was created fresh for the browser in
Phase 1 (#603) as a simpler, read-mostly summary view.

Consequence: this phase's sectioning is a **visual-parity restyle** of the browser's own simpler
control — same section *taxonomy* as desktop (COORDINATES/TIMING/TRANSFORM/TEXTURE/COLOR), not the
same field set, and edits here carry zero risk of regressing desktop (there's nothing to regress —
desktop never calls into this class). `AnimationEditor.App`'s own build/tests were still re-run as
a sanity check regardless.

## TDD

Added `FrameSelected_ShowsSectionHeaders_MatchingDesktopNaming` to `InspectorControlTests.cs`
*before* touching the XAML — asserting `control.CoordinatesHeader.Text == "COORDINATES"` etc.
Confirmed it failed to compile first (the fields didn't exist yet — `CS1061` on all five header
properties), then implemented the sectioned XAML to make it pass. All 23 pre-existing
`InspectorControlTests` continued passing unchanged, since every original `x:Name`d field
(`FrameTextureText`, `RectXInput`, etc.) was preserved — only the surrounding container markup
changed.

## Found and flagged, not fixed here: Save As crashes the WASM runtime

While live-verifying tab switching, a stray click landed on "Save As…" instead of the adjacent
theme button (the button shifted position) and completed the native save picker. This crashed the
entire WASM runtime (unrecoverable `RuntimeError: unreachable`, requiring a page reload) — root
cause confirmed via the Chrome console stack trace: `AnimationChainListSave.Save(Stream)` uses a
synchronous `XmlWriter`, but `Avalonia.Browser.Storage.WriteableStream` (backing
`IStorageFile.OpenWriteAsync()`) only supports `WriteAsync`, throwing
`InvalidOperationException: Browser supports only WriteAsync` deep inside `XmlWriter.Dispose()` in
a way that takes the whole runtime down with it. **Pre-existing bug, unrelated to Phase 6-9 UI
work** — confirmed by isolating the theme-toggle and Undo actions individually (both clean) and
tracing the crash's exact stack frames to the Save As code path specifically. Flagged as a separate
follow-up task rather than fixed here (out of scope for a sidebar-layout phase).

## Verification

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] `dotnet test` on all 3 suites — Core 1584 (one transient/flaky failure on a single run,
      reproduced clean on two immediate reruns — unrelated, Core wasn't touched this phase),
      Views 29 (incl. new section-header test), App 673 — all green
- [x] `dotnet publish -c Release` for `AnimationEditor.Browser.csproj` — exit 0
- [x] Live Chrome: tree/splitter/tabs render correctly; Frame selection shows all 5 sectioned
      headers with real data; History tab shows entries and the tab-foreground swap (`Ink`
      selected / `InkMid` unselected) works both directions; status bar's animation count updates
      via the existing `AnimationChainsChanged` hook; zero console errors across isolated
      theme-toggle, Undo, and tab-switch actions
