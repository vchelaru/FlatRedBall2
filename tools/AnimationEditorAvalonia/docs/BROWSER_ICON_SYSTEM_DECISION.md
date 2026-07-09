# Decision: shared SVG icon system for the browser build (#644, Phase 7)

Status: **Spike passed; rollout implemented and unit-tested.** Live-browser toolbar restyling
(that actually *shows* these icons in the browser UI) is Phase 8 — see
`docs/BROWSER_UI_PARITY_ROADMAP.md`.

## Problem

Phase 6 (#630) moved theme tokens into `AnimationEditor.Views` but deliberately excluded
icon-dependent XAML (`ButtonSpinner` `ControlTheme`, `.plus`/`.minus` styles) because neither
`AnimationEditor.Views` nor `AnimationEditor.Browser` referenced `Svg.Controls.Skia.Avalonia`.
All 23 toolbar/tree SVGs lived under `AnimationEditor.App/Assets/icons/svg/` with
`avares://AnimationEditor/...` URIs — unreachable from the shared Views/Browser projects.

Before moving icons repo-wide, the roadmap required a **spike**: confirm
`Svg.Controls.Skia.Avalonia` renders under Avalonia Browser/WASM and survives a Release publish
(SkiaSharp works on WASM; that does not guarantee this NuGet package's asset pipeline does).

## Spike outcome: SVG path viable

| Check | Result |
|---|---|
| `Svg.Controls.Skia.Avalonia` in `AnimationEditor.Browser` | Restores and builds |
| `dotnet publish -c Release` for `net10.0-browser` | **Exit 0** |
| Trim | `TrimmerRootAssembly Include="Svg.Controls.Skia.Avalonia"` (not blanket IL2104 suppress) |
| Pre-existing publish breakage | `IL2026` from `XmlSerializer` + `ReflectionBinding` — **already broken on main** before Phase 7; suppressed narrowly (`NoWarn` IL2026 only) |

**Decision:** Use `Svg.Controls.Skia.Avalonia` for shared icons. Do **not** fall back to inlined
`<Path Data="...">` unless a future regression blocks SVG on WASM.

## Rollout (implemented)

1. **Moved all 23 SVGs** from `AnimationEditor.App/Assets/icons/svg/` to
   `AnimationEditor.Views/Assets/icons/svg/`; deleted the App copy (PNG/ICO/ICNS icons stay in App).
2. **Added `Svg.Controls.Skia.Avalonia`** to `AnimationEditor.Views.csproj` (Browser keeps its own
   reference + trimmer root for WASM publish).
3. **Rewrote every `avares://AnimationEditor/.../svg/` URI** repo-wide to
   `avares://AnimationEditor.Views/Assets/icons/svg/` (one-time rename, no back-compat shim).
4. **Moved Phase 6 deferrals into Views:**
   - `ThemeIconResources.axaml` — `ButtonSpinner` `ControlTheme` with chevron SVGs
   - `ThemeStyles.axaml` — `.plus`/`.minus` icon button styles
   - Both `App.axaml` files merge `ThemeIconResources.axaml` via `ResourceInclude`.
5. **Removed** the temporary browser spike control (`iconSpike` in `App.axaml.cs`) and the
   spike-only `Browser/Assets/icons/` copy.

## Rollout vs Phase 8 split

**Shipped icon *infrastructure* in Phase 7; ship icon *consumption* in Phase 8.**

Moving 23 SVGs + shared spinner/plus/minus styles in Phase 7 gives the browser build correct
`ButtonSpinner` behavior immediately (e.g. grid-size `NumericUpDown` if wired) and unblocks Phase 8
toolbar restyling without a second asset move. The browser UI still uses text buttons — no visible
icon toolbar until Phase 8 applies desktop's `MainWindow.axaml` chrome to `App.axaml.cs`.

## Tests

`IconAssetsTests.cs` (`AnimationEditor.Views.Tests`): `AssetLoader.Open` on
`avares://AnimationEditor.Views/Assets/icons/svg/IconPlay.svg` — confirms the moved asset is
registered as `AvaloniaResource` in Views.

## Verification checklist

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] `dotnet test` Core / Views / App suites — green (includes new `IconAssetsTests`)
- [x] `dotnet publish -c Release` `AnimationEditor.Browser.csproj` — exit 0
- [ ] Live Chrome: optional until Phase 8; spike proved mechanism before rollout

## Consequences

- Desktop `AnimationEditor.App` now depends on Views for SVG assets — correct layering (Views is
  the shared UI surface for App + Browser).
- `AnimationEditor.App` still owns raster/shell icons (`achx-icon-256.png`, `AppIcon.ico`, etc.).
- Phase 8 can wire `ZoomControl` and icon toolbars knowing all `avares://AnimationEditor.Views/...`
  paths resolve in both desktop and browser.
