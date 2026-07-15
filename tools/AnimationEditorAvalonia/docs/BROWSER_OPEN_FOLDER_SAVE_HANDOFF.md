# Handoff: Browser Open Folder → Save without dialog (write permission)

> **Session log (updated 2026-07-15):** See [`BROWSER_OPEN_FOLDER_SAVE_SESSION_HANDOFF.md`](BROWSER_OPEN_FOLDER_SAVE_SESSION_HANDOFF.md) for everything tried, boot/SRI fixes, root causes found, and current code state. This file remains the **product spec**.

Status: **Working in Chrome/Edge, as designed.** Two real crashes were found and fixed (a
reflection-disabled JSON crash on Open Folder, and a folder-enumeration crash now hardened against
— see session log §1/§3). The "write permission: denied" reports across multiple prior sessions
were **not a bug** — testing had been happening in Brave, which does not implement
`showDirectoryPicker` at all (see session log §2). This doc's original design (below) is confirmed
correct; **do not re-attempt any of the abandoned earlier approaches** documented in the session
log's "Everything tried" table (rows A1–A7) — they were superseded by what's described here.
Date: 2026-07-12 (original), updated 2026-07-15
Worktree: `C:\Users\devin\OneDrive\Documents\Repos\FlatRedBall2\.claude\worktrees\web-open-folder-save`  
Branch: `web-open-folder-save`  
App: `tools/AnimationEditorAvalonia/src/AnimationEditor.Browser`  
Dev URL: `http://localhost:5420`  
Related: [#535](https://github.com/vchelaru/FlatRedBall2/issues/535)

## Goal (product)

After **Open Folder** loads a valid `.achx` from a user-picked folder:

- **Save** writes back to that same `.achx` **with no OS/browser save dialog**.
- **Save As** is the path that shows a picker.

Desktop already behaves this way. Web must match.

## Decision

**No separate web-only Open Folder button.** File → Load Folder uses Avalonia
`OpenFolderPickerAsync` (same menu as before). Write access comes from:

1. Early `index.html` patch of `showDirectoryPicker` forcing `{ mode: 'readwrite' }`
   before Avalonia's `storage.js` / `native-file-system-adapter` closes over it.
2. Belt-and-suspenders: `patchAvaloniaStorageProvider()` wraps
   `StorageProvider.selectFolderDialog` the same way.

Load uses Avalonia streaming reads. Save uses the unwrapped native
`FileSystemDirectoryHandle` (`NativeReadWriteFile` → `createWritable`).

A hidden off-screen `#frb-open-folder` remains in the DOM for experiments but is
not shown and is not the primary path.

## Manual verify

```powershell
cd tools/AnimationEditorAvalonia/src/AnimationEditor.Browser
.\run-browser.ps1
# Wait for: App url: http://localhost:5420/
# (dotnet run --urls http://localhost:5420 does NOT work here -- WasmAppHost ignores --urls;
# use run-browser.ps1 or the launch profile, see session log D2.)
```

1. Open in **Chrome or Edge — not Brave** (Brave doesn't implement `showDirectoryPicker`, see
   session log §2). Hard refresh (Ctrl+Shift+R). Clear site data if prior tests stuck.
2. **File → Load Folder…** — picker should offer edit access.
3. Status should show `[write:granted]`.
4. Edit → **Save** — no picker; file updates on disk.
5. **Save As** still prompts.

### Untested residue (TDD note)

DOM gesture + `showDirectoryPicker` cannot be unit-tested in this repo (no Browser test
project; OS picker is a security boundary). Covered: `WritePermissionGate` pure mapping.
Wiring left untested by design (same category as `LocalStorageInterop` /
`DownloadInterop`).

## Explicit non-goals

- Don't use manual `btoa` loops for large PNG reads (use `FileReader.readAsDataURL`).
- Don't automate the OS folder picker from the agent.
- Don't change desktop `AnimationEditor.App`.
- Don't re-attempt the abandoned approaches in session log rows A1–A7 (direct `[JSImport]`
  `showDirectoryPicker` calls from menu handlers, visible web-only Open Folder button, etc.) —
  all superseded by the `patchAvaloniaStorageProvider` + `OpenFolderPickerAsync` design above,
  confirmed working in Chrome/Edge as of 2026-07-15.
