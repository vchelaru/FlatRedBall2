---
name: animation-editor-browser-verify
description: >-
  Browser/WASM AE smoke — not a Core/App test mirror. Triggers: AnimationEditor.Browser,
  Playwright Browser.Ui, WasmAppHost, __aeUiAutomation, FeatureDemos landmine.
---

# Animation Editor — Browser Visual Verify

Use when the question is **Browser/WASM-specific** (boot, Browser host wiring, Debug automation bridge). Prefer **Core.Tests + desktop Headless/DocScreenshots** for command behavior and desktop UI — see **`animation-editor-testing`**.

Shared desktop drive scripts: **`AnimationEditor.Core.Demo.FeatureDemos`** (internal; InternalsVisibleTo tests/DocScreenshots only) — desktop only, not Browser shipping App.

## When to add a Browser.Ui test

**Add one** only if Headless/desktop cannot catch it, e.g.:

- WASM cold start / sample load / canvas ready
- Browser-only host wiring (`BrowserHotkeys`, Open Folder FS APIs, no Ctrl+D)
- “Does the Debug automation bridge still reach real handlers on Browser?”

**Do not** port Core or `[AvaloniaFact]` suites here. Browser Playwright is slow (ephemeral ports, Debug host) and drives a different shell (`BuildView`, not `MainWindow`). Keep a **small smoke set**; grow only when a Browser-specific bug appears. Full “when / how” lives in `tools/AnimationEditorAvalonia/tests/AnimationEditor.Browser.Ui/README.md`.

## Do not pollute shipping App code

**Landmine:** never commit `?demo=` / `FeatureDemos.TryRun` wiring into Browser `App.axaml.cs` `BuildView` (or Desktop `MainWindow`). A query-param backdoor mutates undo state on any deployed build.

Default proof path:

1. Core unit tests for labels/commands (`CommandDescriptionTests` / `BrowserUiDriveLabelTests`).
2. Desktop History PNGs via DocScreenshots + `FeatureDemos.TryRun`.
3. Optional Browser smoke: Playwright under `tests/AnimationEditor.Browser.Ui/` — Debug `__aeUiAutomation` clicks by `AutomationId` and dumps undo Descriptions. Run `scripts/run-browser-ui-drive.ps1`.

**Landmine — no Playwright `getByRole` on Avalonia.Browser today:** the canvas host does not expose control ARIA to the DOM (CDP AX tree ≈ page title). Keep `AutomationProperties` for desktop a11y + stable ids; Browser drive uses the Debug bridge, not DOM a11y.

## Run and find the real URL

```
dotnet run --project tools/AnimationEditorAvalonia/src/AnimationEditor.Browser --no-launch-profile --urls "http://127.0.0.1:5420"
```

WasmAppHost often **ignores** `--urls` and prints `App url: http://127.0.0.1:<ephemeral>/`. Use that host (`AE_BROWSER_URL`). Port-in-use on launchSettings HTTPS → kill the listener or keep `--no-launch-profile`. **Debug** configuration required for `__aeUiAutomation` (omitted from Release).

## Wait for load

Avalonia **canvas** — `document.body.innerText` stays empty. Poll `canvas` count ≥ 1, then wait for `globalThis.__aeUiAutomation` (Debug) before driving. Screenshot / assert via the bridge — not DOM refs.

## After capture

Copy PNGs to `tools/AnimationEditorAvalonia/tests/_out/<feature>/` (UI-drive uses `_out/browser-ui-drive/`).
