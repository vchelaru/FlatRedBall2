# Decision: shared theme-token foundation for the browser build (#630, Phase 6)

Status: **Implemented, unit-tested, and live-verified with no regressions.** Deliberately non-visual
in the browser build today — see "Consequences" below for why a screenshot can't prove this phase
worked, and what actually does.

## Problem

This is Phase 6 of the UI/UX design-parity roadmap (Phases 6-15, see the plan). Every phase 1-5
built the browser's UI in raw C# (`AnimationEditor.Browser/App.axaml.cs`) using plain, unstyled
Avalonia controls — `AnimationEditor.Browser/App.axaml` had no theme dictionary at all, just
`<FluentTheme/>`. Desktop's actual look (dark/light brand colors, consistent spacing/pill styling)
lives entirely in `AnimationEditor.App/App.axaml`'s `ThemeDictionaries` and style classes, which the
browser build had no access to. Every later visual-parity phase (toolbar restyling, tabbed sidebar,
splitter layout, menu bar, toasts) depends on these `DynamicResource` keys resolving in the browser
first — this phase is pure prerequisite plumbing.

## Decision

Moved the design tokens out of `AnimationEditor.App/App.axaml` into two new files in
`AnimationEditor.Views/Theming/` (the project already shared between `App` and `Browser`):

- **`ThemeTokens.axaml`** — a bare `<ResourceDictionary>` (no `x:Class`) containing the Dark/Light
  `ThemeDictionaries` (all `SolidColorBrush`/`Color` tokens: `BgCanvas`, `BgApp`, `BgPanel`, `BgRail`,
  `BgHover`, `BgActive`, `LineBrush`, `LineStrong`, `Ink`/`InkMid`/`InkDim`, `Accent`/`AccentSoft`/
  `AccentCool`/`Ok`, `InfoBannerBg`, `TreeRowStripe`, `IconInk*`, Fluent accent-ramp overrides).
  Values moved verbatim, no renaming.
- **`ThemeStyles.axaml`** — a separate `<Styles>`-rooted file for the style classes (`.compact`,
  `.flanker` + its pointer-state sub-selectors, the default `NumericUpDown` style, the
  `ButtonSpinner TextBox` border-strip, `Button:disabled` opacity). Avalonia `Style` elements can't
  live inside a `ResourceDictionary` file — they need a `Styles` root — so this had to be a second
  file, not folded into `ThemeTokens.axaml`. Both `App.axaml`s include it via `StyleInclude` inside
  `Application.Styles`, alongside `ThemeTokens.axaml`'s `ResourceInclude` inside
  `Application.Resources` (`ResourceDictionary.MergedDictionaries`, not nested inside
  `ThemeDictionaries` directly — `ThemeTokens.axaml` is itself a whole `ResourceDictionary` with its
  own `ThemeDictionaries`, and Avalonia's resource lookup already walks each merged dictionary's own
  `ThemeDictionaries` recursively, so a plain merge is sufficient for `DynamicResource` to keep
  following the active `ThemeVariant`).

**Deliberately excluded, deferred to Phase 7**: the `ButtonSpinner` `ControlTheme` override and the
`Button.plus`/`Button.minus` style classes. All three render an SVG icon via `Avalonia.Svg.Skia`
(`Svg.Controls.Skia.Avalonia` NuGet package), which neither `AnimationEditor.Views` nor
`AnimationEditor.Browser` reference yet — Phase 7 is the icon-system spike that decides whether that
package survives a WASM publish at all. Moving icon-dependent XAML into the shared project now would
either fail to compile (the `xmlns:svg` type wouldn't resolve without the package reference) or
require prematurely adding the package before its WASM viability is confirmed. `NumericUpDown` falls
back to Fluent's default spinner in the browser until Phase 7 lands — an acceptable interim state,
matching what the roadmap already flagged.

`AnimationEditor.Views.csproj` needed an explicit `<AvaloniaResource Include="Theming\ThemeTokens.axaml"/>`
(and the same for `ThemeStyles.axaml`) — confirmed by a real failing build: the SDK's default
`.axaml` glob only auto-includes files with `x:Class` (compiled `UserControl`s); a bare
`ResourceDictionary`/`Styles` file needs an explicit item, per the exact `AvaloniaXamlLoader.Load`
error message ("No precompiled XAML found... make sure to specify x:Class and include your XAML file
as AvaloniaResource").

## Tests first (TDD)

`ThemeTokensTests.cs` (new, `AnimationEditor.Views.Tests`) loads `ThemeTokens.axaml` directly via
`AvaloniaXamlLoader.Load` and asserts `BgCanvas`/`LineBrush` resolve under both `ThemeVariant.Dark`
and `.Light`, and that the two variants actually produce different values (not both silently falling
back to some default). Written and confirmed failing first — before `ThemeTokens.axaml` existed, the
test failed with `XamlLoadException: No precompiled XAML found for avares://...ThemeTokens.axaml`,
the correct failure mode. Deliberately self-contained (loads the resource dictionary itself, doesn't
wire it into the shared `TestApp.cs`) so its failure before implementation didn't cascade into every
other `AnimationEditor.Views.Tests` test failing for an unrelated reason.

## Consequences

**This phase is deliberately invisible in the browser build today.** Nothing in
`AnimationEditor.Browser/App.axaml.cs`'s current ad-hoc `Button`/`ToggleButton`/`ListBox` controls
uses `DynamicResource` — that's Phase 8's job (toolbar restyling). The one control that already does
(`AnimationEditor.Views/Controls/ZoomControl`) isn't wired into the browser build yet either (also
Phase 8). So a before/after screenshot of the running browser app shows **no visible change** from
this phase, by design — matching the roadmap's own "Explicitly out of scope for Phase 6: no layout
changes of any kind."

What *does* visibly change between the Dark/Light screenshots taken during live verification (the
Wireframe/Preview canvas background) is **not** evidence this phase worked — that's
`AnimationEditor.Views.Theming.CanvasPalette`, a separate, already-shared, already-working SkiaSharp
color palette independent of the XAML `DynamicResource` tokens this phase adds (canvases are
hand-drawn with Skia, not styled via Avalonia XAML). Worth stating plainly so a future session doesn't
mistake that pre-existing behavior for proof of this change.

## Live verification

- `dotnet build` on both `AnimationEditor.App.csproj` (desktop) and the full solution (including the
  WASM `AnimationEditor.Browser` target) — 0 warnings/errors. Desktop's own build is the first real
  regression check: if the `MergedDictionaries`/`StyleInclude` refactor broke resource resolution,
  desktop's `App.axaml` (which still contains the icon-dependent `ButtonSpinner` theme and
  `.plus`/`.minus`, both of which reference `IconInk*` tokens now sourced from the merged
  dictionary) would fail to compile or resolve at runtime.
- All 3 test suites green: `AnimationEditor.Core.Tests` (1572), `AnimationEditor.Views.Tests` (23,
  including the new `ThemeTokensTests`), `AnimationEditor.App.Tests` (654) — desktop's full headless
  suite passing confirms no behavioral regression from moving its own theme resources into a merged
  external file.
- Live browser: loaded the dev server, confirmed the existing theme-toggle button still works
  (label flips Dark/Light, canvas recolors via the pre-existing `CanvasPalette` path as always),
  confirmed **zero** new console errors or resource/binding-related messages after the merge (only
  the same 2 pre-existing "Failed to create render target" GL-context messages already documented in
  `BROWSER_WIREFRAME_DECISION.md`, unrelated to this change) — the meaningful signal here is the
  *absence* of an Avalonia "resource not found" binding error, which is what a broken
  `MergedDictionaries`/`AvaloniaResource` wiring would have produced.

## Known gap

No live-browser proof that `ThemeTokens.axaml`'s specific keys resolve correctly inside the actual
running `AnimationEditor.Browser` `Application` (as opposed to the headless test app) — the
automated test covers the headless case, and the live check only confirms no exceptions/binding
errors occur, not that e.g. `BgCanvas` specifically equals `#0e0f12` at runtime in that environment.
This becomes directly and unavoidably observable in Phase 8 (the first phase that actually paints
something with these tokens) — if it were broken, Phase 8's toolbar restyling would immediately show
wrong/default colors instead of the intended palette, so this gap self-resolves as soon as the next
phase lands rather than needing separate closure here.
