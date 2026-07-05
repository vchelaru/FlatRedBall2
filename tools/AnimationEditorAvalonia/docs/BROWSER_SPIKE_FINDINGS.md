# #535 M1/M2/M3/M4 Spike Findings — Animation Editor on Avalonia.Browser (WASM)

Tracking issue: [vchelaru/FlatRedBall2#535](https://github.com/vchelaru/FlatRedBall2/issues/535).

M1 asked one question: does the editor's SkiaSharp custom-draw rendering
(`WireframeControl`/`PreviewControl`'s `ICustomDrawOperation` +
`ISkiaSharpApiLeaseFeature`) work at all under Avalonia's Browser/WASM backend?
This spike added `src/AnimationEditor.Browser/`, a minimal standalone project
that reproduces that exact mechanism, and tried to answer it directly.

## Setup — works

- `dotnet workload install wasm-tools` installs cleanly against the repo's
  .NET 10 SDK (10.0.301).
- A `net10.0-browser` project referencing `Avalonia.Browser 12.0.1` and
  `SkiaSharp 3.119.3-preview.1.1` (the same versions `AnimationEditor.App`
  already uses) restores and builds with zero errors — no missing
  `browser-wasm` RID assets for either package.
- `dotnet run` boots a dev server, serves `index.html`/`main.js`/the WASM
  runtime, and every framework asset (including `SkiaSharp.wasm` and
  `Avalonia.Skia.wasm`) loads with a 200.
- Console tracing confirms the app reaches `Avalonia.BrowserSingleViewLifetime`
  (not `IClassicDesktopStyleApplicationLifetime` — **browser has no `Window`,
  only a single-view host**, same as any other Avalonia mobile/browser target)
  and successfully constructs and attaches the custom control.

## Blocker found (and since resolved): `AnimationEditor.Core` couldn't be referenced as-is

The issue's own risk list focused on SkiaSharp/`GrContext` and filesystem
access. It didn't flag this: `AnimationEditor.Core.csproj` project-referenced
`../../../../src/FlatRedBall2.csproj`, and for its `net10.0` TFM that pulled in
`MonoGame.Framework.DesktopGL` (desktop-only native bindings — Win32/X11/macOS).
That dependency chain has no `browser-wasm` build and could not be
restored/published for a browser target.

Investigation showed the coupling was incidental, not architectural: neither
`AnimationEditor.Core` nor `AnimationEditor.App` ever touched `Texture2D`,
`GraphicsDevice`, `ContentLoader`, or any other MonoGame type — their entire
footprint in the engine was the pure `.achx` data model (`AnimationFrameSave`,
`AnimationChainSave`, `ShapesSave`, `ColorOperation`, and the XML load/save
members of `AnimationChainListSave`). Those types only pulled MonoGame in
because they shared a file/assembly with the runtime bridge
(`AnimationChainListSave.ToAnimationChainList`/`BuildList`, which *does* need
`Texture2D`/`ContentLoader`).

**Fix landed:** the pure data model moved to a new, MonoGame-free project,
`src/Animation.Content/FlatRedBall2.Animation.Content.csproj` (multi-targets
`net8.0;net10.0`, zero MonoGame/KNI package references). The runtime-bridge
methods moved to `AnimationChainListSaveExtensions` in the main engine
assembly, as extension methods on the same types — call-site syntax is
identical (`save.ToAnimationChainList(content)` either way), so no consumer
anywhere in the engine, samples, or tests needed to change. `FlatRedBall2.csproj`
now references the new project; `AnimationEditor.Core.csproj` references
*only* the new project, not the full engine.

Verified end-to-end: this project (`AnimationEditor.Browser`) now has a real
`ProjectReference` to `AnimationEditor.Core.csproj` and builds clean for
`net10.0-browser` (see `AnimationEditor.Browser.csproj`). Full regression
suite stayed green throughout (1093 `FlatRedBall2.Tests` + 1433
`AnimationEditor.Core.Tests` + 550 `AnimationEditor.App.Tests`, before and
after, identical counts) — this was a pure structural move, no behavior
changed anywhere.

M2 ("content without FS") is now unblocked: Core's IO/state layer
(`ProjectManager`, `AppState`, `IoManager`, etc.) is reusable from a browser
head for real, not just in principle.

## Rendering — CONFIRMED. M1 is closed.

Verified in a real, foregrounded Chrome tab (not the sandboxed headless
preview browser — see below for why that one couldn't confirm this):

- The view resized correctly (`OnSizeChanged: 2560, 1277`, matching the real
  viewport at device pixel ratio) and the custom draw op ran to completion
  with no exception: `ISkiaSharpApiLeaseFeature = present`, `got SkCanvas,
  isGpu=False`.
- **Backend is CPU/software, not GPU/ANGLE** (`lease.GrContext` is `null`) —
  at least in this environment/build. Worth confirming whether GPU is
  available in other browsers/machines before assuming CPU-only.
- Pixel readback (`canvas.getContext('2d').getImageData(...)`) matched the
  expected alpha-blended colors exactly: background `(24,28,36)` (the raw
  clear color), rectangle fill `(67,131,208)` (blue `80,160,255` at alpha
  200/255 over that background), circle fill `(223,124,5)` (orange
  `255,140,0` at alpha 220/255) — down to the integer, not approximate.
- A follow-up screenshot (after the CSS fix below) shows it visually too:
  the blue rectangle, orange circle, and both text lines render exactly as
  authored.

**Answer to M1's core question: yes — `ICustomDrawOperation` +
`ISkiaSharpApiLeaseFeature` work on Avalonia's Browser/WASM backend.**

### Gotcha found along the way: `.avalonia-native-host` needs an explicit transparent background

The canvas painted correctly the whole time, but was invisible on screen —
Avalonia's `.avalonia-native-host` div (an absolutely-positioned sibling that
hosts native text-input elements for IME/accessibility) sits on top of the
canvas in DOM/paint order and picked up an opaque `rgb(20,21,21)` background
(observed on `body`/`#out`/the canvas too — looked like a browser auto-dark-theme
default for unstyled elements, not anything Avalonia or this project set).
Fixed by adding `.avalonia-native-host { background: transparent !important; }`
to `wwwroot/index.html` — the official `dotnet new avalonia.xplat` template's
richer CSS apparently avoids this some other way; this minimal spike's
stripped-down `index.html` didn't carry it over. **Any real browser head
should double-check this isn't silently hiding content.**

### Secondary finding, not chased further: screenshot/compositor capture was flaky

Chrome's own screenshot capture (`Page.captureScreenshot` via CDP) intermittently
hung for 30s or returned a stale cached frame while this page was up, even
though direct pixel readback and DOM queries via `eval` always returned
instantly and correctly. This is the same class of symptom that made the
*headless* preview browser unable to confirm rendering at all earlier in this
investigation (a `ResizeObserver`-driven resize of the canvas never fired
there, leaving the view stuck at a degenerate `1×1`). Worth keeping in mind
if the real editor's UI needs to stay responsive/interactive under automated
testing — but it did not block getting a correct, confirmed render here, and
is not a blocker for M1.

## M2 (content without FS) — CONFIRMED. The real `PreviewControl` plays a bundled sample.

M2 asked: can the editor load a bundled read-only sample and play its
animations in-browser with zero file I/O? Yes, using the *actual*
`WireframeControl`/`PreviewControl` (not a stand-in like the M1 spike's
`SpikeCanvas`).

**Extracted `AnimationEditor.Views`.** The same investigation approach as the
Core fix: `WireframeControl`, `PreviewControl`, and their support types
(`DrawTimeOverlay`, `InspectableImage`, `ZoomPresetStepper`,
`HandleCursorMapper`, `GuideCursorResolver`, `CanvasPalette`,
`FrameColorFilter`, `FramePreviewOpacity`, `ThumbnailService`) turned out to
have zero dependency on `Avalonia.Desktop` — confirmed by grepping every
`using` and fully-qualified reference before moving them. They moved to a new
`AnimationEditor.Views` project (namespaces left unchanged, so no call site
changed) that both `AnimationEditor.App` and `AnimationEditor.Browser`
reference. One gotcha hit during the move: `AnimationEditor.App.csproj`
overrides `<AssemblyName>` to `AnimationEditor` (for the macOS Dock label), so
`InternalsVisibleTo` in the new project has to target `AnimationEditor`, not
`AnimationEditor.App` — the project name and assembly name aren't the same
thing, and internal members that used to be same-assembly-visible needed it
spelled out explicitly once split across assemblies.

**Two small, TDD'd `src/` fixes were needed** — both were incidental
disk-existence checks, not architectural blockers:
- `ProjectManager.LoadAnimationChain(fileName, preParsed)` required
  `fileName.Exists()` even when the caller already supplied `preParsed`
  content. Fixed: the existence check now only runs on the "read it ourselves"
  path; a `preParsed` caller was always trusted for the XML content itself, so
  trusting it on existence too is consistent, not a new leniency.
- `ThumbnailService.ResolveTexturePath`/`GetBitmap` required the texture to be
  a real file on disk. Added `SeedTexture(name, bitmap)` to register an
  already-decoded bitmap under a texture name; `ResolveTexturePath` now checks
  `BitmapCache` before ever touching disk.

Both were covered by failing-test-first pairs (`ProjectManagerLoadTests.cs`,
`ThumbnailServiceTests.cs`) before the fix, per the repo's TDD discipline.

**Sample content.** A tiny bundled sample lives at
`AnimationEditor.Browser/wwwroot/sample/`: `player.png` (a generated 4-frame,
32×64-per-frame color-cycle sheet: red/lime/sky-blue/gold, each with a small
dot marker) and `player.achx` (one `ColorCycle` chain, `CoordinateType=UV` so
no PNG-header disk read is ever needed even in principle). `Program.Main`
fetches both over HTTP via `HttpClient` *before* Avalonia starts (`HttpClient`
needs an absolute `BaseAddress` — main.js passes the page URL as `args[0]` via
`runMain(mainAssemblyName, [location.href])`, which is what `BaseAddress` is
set from). `App.axaml.cs` then wires the same service graph
`MainWindow`/`TestServices` build on desktop (minus file-association/native-menu/
single-instance bits), loads the sample through `ProjectManager.LoadAnimationChain(
path, preParsed: acls)`, seeds the decoded PNG via `ThumbnailService.SeedTexture`,
and sets `selectedState.SelectedChain` — which auto-plays per
`PreviewControl.OnSelectionChanged`'s existing "selecting a whole animation
auto-plays" behavior. No new playback-triggering code was needed.

**Verified in a real Chrome tab:** the actual `PreviewControl` renders — rulers,
tick marks, and all — showing the sprite frame, and the frame visibly advances
(confirmed red → gold between two screenshots a second apart) without any
manual interaction. Screenshot capture worked fine here (unlike the M1 spike),
once one environment issue was worked around (below).

### Gotcha: IPv6 loopback (`::1`) was flaky for large concurrent WASM asset fetches in this sandbox

The dev server binds both `::1` and `127.0.0.1` when told to listen on
`localhost`. With M1's smaller dependency graph this was never a problem, but
once `AnimationEditor.Browser` started pulling in Core + Views (many more
framework assemblies fetched concurrently at boot), requests for the
*project's own* two output files (`AnimationEditor.Browser.{hash}.wasm`/`.pdb`)
intermittently failed in the browser (`TypeError: Failed to fetch`, or a bare
404/503) while every other framework asset loaded fine — and `curl` against
the exact same URLs from the same machine always returned 200. Binding the
dev server to `http://127.0.0.1:5420` explicitly (instead of `http://localhost:5420`,
which resolves to both stacks) made the failures disappear immediately and
consistently. Filed as an environment quirk of this sandbox's IPv6 loopback
under load, not a bug in the app, the build, or Avalonia — but worth knowing
if `dotnet run`'s browser dev server ever misbehaves only for the project's
own (not a dependency's) assets.

## M3 (file I/O) — Open Folder / Save As / drag-drop implemented; open questions on verification

M3 asked: open/save via file picker + drag-drop, and evaluate the File System
Access API for the folder panel. An audit before writing any code (see PR
history) found the desktop app's file dialogs and drag-drop *already* go
through Avalonia's own cross-platform `IStorageProvider`/`DragEventArgs`
abstractions, not OS-native APIs — Avalonia.Browser backs both with the File
System Access API. The actual gap was downstream: both paths reduce a picked/
dropped item to a `string` filesystem path (`IStorageFile.Path.LocalPath`) and
hand that to `AnimationChainListSave.FromFile(path)`/`.Save(path)`, and there
was no stream-based `Save` to pair with the read side's existing
`FromFile(path, streamProvider)` seam.

**Landed:**
- `AnimationChainListSave.Save(Stream)` — byte-identical output to
  `Save(string)` (test: `Save_Stream_ProducesByteIdenticalOutputToSaveToPath`),
  which now delegates to it.
- `BrowserProjectLoader.TryLoadAsync(IReadOnlyList<IStorageFile>, ...)` — the
  shared load path for both Open Folder and drag-drop. Per the user's choice
  of "drag-drop the set together" + "open a folder" (not "open just the achx
  and prompt for missing textures"): given a set of files, it finds the one
  `.achx`, parses it via the existing `FromString`, and matches every other
  `.png` in the set to frames by filename via `ThumbnailService.SeedTexture` —
  no assumption of a shared folder on a real filesystem anywhere.
- `AnimationEditor.Browser`'s view gained a toolbar (**Open Folder…**, **Save
  As…**) and a window-wide drop target. Open Folder uses
  `StorageProvider.OpenFolderPickerAsync` + `IStorageFolder.GetItemsAsync()` to
  collect the folder's files, then calls the same loader. Save As uses
  `StorageProvider.SaveFilePickerAsync` and writes via `IStorageFile.OpenWriteAsync()`
  + the new `Save(Stream)`. "Save" (vs "Save As") has no distinct behavior in
  this build — there's no stored file handle from the bundled sample to save
  back to, so both go through the same picker; a real editor would need to
  track the `IStorageFile` from whichever Open/Save As last succeeded.

**Verified:** the toolbar renders and the app still loads/plays the bundled
sample correctly with the new UI in place (confirmed in a real Chrome tab).

**Not verified, and likely can't be from this harness:** clicking Open
Folder/Save As opens a real native OS picker dialog, outside the page's DOM —
no tool available in this session can drive that (same class of boundary as
a native screenshot dialog). Drag-drop was tested by dispatching a synthetic
`DragEvent` (with real `File` objects built from a canvas-generated PNG and an
in-memory `.achx` string) at the canvas element — it never reached the
registered `DragDrop.DragOverEvent`/`DropEvent` C# handlers at all (no trace
of them running), meaning Avalonia's browser drag-drop plumbing requires a
genuine trusted OS-driven drag gesture and silently ignores synthetic ones —
consistent with browsers' security model around drag-and-drop file access
generally. **This code is verified by construction** (compiles; follows the
exact portable `IStorageProvider`/`DragEventArgs` pattern already proven
working in the desktop app's own Open/Save/drag-drop, per the pre-change
audit) and by the unit-tested `Save(Stream)`/`SeedTexture`/`BrowserProjectLoader`
pieces, but a real end-to-end human/OS-driven test (actually dragging a file,
actually clicking through a native folder picker) is the one remaining gap —
same category as M1's real-browser rendering check earlier in this doc.

**Update: live-watch landed after all, via polling.** The PNG folder-scan
panel's desktop implementation (`PngFolderScanner`/`PngFolderWatcher`) is pure
`Directory.EnumerateFiles` + `FileSystemWatcher`, neither of which exist in
the browser. Two alternatives were researched — `FileSystemObserver` (a real,
stable, purpose-built browser API, ultimately not usable here because
Avalonia keeps the native handle it would need `internal`) vs. polling
`Size`/`DateModified` via the already-public `GetBasicPropertiesAsync()` on
the same folder handle Open Folder already granted. Full research and the
decision are in **[BROWSER_FOLDER_WATCH_DECISION.md](BROWSER_FOLDER_WATCH_DECISION.md)**.

Polling is what's implemented: `FolderSnapshotDiff.FindChanged`
(`AnimationEditor.Core/IO/`, TDD'd, 6 tests) + `BrowserFolderWatcher`
(`AnimationEditor.Browser/`, `DispatcherTimer` every 2s). Detected changes
queue up rather than auto-applying, surfaced as a "Reload Changed Textures (N)"
button. Verified: the diff logic via unit tests, and the app still builds and
the bundled sample still loads with the new UI. Not verified, same reason as
Open Folder/Save As above: exercising a real folder pick + external file edit
needs the native OS folder picker, which no tool in this environment can drive.

### Bug found and fixed: `Pixel`-coordinate `.achx` files silently failed to load correctly

Every existing `.achx` example elsewhere in the repo (`samples/*/Content/**/*.achx`,
`.claude/templates/AnimationChains/*.achx`, test fixtures) uses `CoordinateType=Pixel` —
only the hand-authored bundled `sample/player.achx` uses `UV`. `ProjectManager.LoadAnimationChain`
always normalizes to UV via `ConvertCoordinates`, which read each texture's pixel size with
`System.IO.File.OpenRead` (`TryReadPngSize`). In the browser there is no filesystem, so that
read threw, was swallowed by a `try/catch`, and the frame's conversion was silently skipped —
raw pixel values were then misinterpreted as UV (0–1) fractions at render time, producing
garbled/wrong-region sprites with no error or crash.

**Fixed:** `LoadAnimationChain` (and `IProjectManager`) gained an optional
`knownTextureSizes` parameter; `BrowserProjectLoader.TryLoadAsync` already decodes every
dropped/picked PNG into an `SKBitmap` (for `ThumbnailService.SeedTexture`) and now also
records each one's pixel dimensions into that dictionary, so the Pixel→UV conversion never
needs to touch disk in the browser. TDD'd: `ProjectManagerLoadTests.LoadAnimationChain_PixelCoordinatesWithKnownTextureSizes_*`
construct a Pixel-coordinate chain whose texture is never written to disk and assert the
conversion still happens correctly.

### Known gap: no chain selector in this spike's UI

`App.axaml.cs` and `BrowserProjectLoader.cs` both hardcode
`selectedState.SelectedChain = acls.AnimationChains[0]` — whichever chain is first in the
`.achx`'s XML document order is the only one this build can ever show; there is no
ComboBox/tree/list to pick another. This was never in scope for M1–M4 (the ported UI is
intentionally just Open Folder / Save As / Preview), but it means a manual test file whose
first chain happens to be single-frame (e.g. `ShmupSpace.achx`'s `ShipTurnLeft`, a static
ship-rotation pose — see `manual-test-content/shmup-space/`) will look like "nothing is
animating" even though the file has real multi-frame chains (`Explosion`, `EnemyClam`,
`ShipBoosterStrong/Weak`) later in the same document. Revisit if/when more of the desktop
editor's UI gets ported to the browser build.

## M4 (deploy) — workflow + load indicator + bundle size measured; deploy not triggered

M4 asked for a GitHub Actions → Pages workflow with the correct base-href
subpath and a load indicator, plus measuring bundle size / first load. Scoped
deliberately to *writing* these (local, reversible commits) without actually
running a deploy to the repo's public Pages site — that's a separate,
explicit step for whoever's ready to make this live.

**Bundle size** (`dotnet publish -c Release`, measured locally, verified
reproducible):

| | Size |
|---|---|
| Total `wwwroot` | 34 MB |
| Raw `.wasm`/`.dll` in `_framework` | 18 MB |
| Brotli-compressed (what a browser actually fetches) | **5.7 MB** |
| File count in `_framework` | 159 |

The workflow (below) reports these same three numbers to the GitHub Actions
run summary on every deploy, so bundle-size drift is visible without digging
through logs.

**Load indicator.** Checked whether Avalonia.Browser has a built-in
splash-screen convention first — it doesn't, at least not in 12.0.1: grepped
the actual shipped `avalonia.js` for `avalonia-splash`/`splash-close` (the
class names the `dotnet new avalonia.xplat` template's CSS references) and
found zero matches, so that CSS is unused boilerplate in the scaffold, not a
real mechanism. Built one instead — a centered spinner + "Loading Animation
Editor…" message, placed inside `#out`. First attempt assumed Avalonia
*replaces* `#out`'s children when it attaches (mirroring what M1 assumed about
the native-host div); that was wrong here too — confirmed by checking the DOM
directly, Avalonia *appends* its canvas/native-host/input elements as
additional siblings, so the loading div (opaque, `position:absolute;inset:0`,
added first) ends up on top forever once appended-after content stacks over
it. Fixed with a small `MutationObserver` in `main.js` watching `#out` for the
canvas to appear, then removing the loading div — `runMain()` never resolves
for a long-lived SPA, so there's no "app is ready" promise to hang this off
instead. Verified in a real browser tab: the spinner shows immediately, and
`document.getElementById('loading')` returns `null` once the canvas exists,
confirming actual removal, not just visual covering.

**GitHub Actions workflow.** `.github/workflows/deploy-animation-editor-web.yml`,
`workflow_dispatch`-only (not on every push — this is still an early spike).
Mirrors the repo's existing `deploy-sample.yml` (which already deploys the
BlazorGL game samples to Pages: base-href rewrite via `sed`, `.nojekyll`,
stripping stray `.gitignore` files before `peaceiris/actions-gh-pages`'
internal `git add`, `keep_files: true` so other Pages content under different
`destination_dir`s survives each other's deploys) with one addition that
project's KNI/BlazorGL samples don't need: installing the `wasm-tools`
workload, since this project uses Microsoft's official
`Microsoft.NET.Sdk.WebAssembly` rather than KNI. Deploys to
`gh-pages/AnimationEditor`, base href `/FlatRedBall2/AnimationEditor/`.
Verified locally (not in CI): published in Release config, confirmed the
`sed` rewrite matches `<base href="./" />` (added to `index.html` for exactly
this) and produces the expected `/FlatRedBall2/AnimationEditor/`, and
reproduced the same bundle-size numbers above from that publish output.

**Not done, by design:** the workflow has not been run. `workflow_dispatch`
means it only executes when someone explicitly triggers it — deploying real
content to the repo's public GitHub Pages site is a deliberate, separate
action from writing the deploy mechanism.
