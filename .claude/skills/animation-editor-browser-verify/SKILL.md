---
name: animation-editor-browser-verify
description: >-
  Visual proof on Animation Editor WASM without shipping demo hooks. Triggers:
  browser History screenshot, WasmAppHost ports, canvas wait, FeatureDemos.
---

# Animation Editor — Browser Visual Verify

Use when proving a UI change in **`AnimationEditor.Browser`**. Prefer **desktop DocScreenshots + Core.Tests** when that already covers the behavior — browser capture is optional extra evidence.

Shared drive scripts: **`AnimationEditor.Core.Demo.FeatureDemos`** (internal; InternalsVisibleTo tests/DocScreenshots only).

## Do not pollute shipping App code

**Landmine:** never commit `?demo=` / `FeatureDemos.TryRun` wiring into `App.axaml.cs` `BuildView` (or Desktop `MainWindow`). A query-param backdoor mutates undo state on any deployed build.

Default proof path:
1. Unit-test labels via Core (`CommandDescriptionTests` / `FeatureDemosTests`).
2. Desktop History PNGs via DocScreenshots `_ScratchCapture` + `FeatureDemos.TryRun`.

Browser screenshot only if desktop isn't enough: add a **temporary local** `#if DEBUG` hook, capture, **revert before merge**.

## Run and find the real URL

```
dotnet run --project tools/AnimationEditorAvalonia/src/AnimationEditor.Browser --no-launch-profile --urls "http://127.0.0.1:5420"
```

WasmAppHost often **ignores** `--urls` and prints `App url: http://127.0.0.1:<ephemeral>/`. Use that host. Port-in-use on launchSettings HTTPS → kill the listener or keep `--no-launch-profile`.

## Wait for load

Avalonia **canvas** — `document.body.innerText` stays empty. Poll `canvas` count ≥ 1 and splash gone, then MCP screenshot. No DOM refs for sidebar tabs.

## After capture

Copy PNGs to `tools/AnimationEditorAvalonia/tests/_out/<feature>/` beside desktop output.
