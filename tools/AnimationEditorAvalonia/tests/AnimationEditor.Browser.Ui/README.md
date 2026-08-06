# AnimationEditor.Browser.Ui

External **Playwright** smoke for `AnimationEditor.Browser` (WASM). This is **not** the primary test suite and **must not** mirror `Core.Tests` / `App.Tests`.

For where tests belong in general, see skill **`animation-editor-testing`**. For Browser landmines and run tips, see **`animation-editor-browser-verify`**.

## What this suite is for

Prove a **small** set of Browser-only paths that desktop Headless cannot catch:

- WASM boot → canvas up → Debug automation bridge ready
- Browser host wiring (actions reachable through `__aeUiAutomation` / Browser-only menus/hotkeys)
- Optional assertable undo Descriptions after a scripted path (A2), without shipping `?demo=` / FeatureDemos

Today that is one smoke: Add Frame → Add Animation → open History → assert labels + screenshot.

## What this suite is *not* for

| Do not add here | Put it here instead |
|---|---|
| Undo/command label correctness | `AnimationEditor.Core.Tests` |
| Desktop input routing / layout | `AnimationEditor.App.Tests` (`[AvaloniaFact]`) |
| Doc / History PNGs on desktop | `AnimationEditor.DocScreenshots` |
| “Same test as Headless but on Browser” | Don’t — different shell, high cost, low extra signal |

If Core already covers the behavior, stop. Only add a Browser test when a **Browser-specific** regression or gap showed up (or you are extending this intentional smoke).

## How driving works (why not `getByRole`)

Avalonia.Browser paints to a **canvas**. Control ARIA is not exposed to the DOM today, so Playwright `getByRole` cannot find History / Add Animation.

**Debug-only bridge** (`BrowserUiAutomation` + `wwwroot/aeUiAutomation.js`):

1. Controls carry stable `AutomationProperties.AutomationId` (also useful on desktop a11y).
2. Debug builds register `globalThis.__aeUiAutomation`.
3. Playwright calls `clickByAutomationId` / `dumpUndoDescriptionsJson` — C# finds the control and raises a real Avalonia click, or reads `UndoManager`.

Not compiled into Release. Not activated by a public query string. Do not replace this with FeatureDemos wiring in shipping `App.axaml.cs`.

## When to add another test

Add a new spec only if **all** are true:

1. Headless/Core cannot catch the failure mode.
2. The scenario is Browser-host-specific (boot, FS APIs, Browser hotkey filter, bridge wiring, WASM render smoke).
3. You can keep the suite small (prefer one focused path over combinatorial coverage).

Prefer extending the existing smoke with one more step over adding many near-duplicate specs.

## Prerequisites and run

1. Node.js 20+ — `npm install` then `npm run install-browsers` in this folder.
2. **Debug** Browser host (Release has no bridge):

```powershell
dotnet run --project ../../src/AnimationEditor.Browser --no-launch-profile --urls "http://127.0.0.1:5420"
```

Use the printed ephemeral `App url:` as `AE_BROWSER_URL`.

```powershell
$env:AE_BROWSER_URL = "http://127.0.0.1:<port>/"
npm test
```

Or from repo root: `scripts/run-browser-ui-drive.ps1`.

## Outputs

Under `tools/AnimationEditorAvalonia/tests/_out/browser-ui-drive/` (gitignored):

- `history-after-ui-drive.png`
- `undo-descriptions.json`
- `ax-tree-before.json` / `phase0-findings.md` (ARIA spike evidence)

## Stable AutomationIds

| Control | Name | AutomationId |
|---|---|---|
| History tab | `History` | `history-tab` |
| History list | `Undo history` | `undo-history` |
| Animations tree | `Animations` | `animations-tree` |
| Add Animation | `Add Animation` | `add-animation` |
| Add Frame (hidden cmd) | `Add Frame` | `add-frame` |
| Show History menu | `Show History` | `show-history` |
| Undo / Redo | `Undo` / `Redo` | `history-undo` / `history-redo` |

Expected smoke Descriptions (locked in `BrowserUiDriveLabelTests`):  
`Add Frame to 'ColorCycle'`, `Add Animation 'NewAnimation'`.
