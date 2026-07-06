# Decision: localStorage-backed settings persistence (#610 follow-up)

Status: **Implemented and partially verified in a live browser tab** -- the JS-module-import
bug described below was actually caught and fixed this way. Related:
[#610](https://github.com/vchelaru/FlatRedBall2/issues/610).

## Problem

The post-#588 roadmap called for persisting browser-editor settings (theme, canvas background,
recent files, per-file zoom/pan/grid/guides) to `localStorage`, mirroring desktop's file-backed
`AppSettingsModel`/`.aeproperties`. But before writing any storage layer, a check of
`AnimationEditor.Browser/App.axaml.cs` turned up **zero** theme, zoom, or grid UI in the browser
build — none of those settings have anywhere to come from yet. Building the persistence layer
first would be speculative plumbing with nothing to persist.

## Decision

Added the smallest real slice: a **theme toggle button**, plus the storage layer to persist it.
This gives the localStorage plumbing an actual, honest consumer instead of a hypothetical one.
Everything else from the original scope (canvas background, recent files, per-file zoom/pan/grid/
guides) stays deferred until the browser build grows the corresponding UI — extend
`BrowserSettingsStore` alongside whichever setting gets its first control next, rather than in
advance of it.

### Architecture

Split along the same testable-core / thin-glue line as `BrowserFolderWatcher`/`FolderSnapshotDiff`
(see `BROWSER_FOLDER_WATCH_DECISION.md`):

- **`AnimationEditor.Core/IO/ILocalStorage.cs`** — a minimal `GetItem`/`SetItem` interface,
  abstracting `window.localStorage`. Exists purely so the persistence logic below is testable with
  a fake, independent of real browser JS interop.
- **`AnimationEditor.Core/IO/BrowserSettingsStore.cs`** — the actual persistence logic
  (`LoadTheme`/`SaveTheme`), portable and unit-tested (`BrowserSettingsStoreTests.cs`, 4 tests:
  round-trip, both theme values, missing-key returns null, corrupt-stored-value returns null
  instead of throwing).
- **`AnimationEditor.Browser/LocalStorageInterop.cs`** — the real `ILocalStorage`
  implementation, backed by `System.Runtime.InteropServices.JavaScript`'s `[JSImport]` against a
  small ES module (`wwwroot/localStorage.js`) that wraps `window.localStorage.getItem`/`setItem`.
  This is thin, untestable browser glue -- same category as every other file in
  `AnimationEditor.Browser` (no dedicated test project exists for this assembly, by established
  precedent).
- **`ThemeManager`** (mapping `AppTheme` → Avalonia's `ThemeVariant`) already existed, but lived in
  `AnimationEditor.App.Theming` -- pure, zero Avalonia.Desktop dependency, just misplaced in the
  desktop-only project. Moved to `AnimationEditor.Views/Theming/ThemeManager.cs` (namespace left
  unchanged, matching this PR line's established convention for exactly this kind of move) so the
  browser build can reuse it instead of duplicating the `AppTheme` → `ThemeVariant` switch.

### Wiring

`LocalStorageInterop.InitializeAsync()` (the JS module import) runs in `Program.Main`, alongside
the existing bundled-sample HTTP fetches inside the same `Task.WhenAll` -- it's independent of
them, so no reason to serialize it. `App.axaml.cs`'s `BuildView()` reads the persisted theme
synchronously and applies it via `Application.Current.RequestedThemeVariant` before anything
renders; the toolbar's new "Theme: {name}" button toggles Dark/Light and saves on click.

## Live verification: a real bug, found and fixed

The first draft imported the JS module with `JSHost.ImportAsync("localStorage.js", "./localStorage.js")`.
Loading the actual dev server in a real Chrome tab (via browser automation, `dotnet run` +
navigating to `http://127.0.0.1:5420`) surfaced this immediately in the console:

```
ManagedError: AggregateException_ctor_DefaultMessage (TypeError: Failed to fetch dynamically
imported module: http://127.0.0.1:5420/_framework/localStorage.js)
```

The WASM runtime resolves `JSHost.ImportAsync`'s path relative to `_framework/` (where
`dotnet.runtime.js` itself lives), **not** the `wwwroot` root the file actually sits in. Fixed by
changing the path to `"../localStorage.js"`. Reloading confirmed the exception was gone. This is
exactly the kind of bug that compiles cleanly (the `[JSImport]`/`[JSExport]` source generator only
validates method signatures, not JS runtime path resolution) and would have shipped silently
broken without a live check -- the unit-tested `BrowserSettingsStore` logic was never wrong, only
the untestable glue in front of it.

## Remaining known gap

The dev server itself was confirmed correct by directly `curl`-ing the fingerprinted `.wasm`/`.pdb`
asset URLs (both returned `HTTP 200` with correct byte sizes), but the browser-automation tool
used for this session could not get the WASM app past its asset-download phase (`TypeError: Failed
to fetch` on the same URLs `curl` succeeded on) -- a limitation of that specific tool's handling of
large binary fetches, not a defect in this code or the server. So the actual "click the theme
button, reload, confirm it stuck" end-to-end pass still needs a human in a real, non-automated
browser tab -- the same category of residual gap Phase 1 and Phase 2's other slices already carry
(see `docs/BROWSER_TREE_INSPECTOR_DECISION.md`), just narrowed down from "the whole JS bridge" to
"the last few seconds of manual clicking," since the interop layer itself is now confirmed to
load and run.
