---
name: animation-editor-browser-verify
description: >-
  Visual proof on Animation Editor WASM without shipping demo hooks. Triggers:
  browser History screenshot, WasmAppHost ports, canvas wait, FeatureDemos,
  Playwright UI-drive (#690).
---

# Animation Editor — Browser Visual Verify

Use when proving a UI change in **`AnimationEditor.Browser`**. Prefer **desktop DocScreenshots + Core.Tests** when that already covers the behavior — browser capture is optional extra evidence.

Shared drive scripts: **`AnimationEditor.Core.Demo.FeatureDemos`** (internal; InternalsVisibleTo tests/DocScreenshots only).

## Do not pollute shipping App code

**Landmine:** never commit `?demo=` / `FeatureDemos.TryRun` wiring into `App.axaml.cs` `BuildView` (or Desktop `MainWindow`). A query-param backdoor mutates undo state on any deployed build.

Default proof path:
1. Unit-test labels via Core (`CommandDescriptionTests` / `BrowserUiDriveLabelTests` / `FeatureDemosTests`).
2. Desktop History PNGs via DocScreenshots `_ScratchCapture` + `FeatureDemos.TryRun`.
3. **True UI-driven Browser proof (#690):** external Playwright at
   `tools/AnimationEditorAvalonia/tests/AnimationEditor.Browser.Ui/` (Decision C2).
   Phase 0: Avalonia.Browser 12.0.1 does **not** expose control ARIA to the DOM — do not
   rely on Playwright `getByRole`. Phase 1 drives DEBUG `__aeUiAutomation` by B1
   `AutomationId` and asserts undo Descriptions (A2). Run: `scripts/run-browser-ui-drive.ps1`.
   **Do not** paste temporary `#if DEBUG` / `?demo=` FeatureDemos hooks into shipping App for this.

## Run and find the real URL

```
dotnet run --project tools/AnimationEditorAvalonia/src/AnimationEditor.Browser --no-launch-profile --urls "http://127.0.0.1:5420"
```

WasmAppHost often **ignores** `--urls` and prints `App url: http://127.0.0.1:<ephemeral>/`. Use that host (`AE_BROWSER_URL`). Port-in-use on launchSettings HTTPS → kill the listener or keep `--no-launch-profile`.

## Wait for load

Avalonia **canvas** — `document.body.innerText` stays empty. Poll `canvas` count ≥ 1 and splash gone, then screenshot / Playwright. Sidebar tabs are **not** ordinary DOM buttons; use a11y Names (`History`, `Add Animation`, `Undo history`) or CDP `Accessibility.getFullAXTree`. Avalonia.Browser ARIA is **partial** (12.0.1).

## After capture

Copy PNGs to `tools/AnimationEditorAvalonia/tests/_out/<feature>/` (UI-drive uses `_out/browser-ui-drive/`).
