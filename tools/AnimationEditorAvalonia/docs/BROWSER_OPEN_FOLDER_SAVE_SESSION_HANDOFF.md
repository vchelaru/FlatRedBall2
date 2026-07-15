# Session handoff: Browser Open Folder → Save (2026-07-12, last updated 2026-07-15)

**Supersedes status in:** [`BROWSER_OPEN_FOLDER_SAVE_HANDOFF.md`](BROWSER_OPEN_FOLDER_SAVE_HANDOFF.md) (original plan — still the product spec).

**Worktree:** `C:\Users\devin\OneDrive\Documents\Repos\FlatRedBall2\.claude\worktrees\web-open-folder-save`
**Branch:** `web-open-folder-save`
**App:** `tools/AnimationEditorAvalonia/src/AnimationEditor.Browser`
**Dev URL:** `http://localhost:5420` (use `run-browser.ps1` from **your own terminal** — do not let an
agent start it in a background shell that later ends, and don't pass `--urls` manually)
**Related:** [#535](https://github.com/vchelaru/FlatRedBall2/issues/535)

---

## Product goal (unchanged)

After **File → Load Folder…** loads a valid `.achx`:

- **Save** / Ctrl+S writes back to that same file **with no picker** (desktop parity).
- **Save As** keeps the picker.

---

## TL;DR — current status (2026-07-15)

| Area | Status |
|------|--------|
| App boot / spinner | Working — see boot section below |
| File → Load Folder — crash #1 (`JsonSerializerIsReflectionDisabled`) | **Fixed.** Was a real bug, reproducible in any browser. See §1. |
| File → Load Folder — crash #2 (`NotFoundError` during enumeration) | **Hardened, not eliminated.** Root cause isolated to something outside this repo's code (very likely local Windows security software); the app now degrades gracefully instead of crashing when it happens. See §3. |
| Save without dialog / write-permission architecture | **Confirmed correct in Edge.** Was never actually broken — every earlier "denied" result was reproduced in **Brave**, which does not implement `showDirectoryPicker` at all. See §2. |

**Two previous sessions' "freeze" reports were the same underlying issue as §1** — an unhandled
exception silently aborting the single-threaded WASM runtime, which looks like a hang (canvas goes
dead, no error box) rather than a catchable error. Neither prior session had console access to see
the actual exception.

**Nothing committed yet** as of this doc update — see PR prep section at the bottom for what's
being staged.

---

## §1 — Crash #1: `JsonSerializerIsReflectionDisabled` (FIXED)

Added timed `Console.WriteLine` diagnostics through the Open Folder chain (`App.axaml.cs`'s
`LogOpenFolderStep`, plus `console.log` in `nativeFolder.js`) to get real console visibility for
the first time — `Console.WriteLine`, not `Debug.WriteLine`: only the former reliably reaches the
browser DevTools console from WASM with no listener setup. (The existing
`patchAvaloniaStorageProvider` failure log used `Debug.WriteLine` and was silently invisible the
whole time — fixed as part of this.)

With real logging, the "freeze" turned out to be:
```
[OpenFolder] ...ms: LoadFromNativeDirectoryAsync start (writeState=denied)
[MONO] Process terminated.
Unhandled Exception:
System.InvalidOperationException: JsonSerializerIsReflectionDisabled
   at System.Text.Json.JsonSerializer.Deserialize[String[]](String json, JsonSerializerOptions options)
   at AnimationEditor.Browser.NativeFolderInterop.ListFileNamesAsync(JSObject dirHandle) in NativeFolderInterop.cs:line 103
MONO_WASM: Assert failed: .NET runtime already exited with 1 native code called abort()
```

`ListFileNamesAsync` called `JsonSerializer.Deserialize<string[]>(json)` — reflection-based.
`Microsoft.NET.Sdk.WebAssembly` disables reflection-based `System.Text.Json` at runtime,
independent of Debug/Release/trimming settings — **exact same root cause already documented and
fixed for the Export button** (see `AnimationEditor.Core/Export/PixiJsJsonContext.cs` and
`docs/BROWSER_EXPORT_POLISH_DECISION.md` §"Real bug found and fixed"). WASM is single-threaded, so
one unhandled exception aborts the *entire* Mono runtime — hence "freeze," not a catchable error.
The repeating `MONO_WASM: Assert failed` console spam some testers saw is just a leftover
`setInterval` (Avalonia's own render/input timer) still firing into the already-dead runtime every
tick — noise from the one crash, not new failures.

**Fix** (TDD: failing test written first): added `AnimationEditor.Core.IO.NativeFolderJsonContext`
— a `public` source-generated `JsonSerializerContext` for `string[]`, mirroring `PixiJsJsonContext`
but `public` (not `internal`) because the only caller lives in the separate `AnimationEditor.Browser`
assembly, which Core's `InternalsVisibleTo` does not cover. Swapped `ListFileNamesAsync`'s
reflection-based `Deserialize<string[]>` for it. 3 new tests in `NativeFolderJsonContextTests.cs`.
**Live-confirmed fixed twice** in real Chrome-family browsers — Open Folder completes the full
chain with no crash.

Checked the whole `AnimationEditor.Browser` project for other unguarded
`JsonSerializer.Deserialize<T>`/`Serialize<T>` calls — this was the only one. **If new ones get
added later** (to `AnimationEditor.Browser`, or to `AnimationEditor.Core` code Browser calls),
re-check — desktop tests will never catch this since desktop has reflection-based serialization
enabled.

---

## §2 — Write permission: was never broken, testing happened in the wrong browser

Extensive live diagnosis (documented in full in the "Everything tried" table below, rows A1–A9)
chased why picked-folder write permission always came back `denied` in ~2ms with zero visible
browser UI. Two real findings came out of that process, then a decisive one:

**`patchAvaloniaStorageProvider`'s wrapped `selectFolderDialog` logged the exact branch it took**
once instrumented: `falling back to original (preferPolyfill or no native picker)` with
`preferPolyfill: false` — the *only* way that branch triggers is
`typeof globalThis.showDirectoryPicker !== "function"`. Confirmed directly in the user's own
DevTools console: `typeof window.showDirectoryPicker` → **`"undefined"`**. Browser: **Brave**, not
Chrome — all testing up to that point had assumed Chrome because Brave's DevTools UI is visually
identical (same Chromium DevTools).

**Brave deliberately does not implement `showDirectoryPicker`.** Confirmed via Brave's own
community forum: a permanent "deviation from Chromium" (privacy/fingerprinting rationale), not a
missing flag — `brave://flags` has no toggle for it. See
[Brave community thread](https://community.brave.app/t/how-to-enable-showdirectorypicker-api-on-brave/527246),
[brave-browser#18979](https://github.com/brave/brave-browser/issues/18979),
[brave-browser#44411](https://github.com/brave/brave-browser/issues/44411).

This retroactively explains every symptom chased across this and prior sessions, with zero
remaining mystery:
- Both patches (the `index.html` global override and `patchAvaloniaStorageProvider`) correctly
  no-op when `showDirectoryPicker` doesn't exist — intentional fallback behavior, not a bug.
- Avalonia's `storage.js` picker helper falls through to its `<input type="file" webkitdirectory
  multiple>` **polyfill** — this is the dialog that always appeared; it looks like a folder picker
  but is a legacy multi-file-select input, not the File System Access API.
- The polyfill's `getDirHandlesFromInput` constructs its `FolderHandle` with `writable` **hardcoded
  to `false`** — not a permission decision, a structural constant. `queryPermission`/
  `requestPermission` on a polyfill handle just check that hardcoded flag and resolve
  synchronously — explaining the ~2ms "denied" with no UI, identically across every folder and
  every cache clear, because no real permission system is involved on this path at all.
- **No code change in this repo could ever have fixed this for Brave.** The polyfill path is
  structurally incapable of readwrite, by design.

**Confirmed working correctly in real Edge**, live:
```
[OpenFolder] globalThis.showDirectoryPicker patched wrapper called, options: {mode: 'readwrite'}
[OpenFolder] patched StorageProvider.selectFolderDialog called, preferPolyfill: false
[OpenFolder] queryPermission(readwrite) initial state: granted
[OpenFolder] EnsureReadWriteAsync returned (writeState=granted)
```
Both patches fired correctly, `mode:'readwrite'` was honored by the picker, and permission was
`granted` immediately with no extra prompt — exactly the intended design. **The picker-only
write-permission architecture (this doc's whole original premise) is correct as designed.**

One incidental finding along the way, also fixed: `_framework/storage.js` (Avalonia's bundled
picker helper) captures `var Re = globalThis.showDirectoryPicker` **once at module top-level
eval**, not per-call — so a global-object monkey-patch applied after that module first evaluates
can never reach it. Confirmed live the `index.html` early-patch wrapper never fires on Avalonia's
call path for this reason. This is moot for the write-permission fix itself (the *second* patch,
`patchAvaloniaStorageProvider`, reassigns the exported `StorageProvider.selectFolderDialog` method
directly and calls `globalThis.showDirectoryPicker` itself, sidestepping the stale `Re` reference
entirely — which is why it works), but is a real landmine for any *other* future monkey-patch of
this same global.

---

## §3 — Crash #2: `NotFoundError` during folder enumeration (hardened, root cause outside this repo)

A **different** unhandled exception, found once write permission started actually being granted
(in Edge): right after `LoadFromNativeDirectoryAsync start (writeState=granted)`,
```
Unhandled Exception:
NotFoundError: A requested file or directory could not be found at the time an operation was processed.
```
crashing the runtime the same way (single-threaded WASM — any unhandled exception aborts
everything). This happens inside `NativeReadWriteFolder.GetItemsAsync()` →
`NativeFolderInterop.ListFileNamesAsync` → `nativeFolder.js`'s `listFileNames`, during
`dirHandle.entries()` enumeration — before it lists even one entry.

### Isolation (all live-tested, in order)

- **Folder contents** (hidden files, lock files, `.tmp`, `.vs/`) — ruled out. The original test
  folder was accidentally the user's real `Downloads` (60+ GB, thousands of unrelated personal/
  financial files — not a real test fixture; never point experimental file-system code at that
  again). Identical crash on a clean, freshly-created 2-file fixture (`player.achx` + `player.png`,
  copied from the bundled sample) with nothing else in the folder.
- **Drive letter / volume type** — ruled out. Identical crash on both `C:` and `D:`, both plain
  Fixed NTFS volumes (`Get-Volume` confirmed).
- **Multi-threaded WASM / worker-realm handle mismatch** — ruled out. A context probe confirmed
  `hasWindow=true isWorkerScope=false dirHandle.constructor=FileSystemDirectoryHandle
  dirHandle.kind=directory dirHandle.name=<correct>` — a genuinely correct, main-thread, properly-
  typed native handle for the exact folder picked.
- **Iterator vs. handle isolation (decisive)** — stepped `dirHandle.entries()` manually instead of
  relying on `for await`'s sugar to hide which step failed:
  - `dirHandle.entries()` (obtaining the async iterator) — **succeeds**.
  - `iter.next()` (the first enumeration step) — **fails with `NotFoundError`**.
  - `dirHandle.getFileHandle("player.achx")` (direct named lookup, no enumeration) on the *same
    handle* — **succeeds**.

**Conclusion: the handle is completely valid and functional. Only directory *enumeration*
(`entries()`/`keys()`/`values()`) fails; direct named-file access on the identical handle works
perfectly.** Not fixable by anything in this repo's code — the permission/interop/handle chain is
all proven correct.

### Root cause: environmental, not this repo — likely Windows security software

Chromium's File System Access implementation is path-based (each operation re-resolves against a
file path — no exact matching public bug report found, but the mechanism fits), so local security
software can plausibly intercept a directory-*listing* syscall differently than a single
named-file-*open* syscall, explaining exactly this asymmetry.

**Checked and ruled out on the test machine:**
- Windows Security → Controlled Folder Access — confirmed **off**.
- NTFS ACLs on the test folder (`icacls`) — completely standard inherited permissions, Read &
  Execute includes List Folder Contents. Not a Traverse-without-List permission gap.
- Only Windows Defender is registered (`Get-CimInstance -Namespace root/SecurityCenter2
  -ClassName AntiVirusProduct` — no third-party AV/EDR).

**Not fully confirmed:** Defender's real-time/on-access protection *is* active
(`RealTimeProtectionEnabled: True`, `OnAccessProtectionEnabled: True`) even with Controlled Folder
Access specifically off, and remains the leading suspect. Testing this further requires a Defender
exclusion (`Add-MpPreference -ExclusionPath ...`) — a security-setting change intentionally left
for the user to run themselves if still curious; it doesn't block anything below.

General web research (Chromium bug tracker, WICG file-system-access repo, production-app guidance)
turned up no exact public match for this specific asymmetry, but consistent general guidance:
treat File System Access failures (revoked permission, moved/locked files, blocked by security
software) as an **expected, recoverable condition**, not exceptional — never let one fail the whole
app.

### Fix: graceful degradation (independent of the exact external cause)

`openButton.Click`'s call to `LoadFromNativeDirectoryAsync` is now wrapped in
`try/catch (JSException)`. On failure: logs via the existing `LogOpenFolderStep` diagnostic,
formats a clear user-facing message via the new `AnimationEditor.Core.IO.OpenFolderLoadFailure
.FormatMessage` (pure, unit-tested — mentions antivirus/security software as a possible cause), and
shows it via `notifications.ShowErrorBanner` + `status.Text` — matching the existing Save-failure
UX pattern (`TryWriteToKnownFileAsync`'s banner-and-drop-handle behavior). The app stays usable
after this failure instead of the whole runtime crashing.

**Exploratory diagnostics from mid-investigation have been removed** now that the mystery is
closed — specifically the manual iterator-stepping probe and a hardcoded
`getFileHandle("player.achx")` lookup (test-fixture-specific, would have been dead weight/noise in
real usage). Kept: the general per-entry `console.log` in `listFileNames`'s real enumeration loop
(genuinely useful ongoing diagnostic, not session-specific), the `LogOpenFolderStep` timeline, and
the patch-success/permission-state logs from §1/§2 (all lightweight, general-purpose, low-noise —
fire once per Open Folder action, not per frame).

---

## Also worth doing (not done this session — flagged, not blocking)

**Detect missing `showDirectoryPicker` support and say so.** Right now a Brave user (or any browser
without the API) still sees a generic `write permission: denied` banner with no indication *why* —
they can't tell "this is a browser limitation, try Chrome/Edge" from "something is wrong, retry."
Checking `typeof globalThis.showDirectoryPicker !== "function"` at Open Folder time and showing a
specific message for that case would close this gap. Not implemented — the immediate ask this
session was hardening the crash, not this UX polish; worth a follow-up.

---

## Everything tried (chronological — kept for history; several rows superseded by §1–§3 above)

### A. Open Folder / write permission approaches

| # | Approach | Result |
|---|----------|--------|
| A1 | Avalonia `OpenFolderPickerAsync` + early `index.html` `showDirectoryPicker` patch | Dialog works; status often `[write:denied]`; Save fails |
| A2 | `patchAvaloniaStorageProvider()` in `nativeFolder.js` (wrap `selectFolderDialog` with `mode:'readwrite'`) | Confirmed working correctly in Edge (§2). Earlier "denied" results were Brave, not a patch bug. |
| A3 | `StoragePermissionInterop.EnsureReadWriteAsync` at **Save** time (menu click) | Chrome-family returns `denied` **silently** — WASM menu Save loses user activation; no permission UI |
| A4 | Visible HTML **Open Folder** button in `index.html` (Phase 1 plan) | User rejected separate web chrome — reverted to Avalonia menu |
| A5 | Hidden off-screen `#frb-open-folder` + `ClickOpenFolderButton()` from Avalonia Load | Never made primary; synthetic DOM click may not carry user activation |
| A6 | `RegisterFolderPickedHandler` + HTML button callback path | Wired in JS/C# but not kept as primary |
| A7 | **`PickFolderReadWriteAsync()`** JSImport — call `showDirectoryPicker({mode:'readwrite'})` directly from `openButton.Click` | **File → Load Folder did not open OS dialog at all** — WASM menu clicks are not a browser user activation |
| A8 | Restore Avalonia picker + **`EnsureReadWriteAsync` immediately after pick** (before load) + Save uses **`queryPermission` only** (no `requestPermission` at Save) | This is the current, working design (§2) |
| A9 | `NativeReadWriteFolder` + load/save entirely via native handle (`listFileNames`, `readFileBase64`, `writeFileBytes`) | Working; load path hardened against enumeration failures (§3) |
| A10 | Root-caused the "freeze" — see §1 | Fixed |
| A11 | Root-caused write-permission "denied" — see §2 | Not a bug; Brave doesn't support the API |
| A12 | Root-caused + hardened the second crash — see §3 | Hardened; exact external cause (likely Defender) not fully confirmed |

### B. Save path changes

| # | Change | Rationale |
|---|--------|-----------|
| B1 | `WritePermissionGate.cs` + unit tests | Testable mapping of `granted`/`denied` → allow Save |
| B2 | `WritePermissionGate.EvaluateSaveFromQueryState` | Save must use **query only**, not `requestPermission` |
| B3 | `TryWriteToKnownFileAsync` — removed `EnsureReadWriteAsync` at Save | Avoid silent `denied` from lost activation |
| B4 | `NativeWriteStream` — buffer then `createWritable` on dispose | Matches `OpenWriteAsync` shape |
| B5 | On Save failure: remove tab handle, show banner "Use Save As" | Avoid retry loop / second dialog |

### C. Other bugs fixed along the way

| # | Issue | Fix |
|---|-------|-----|
| C1 | **Load Folder did nothing** | Hidden command buttons (`openButton`, etc.) were never parented — `TopLevel.GetTopLevel` returned null. Fixed: parent `hiddenCommandButtons` in shell grid (still `IsVisible=false`). |
| C2 | `HttpClient` BaseAddress included query string from WasmAppHost | Strip query/fragment in `Program.cs` before sample fetch |
| C3 | `Program.Main` hung boot if optional JS init blocked | Reordered init; native folder init awaited before `StartBrowserAppAsync` |

### D. Dev server / loading spinner (major friction)

| # | Issue | What we tried | Outcome |
|---|-------|---------------|---------|
| D1 | Wrong port (`localhost:5428` vs `5420`) | `launchSettings.json` → 5420 only; `run-browser.ps1` | User must use printed URL; 5428 was stale/wrong session |
| D2 | `dotnet run --urls http://localhost:5420` ignored by WasmAppHost | Document: use `dotnet run` + launch profile only | Random port if `--urls` passed as arg |
| D3 | 404 + **SRI integrity failed** on `_framework/*.wasm` | Clean rebuild; kill stale processes | Root cause: **`cache: force-cache`** on stable virtual paths (`AnimationEditor.Core.wasm`) + stale HTTP cache after rebuild |
| D4 | Hot reload swapping wasm hashes | `<WasmEnableHotReload>false</WasmEnableHotReload>` in csproj | Helps |
| D5 | `<BlazorCacheBootResources>false</BlazorCacheBootResources>` | Added to csproj | Boot manifest still shows `force-cache` in dotnet.js; unclear if fully effective |
| D6 | Service worker stale assets | Unregister SW + clear Cache API in `index.html` before boot | Helps |
| D7 | `main.js`: `disableIntegrityCheck: true` + patch `fetch` for `/_framework/` → `cache: 'no-store'`, strip integrity | **Headless Playwright boot OK** (canvas appeared) | User still had to hard-refresh after server up |
| D8 | Boot error UI in `main.js` | Red box instead of endless spinner | Surfaces "Failed to fetch" when server down |
| D9 | Agent background shells killing dev server | User must run `run-browser.ps1` in **their own terminal** long-term | Ctrl+C / agent shell ending → "Failed to fetch". (Within a single Claude Code session, launching it via a persistent background task and keeping that session open works for testing — the underlying constraint is the *process* dying, not specifically "who starts it".) |
| D10 | `run-browser.ps1`'s own stale-process-killer can kill itself | The script's `Get-CimInstance Win32_Process \| Where CommandLine -like "*AnimationEditor.Browser*"` self-matches when invoked through a tool that embeds the full script path (containing "AnimationEditor.Browser") into the wrapper process's own command line, then `Stop-Process -Force`s itself mid-script | When launching via an agent tool that wraps commands this way, skip the stale-process-kill step (verify no real stale process first) and run `dotnet build` + `dotnet run --no-build` directly instead of the full script |

### E. Interop / JS lessons (don't re-learn)

| Topic | Finding |
|-------|---------|
| `Task<byte[]>` JSImport return | **Fails** (SYSLIB1072) — use base64 string + `Convert.FromBase64String` |
| `Task<JSObject?>` from `pickFolderReadWrite` | Builds, but **no dialog** when called from async WASM menu handler |
| PNG reads | Use `FileReader.readAsDataURL` in JS — manual btoa loops hang on large PNGs |
| `Debug.WriteLine` vs `Console.WriteLine` in browser-wasm | **`Debug.WriteLine` does not reliably reach the DevTools console.** `Console.WriteLine` does, with zero listener setup. Any diagnostic logging in this app must use `Console.WriteLine` |
| Avalonia menu → File System Access API | **Not a DOM user gesture** — direct `showDirectoryPicker` from `[JSImport]` after menu click does not open picker |
| Avalonia `OpenFolderPickerAsync` | **Does** open OS dialog (bridges to browser somehow) |
| `requestPermission` at Save | Unreliable from WASM; browsers often return `denied` with no UI if activation is lost |
| `_framework/storage.js` internals | Captures `var Re = globalThis.showDirectoryPicker` **once at module top-level eval**, not per-call — a global-object monkey-patch applied after this module first evaluates can never reach it. Also: Avalonia's own `StorageProvider.selectFolderDialog` never puts `mode` in its picker options at all — reassigning the exported method itself (not patching the global) is the only structurally-sound approach (`patchAvaloniaStorageProvider`) |
| `JsonSerializer.Deserialize<T>()` in browser code | **Throws `JsonSerializerIsReflectionDisabled`, crashes the whole Mono runtime** (single-threaded — any unhandled exception aborts everything, looks like a freeze/hang, not a catchable error). Confirmed twice now (Export button, Open Folder). Any reflection-based `JsonSerializer.Serialize`/`Deserialize<T>` call reachable from a browser code path is a dormant crash — must use a source-generated `JsonSerializerContext` instead (see `PixiJsJsonContext`, `NativeFolderJsonContext`) |
| Testing in Brave vs. Chrome/Edge | Brave's DevTools look identical to Chrome's (same Chromium DevTools) but Brave **does not implement `showDirectoryPicker`** (deliberate, permanent, no flag). Always confirm `typeof window.showDirectoryPicker` before debugging File System Access issues in an unfamiliar browser window |
| `dirHandle.entries()`/`keys()`/`values()` failing while `getFileHandle()` works | Real, reproducible asymmetry seen on Windows (§3) — directory *enumeration* can fail independently of named-file access on an otherwise fully valid, correctly-permissioned handle. Likely security-software interference with directory-listing syscalls specifically. Any code enumerating a picked directory should catch this and degrade gracefully, not assume enumeration succeeding is a given once permission is granted |

---

## Current architecture (as of this update)

```
File → Load Folder (menu → openButton.Click)
  → OpenFolderPickerAsync (Avalonia — dialog works)
  → TryGetNativeFileSystemHandle(rawFolder)
  → EnsureReadWriteAsync(nativeDir)      ← pick-time write grant; confirmed working in Edge (§2)
  → try:
      LoadFromNativeDirectoryAsync
        → NativeReadWriteFolder.GetItemsAsync() → ListFileNamesAsync
             (can throw JSException -- e.g. NotFoundError during enumeration, §3)
        → BrowserProjectLoader.TryLoadAsync
        → status suffix [write:…]; tabFileHandles[tab] = NativeReadWriteFile
        → BrowserFolderWatcher.StartAsync()
    catch (JSException):
      OpenFolderLoadFailure.FormatMessage(...) → status.Text + notifications.ShowErrorBanner(...)
      (app stays usable; no crash)

Save (menu / Ctrl+S)
  → QueryWritePermissionAsync only (WritePermissionGate) -- never requestPermission at Save
  → NativeWriteStream → writeFileBytes → createWritable
  → on denied: banner + drop handle, fall through to Save As next time
```

**Boot (`index.html` + `main.js`):**
- Cache/SW cleanup before module load
- Fetch patch: `/_framework/` → `no-store`, drop integrity
- `dotnet.withConfig({ disableIntegrityCheck: true })`

---

## Key files (created or materially changed)

| File | Role |
|------|------|
| `wwwroot/index.html` | Early readwrite patch (known dead code path for Avalonia's own call, see §2 — kept as a no-cost belt-and-suspenders for any *other* future caller of the global); boot cache/SW cleanup; fetch patch loader |
| `wwwroot/main.js` | Boot error UI; `disableIntegrityCheck` |
| `wwwroot/nativeFolder.js` | `pickFolderReadWrite`, `patchAvaloniaStorageProvider` (the patch that actually works, §2), hidden button, read/write helpers, `listFileNames` (hardened call site is in C#, §3) |
| `NativeFolderInterop.cs` | JSImport bridge; `ListFileNamesAsync` uses `NativeFolderJsonContext` (§1) |
| `NativeReadWriteFile.cs` / `NativeReadWriteFolder.cs` | Native handle I/O |
| `NativeWriteStream.cs` | Buffered write flush |
| `StoragePermissionInterop.cs` | Unwrap Avalonia handle; drag-drop write upgrade |
| `WritePermissionGate.cs` (Core/IO) | Pure permission gate (+ tests) |
| `NativeFolderJsonContext.cs` (Core/IO) | Source-generated `JsonSerializerContext` for `string[]` — fixes the crash in §1 |
| `OpenFolderLoadFailure.cs` (Core/IO) | Pure message formatter for the graceful-degradation fix in §3 (+ tests) |
| `App.axaml.cs` | Open/Save wiring, hidden command buttons fix, `LogOpenFolderStep` diagnostics, try/catch hardening around folder load (§3) |
| `Program.cs` | Sample fetch, JS init order |
| `AnimationEditor.Browser.csproj` | `WasmEnableHotReload=false`, `BlazorCacheBootResources=false` |
| `run-browser.ps1` | Clean rebuild + start on 5420 (see D10 for a self-kill caveat when run through certain tool wrappers) |
| `Properties/launchSettings.json` | `http://localhost:5420` |
| `tests/.../WritePermissionGateTests.cs` | 6 tests |
| `tests/.../NativeFolderJsonContextTests.cs` | 3 tests |
| `tests/.../OpenFolderLoadFailureTests.cs` | 3 tests |

---

## Repro steps (for next agent)

### Boot

```powershell
cd tools\AnimationEditorAvalonia\src\AnimationEditor.Browser
.\run-browser.ps1
# Wait for: App url: http://localhost:5420/
```

Open in **Chrome or Edge** (not Brave — it doesn't support the required API, see §2) at
`http://localhost:5420` → hard refresh (Ctrl+Shift+R), clear site data if needed.

### Open Folder → Save

1. File → Load Folder…
2. Pick any folder with a valid `.achx` + referenced texture. Use a small, dedicated test folder
   (e.g. copy `wwwroot/sample/player.achx` + `player.png` somewhere clean) — **do not point this at
   a real, large personal folder** (Downloads, Documents) both for safety and because a huge
   eclectic folder is more likely to hit whatever's causing §3's enumeration failure.
3. Expect `[write:granted]` in Chrome/Edge. If enumeration fails (§3), expect a red error banner,
   not a crash — verify this explicitly since it wasn't live-confirmed as of this doc update.
4. Edit something → Save → expect no picker, direct write.

---

## Tests

```powershell
dotnet test tools/AnimationEditorAvalonia/tests/AnimationEditor.Core.Tests
dotnet build tools/AnimationEditorAvalonia/src/AnimationEditor.Browser -c Debug
```

Full `AnimationEditor.Core.Tests` suite: 1599/1599 passing as of this update. `AnimationEditor.Browser`
rebuilds clean, 0 warnings/errors.

Browser E2E (Open Folder / Save) — **not automated**; manual only. No Browser test project exists
(DOM gesture + native file pickers are a security boundary that can't be driven from a test host).

---

## Explicit non-goals (unchanged)

- No desktop `AnimationEditor.App` changes
- No agent-driven OS folder picker automation
- No manual btoa read loops for PNGs

---

## Reference

- **Original spec:** [`BROWSER_OPEN_FOLDER_SAVE_HANDOFF.md`](BROWSER_OPEN_FOLDER_SAVE_HANDOFF.md)
- **Prior spike context:** `docs/BROWSER_SPIKE_FINDINGS.md` (if present on branch)
- **Agent transcript:** Cursor agent id `622d37aa-e690-41f6-ae1f-7ee704c69945`
