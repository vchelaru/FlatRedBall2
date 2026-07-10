# Decision: toolbar/pill restyling + branded header bar for the browser build (#648, Phase 8)

Status: **Shipped.** Verified live in a real Chrome tab, theme toggle confirmed to recolor the
new chrome (not just the canvas).

## Scope shipped

1. **Icon + label buttons.** Every toolbar button/toggle that has a matching SVG in
   `AnimationEditor.Views/Assets/icons/svg/` (Phase 7) now renders that icon next to its label:
   Add Animation/Frame/Rectangle/Circle/Delete Selected, Undo/Redo, Onion Skin, Guides, Grid.
   `Interpolate` and `Diagnostics (F3)` stay text-only — no dedicated icon exists for either, and
   inventing one wasn't in scope.
2. **Move/Magic-Wand pill.** Added the visual toggle-pair pill desktop has (split corner radii, 1px
   `LineBrush` divider, mutually-exclusive `IsChecked`) — browser never had a "Move" toggle at all
   before this, only the implicit default. No new interaction logic: Move still just clears
   `wireframe.IsMagicWandMode`.
3. **Real `ZoomControl` instances.** Replaced the four bespoke +/- zoom buttons and their manual
   `ZoomPresetStepper` calls with two `AnimationEditor.Views/Controls/ZoomControl` instances
   (`.Attach(wireframe)` / `.Attach(preview)`) — the same shared, already-tested,
   `DynamicResource`-styled widget desktop's wireframe/preview toolbars and PNG diff bar all use.
   Net simplification: deletes the bespoke stepper wiring entirely.
4. **Branded header bar.** Visual only, per the roadmap's decision #1 — `IconChain` (closest thing
   to an app mark in the shared icon set; no dedicated logo asset exists), "Animation Editor", and
   the active tab's filename. Explicitly **no** drag-to-move / custom resize /
   minimize-maximize-close — the browser tab already has real OS chrome for those.
5. **2-zone status bar.** Left zone: active filename + animation count. Right zone: the existing
   transient status message. Desktop's middle zone (cursor position + selection summary) is
   dropped, not faked — the browser has no cursor-position tracking to report honestly.

## Real bug found and fixed: `DynamicResourceExtension` + `.Bind()` throws at runtime

Discovered by pushing past a false negative — the sandboxed preview browser's screenshot tool
returned solid black for this page, matching the `BROWSER_SPIKE_FINDINGS.md` precedent of stale/
hung CDP screenshot capture. Pixel readback (`canvas.toDataURL()`) proved real content was
rendering, but out of caution this was re-verified in a **real** Chrome tab via `claude-in-chrome`
per that same precedent — and there, it genuinely didn't render.

Console showed: `ManagedError: AggregateException_ctor_DefaultMessage (Arg_InvalidCastException)`,
thrown during `BuildView()` before `MainView` was ever assigned (fully blank page, not a partial
render). Root cause: `control.Bind(Property, new DynamicResourceExtension("Key"))` — valid-looking
C# that compiles cleanly (the XAML precedent everywhere else in this codebase uses
`{DynamicResource Key}` markup syntax, which the XAML compiler resolves through a different path)
throws an `InvalidCastException` when actually run this way from code-behind.

**Fix:** use `control.Bind(Property, control.GetResourceObservable("Key"))` instead —
`GetResourceObservable` returns an `IObservable<object?>` that `.Bind()`'s standard-conversion
overload handles safely, and this is Avalonia's documented pattern for resource binding in
code-behind rather than XAML. Applied to all 7 call sites this phase introduced (icon color, pill
border, header bar background/border/filename text, divider background, status bar background).
Confirmed fixed: live Chrome re-test rendered correctly, zero console errors, and the theme toggle
now visibly recolors the header/toolbars/status bar in both directions.

**Consequence for future phases:** any code-behind `DynamicResource` binding in
`AnimationEditor.Browser` or `AnimationEditor.Views` (Phases 9-15 will add plenty) must use
`GetResourceObservable`, not `new DynamicResourceExtension(...)` passed to `.Bind()`.

## Explicitly out of scope

- **Grid-size stepper.** Left as `NumericUpDown` rather than rebuilding desktop's exact hand-rolled
  `TextBox` + flanker `−`/`+` button pill. It already benefits from Phase 6/7's shared
  `ButtonSpinner` `ControlTheme` (real chevron icons) with zero code changes here — a free win from
  earlier phases, not something this phase did. Rebuilding the flanker version is a bigger,
  separable change than "restyle with existing tokens/icons."
- **Layout restructuring.** Still the fixed 3-column grid (sidebar | wireframe | preview) — that's
  Phase 9 (sidebar tabs) and Phase 10 (stacked splitter layout).
- **Menu bar.** Phase 13.

## Verification

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] `dotnet test` on all 3 suites — Core 1584, Views 28, App 673 — unchanged, all green (this
      phase is browser-only glue code; no Core/Views behavior changed)
- [x] `dotnet publish -c Release` for `AnimationEditor.Browser.csproj` — exit 0
- [x] Live Chrome: icons/pills/ZoomControl/header/status bar all render correctly; theme toggle
      recolors all new chrome in both directions; zero console errors after the fix
