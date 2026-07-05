# Decision: live texture reload on the browser build, without a filesystem watcher

Status: **Decided and implemented** (see `AnimationEditor.Browser/BrowserFolderWatcher.cs`).
Related: [#535](https://github.com/vchelaru/FlatRedBall2/issues/535) M3,
`docs/BROWSER_SPIKE_FINDINGS.md`.

## Problem

On desktop, the Animation Editor's PNG folder-scan panel doesn't just list
textures once — it watches the folder (`PngFolderWatcher`, backed by
`FileSystemWatcher`) so editing a texture in an external image editor and
saving it is picked up automatically, no manual refresh. The browser build
has no `FileSystemWatcher` equivalent, and no local filesystem at all — every
file the app touches comes from an `IStorageFile`/`IStorageFolder` handle
granted through Avalonia's `IStorageProvider` (backed by the browser's File
System Access API).

The question: is there *any* way to detect an external edit to a picked
folder in the browser, or does live-watch have to be dropped for that build?

## Options considered

### Option 1 — `FileSystemObserver` (rejected)

[`FileSystemObserver`](https://developer.mozilla.org/en-US/docs/Web/API/FileSystemObserver)
is a real, purpose-built browser API — the closest thing to `FileSystemWatcher`
the web platform has. `new FileSystemObserver(callback)`, then
`observer.observe(handle, { recursive: true })`; the callback fires with
change records (appeared/disappeared/modified/moved) whenever something under
the observed handle changes.

**Status:** not experimental. Went stable in **Chrome 133** (January 29, 2025),
enabled by default, after an origin trial from Chrome 129 (September 2024).
Chromium-only — Chrome, Edge, Opera. No Firefox or Safari, same restriction as
the File System Access API generally.

**Why it doesn't fit here.** `observe()` requires the actual native
`FileSystemDirectoryHandle`/`FileSystemFileHandle` JS object — no other
representation works. Avalonia's `IStorageProvider` does wrap that same native
handle internally, but doesn't expose it. Pulling Avalonia's real
`src/Browser/Avalonia.Browser/Storage/BrowserStorageProvider.cs` from GitHub
confirms it directly:

```csharp
internal abstract class JSStorageItem : IStorageBookmarkItem
{
    internal JSObject? _fileHandle;
    internal JSObject FileHandle => _fileHandle ?? throw new ObjectDisposedException(nameof(JSStorageItem));
    ...
}
```

`internal` — not reachable from `AnimationEditor.Browser` (a different
assembly), and there is no public accessor anywhere in `IStorageFile`,
`IStorageFolder`, `IStorageBookmarkItem`, or `IStorageItem` that surfaces it.
An Avalonia GitHub discussion asking for exactly this kind of cross-platform
file-watch capability
([AvaloniaUI/Avalonia#11001](https://github.com/AvaloniaUI/Avalonia/discussions/11001))
got the answer that Avalonia is a GUI framework and this isn't planned — so
this is a permanent gap, not a "not yet" one.

**What using it for real would require.** Since Avalonia's picker can't hand
back a usable handle, `FileSystemObserver` only works by **not** going through
`IStorageProvider` for this feature at all:

1. A custom JS module calling `window.showDirectoryPicker()` directly,
   marshaled into C# via our own `[JSImport]` — bypassing Avalonia's picker
   entirely.
2. Our own folder enumeration (`handle.entries()`/`values()`), duplicating
   what Avalonia's own (internal, unreachable) `storageProvider.ts` already
   does.
3. Our own file reading (`handle.getFile()` → blob → stream), duplicating
   Avalonia's internal `OpenReadAsync`.
4. `new FileSystemObserver(callback).observe(handle, { recursive: true })`,
   with the callback marshaled back to C# via `[JSExport]`.

**Downsides of that custom route, weighed against staying inside
`IStorageProvider`:**

| | Custom (`FileSystemObserver`) | Stay inside `IStorageProvider` |
|---|---|---|
| Folder picking | Reimplemented from scratch in our own JS | Already built, already correct |
| Two grants? | Yes — Avalonia's picker (for loading) and a separate custom one (for watching) can't share a handle, so the user grants folder access twice for one folder | One grant covers both |
| Portability | Permanently browser-only code, a real fork from every other platform-agnostic code path in this app so far | Same code path Avalonia already runs on every platform it supports |
| Interop risk | Hand-rolled `JSObject` lifetime, promise/callback marshaling, permission-error handling — all things Avalonia already gets right today | None — it's a maintained library |
| Browser coverage | Chromium only, same as the polling option | Chromium only (File System Access API itself is Chromium-only) — no coverage difference to justify the extra risk |
| Maintenance | No upstream fix coming (per #11001) — this is forever code, not a stopgap | N/A |

Net: a large amount of duplicated, permanently-forked, higher-risk code, for
zero improvement in browser coverage over the alternative below.

### Option 2 — poll `Size`/`DateModified` and diff (chosen)

`IStorageItem.GetBasicPropertiesAsync()` is **public**, already-integrated
Avalonia API (works identically on every `IStorageProvider` backend), and
returns exactly the metadata needed to detect an edit without re-reading file
content: `Size` and `DateModified`. Reusing the *same* `IStorageFolder`
handle the user already granted via Open Folder means no second permission
prompt, no parallel picker, no raw JS interop.

Trade-off: this is detection by polling (every 2s), not a push notification —
a change can take up to one poll interval to surface, and it costs one
`GetItemsAsync()` + `GetBasicPropertiesAsync()` per file every interval while
a folder is open. For a texture-editing workflow (edits are seconds-to-minutes
apart, not needing millisecond reaction time), this is a non-issue.

## Decision

Implemented Option 2:

- **`FolderSnapshotDiff.FindChanged`** (`AnimationEditor.Core/IO/FolderSnapshotDiff.cs`) —
  pure comparison between two named `(Size, DateModified)` snapshots, returning
  which names changed. New/deleted files are deliberately not reported (a
  polling loop's "first seen" pass already establishes the baseline without
  them counting as edits). Has zero Avalonia dependency, so it's unit-tested
  without a browser (`FolderSnapshotDiffTests.cs`, 6 tests).
- **`BrowserFolderWatcher`** (`AnimationEditor.Browser/BrowserFolderWatcher.cs`) —
  wraps the diff with a `DispatcherTimer` (2s interval) polling
  `IStorageFolder.GetItemsAsync()` + `GetBasicPropertiesAsync()` for every
  `.png` in the folder Open Folder already granted.
- Detected changes **queue up rather than auto-applying**, surfaced as a
  "Reload Changed Textures (N)" button. This matches "see a diff, prompt the
  user to refresh" rather than silently swapping a texture out from under an
  in-progress edit — a deliberate choice, since the underlying constraint
  here was never "we can't read the file," only "we can't get pushed a
  notification that it changed." Reading it the moment a diff is detected was
  always possible; the button is a UX choice, not a technical requirement.
  Clicking it calls `ThumbnailService.InvalidatePath` + `SeedTexture` for each
  changed file and invalidates the preview.

## Consequences

- Live-watch works today on Chromium-based browsers (Chrome, Edge, Opera) —
  the same set File System Access API supports at all. Firefox/Safari have
  no folder-open capability in this build regardless of which option was
  chosen, so this decision doesn't change their coverage either way.
- If Avalonia ever exposes the native handle publicly (or `FileSystemObserver`
  becomes broadly cross-browser, which would require Firefox/Safari to adopt
  the underlying File System Access API first — not indicated as planned by
  either as of this writing), revisit: a push-based observer would reduce
  poll overhead and detection latency, but is not worth the current
  duplication/fork cost for that gain alone.
- Not yet verified end-to-end in a live browser: exercising a real folder
  pick + external file edit needs a human driving the native OS folder
  picker, which no automation tool in the development environment used to
  build this could do (see `BROWSER_SPIKE_FINDINGS.md` for the same caveat
  on Open Folder/Save As/drag-drop).
