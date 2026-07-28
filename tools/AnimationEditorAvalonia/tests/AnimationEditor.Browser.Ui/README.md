# AnimationEditor.Browser.Ui (#690)

External **Playwright** harness (Decision **C2**) for UI-driven verification of
`AnimationEditor.Browser`. Does **not** wire `?demo=` / `FeatureDemos` into shipping App code.

## Decisions

| Decision | Choice | Notes |
|---|---|---|
| A — done bar | **A2** assertable History undo labels | Via DEBUG dump of `UndoManager` Descriptions |
| B — surface | **B1** `AutomationProperties` / AutomationId | Names land on controls; see Phase 0 |
| C — harness | **C2** external Playwright | `tests/AnimationEditor.Browser.Ui/` |

## Phase 0 spike (Avalonia.Browser 12.0.1) — B1 DOM/ARIA = NO-GO

After setting `AutomationProperties.Name` / `AutomationId` on History, tree, Add Animation:

- CDP `Accessibility.getFullAXTree` exposes **only the page title**
- DOM has **no** Avalonia control `aria-*` (canvas host only; `body.innerText` empty)
- Playwright `getByRole('button', { name: 'Add Animation' })` cannot find controls

**Go/no-go:** pure Browser ARIA for Playwright is a dead end on 12.0.1. Keep B1 names for desktop a11y + as stable AutomationIds.

**Phase 1 path:** DEBUG-only `globalThis.__aeUiAutomation` (`BrowserUiAutomation` + `wwwroot/aeUiAutomation.js`) clicks by AutomationId and dumps undo Descriptions. `#if DEBUG` only — not in Release, not query-string activated.

## Prerequisites

1. Node.js 20+
2. From this folder: `npm install` then `npm run install-browsers`
3. **Debug** Browser host (Release omits the bridge):

```powershell
dotnet run --project ../../src/AnimationEditor.Browser --no-launch-profile --urls "http://127.0.0.1:5420"
```

Use the printed ephemeral `App url:` as `AE_BROWSER_URL`.

## Run

```powershell
$env:AE_BROWSER_URL = "http://127.0.0.1:<port>/"
npm test
```

Or from repo root: `scripts/run-browser-ui-drive.ps1`

## Smoke script

1. Wait for canvas + `__aeUiAutomation`
2. Click `add-frame` → `add-animation` → `history-tab`
3. Assert undo Descriptions contain `Add Frame to 'ColorCycle'` and `Add Animation 'NewAnimation'`
4. Screenshot → `tests/_out/browser-ui-drive/history-after-ui-drive.png`

## Stable AutomationIds (B1)

| Control | Name | AutomationId |
|---|---|---|
| History tab | `History` | `history-tab` |
| History list | `Undo history` | `undo-history` |
| Animations tree | `Animations` | `animations-tree` |
| Add Animation | `Add Animation` | `add-animation` |
| Add Frame (hidden cmd) | `Add Frame` | `add-frame` |
| Show History menu | `Show History` | `show-history` |
| Undo / Redo | `Undo` / `Redo` | `history-undo` / `history-redo` |
