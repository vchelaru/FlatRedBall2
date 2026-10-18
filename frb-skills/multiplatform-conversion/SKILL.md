---
name: multiplatform-conversion
description: "Adding a KNI BlazorGL (Blazor WebAssembly / browser) head to a FlatRedBall2 desktop project. Use when the user mentions web deployment, browser/WASM/itch.io targets, KNI, or asks to add web support to an existing game, or hits a browser-only failure (JSImport, user activation, AOT publish). Assumes you already have a working desktop sample — see sample-project-setup for the desktop bootstrap."
---

# Multi-Platform (Desktop + KNI BlazorGL)

> References: `templates/frb2-multiplatform/` (minimal — proves the wiring, not the content story) and `samples/PlatformKing/` (content-rich — TMX, JSON, PNG animations). Read PlatformKing first when porting any non-trivial game.

## One engine, one Common

Both heads run on KNI and reference the same engine (`src/Kni/FlatRedBall2.Kni.csproj` / NuGet `FlatRedBall2.Kni`). There is no backend switch: Game1 and all game code are shared verbatim, and branch at runtime on `OperatingSystem.IsBrowser()` only where a browser genuinely differs.

## Project layout

Projects, mirroring `templates/frb2-multiplatform/`:

```
GameName/
  GameName.Common/            net10.0   game code + Content/, one csproj, default globbing
  GameName.Desktop/           net10.0   Program.cs, KNI SDL2.GL
  GameName.BlazorGL/          net10.0   Blazor WASM host
  GameName.slnx
```

The `Game` subclass (Game1) lives in `Common` so both heads instantiate the same type. Heads own only their entry points and platform-specific csproj wiring.

## Asset placement

Assets belong in `Common/Content/` by default. Only move to platform-specific folders if they won't work elsewhere (e.g., platform-specific UI sizes, backend-incompatible shader variants). Most art, audio, and data stay in `Common/` so both Desktop and BlazorGL draw from a single source — no duplication.

## Common

One `GameName.Common.csproj`: `net10.0`, default compile items, the `nkast.Xna.Framework*` packages the game code uses, and a `ProjectReference` (or `FlatRedBall2.Kni` package) to the engine. Both heads reference it. See `samples/PlatformKing/PlatformKing.Common/` (minimal) and `samples/Solitaire/Solitaire.Common/` (Gum content items).

Desktop head: `<KniPlatform>DesktopGL</KniPlatform>` plus `nkast.Xna.Framework*` and `nkast.Kni.Platform.SDL2.GL`. BlazorGL head: `<KniPlatform>BlazorGL</KniPlatform>` plus `nkast.Kni.Platform.Blazor.GL` (below).

**Landmine: list every `ProjectReference` target as its own `<Project Path>` in the `.slnx`.** Visual Studio's IDE-hosted NuGet restore only walks `ProjectReference`s among projects that are themselves listed as solution members — a project that exists on disk and is correctly referenced but missing from the `.slnx` builds fine via `dotnet build`/CLI `msbuild` (both walk the full reference closure regardless of solution membership) but fails with NU1105 ("not part of the current solution") the moment someone opens the `.slnx` in Visual Studio. So list the engine projects (`FlatRedBall2.Kni`, `AnimationChain.Common`, and `FlatRedBall2.BlazorGL` for a BlazorGL head) alongside the game's own. `tests/FlatRedBall2.Tests/Packaging/SolutionMembershipTests.cs` checks this for every `.slnx` in this repo; there's no equivalent check for a downstream game project, so verify it by hand when scaffolding one.

No `Version` attribute — the repo uses NuGet Central Package Management, so versions are pinned once in `Directory.Packages.props`, not per `PackageReference`.

`Apos.Shapes` needs no content-pipeline wiring at all — its shader is embedded in the assembly.

## GraphicsProfile

Apos.Shapes ships SM 4.0+ shaders, so Game1 must set `GraphicsProfile.FL10_0`. The `Reach` default (SM 2.0) crashes at runtime with "Shader model 4.0 is not supported."

## Two canvas patterns — pick one before writing Game1

Stretch-to-viewport (canvas fills the browser) and fixed-size canvas (matches the desktop window) need opposite engine settings. Each pattern is a coordinated set across Game1, holder CSS, body CSS, and the JS host script — mixing them produces the squashing / shifting bugs the engine gates were added to prevent.

> **Pattern A is the recommended default.** With `DisplaySettings.AspectPolicy = AspectPolicy.Locked` (the engine default), the canvas can fill the browser viewport and the engine pillarboxes/letterboxes the gameplay area to the design ratio internally — no playfield reshaping. Pattern B is only needed for legacy fixed-canvas embeds.

`DisplaySettings.AllowUserResizing` is the source-of-truth signal — it propagates to `Game.Window.AllowUserResizing` at init and gates two engine behaviors: the `externallyManaged` check that skips `ApplyWindowSettings`, and `HandleClientSizeChanged` short-circuiting browser resize echoes.

### Pattern A — Stretch-to-viewport (recommended)

Canvas fills the browser; the engine pillarbox/letterboxes the design world to its locked aspect ratio inside the canvas. Reference: `samples/ShmupSpace`, `samples/PlatformKing`.

- **Game1**: identical code on Desktop and BlazorGL — set `ResolutionWidth/Height`, `PreferredWindowWidth/Height`, `AllowUserResizing = true`, all on `DisplaySettings`. The engine ignores `PreferredWindowWidth/Height` in the browser (the canvas DOM owns sizing).
- **Holder + canvas markup**: ships from the `FlatRedBall2.BlazorGL` package's `Pages/Index.razor` — fills viewport via `position: fixed; top: 0; left: 0; right: 0; bottom: 0`. No per-sample Razor needed.
- **Body CSS** (`wwwroot/index.html`): `margin: 0; overflow: hidden;` — no flex centering needed.
- **JS**: ships from the package as `_content/FlatRedBall2.BlazorGL/frb-host.js` (referenced via one `<script>` tag in `index.html`). Defines `initRenderJS` and `tickJS`; sets canvas buffer once from holder size.

### Pattern B — Fixed-size canvas (legacy)

Canvas locked at exact pixel dimensions; nothing scales. Use only for embeds with a strict pixel budget; otherwise prefer Pattern A + locked aspect.

- **Game1**: set `PreferredWindowWidth/Height` on **both** backends, plus `ds.AllowUserResizing = false`. Don't set `Window.AllowUserResizing = true`. Per-screen `PreferredDisplaySettings` left unset (same as Pattern A).
- **Index.razor override**: ship a per-sample `Pages/Index.razor` that overrides the package's route (`@page "/"`) with explicit-dimension CSS:
  - **Holder CSS**: `width: NNNpx; height: MMMpx; flex-shrink: 0;` — explicit dims, won't shrink in flex centering.
  - **Canvas CSS**: `width: NNNpx; height: MMMpx; display: block;` — explicit dims, not `100%` (defense in depth if holder is overridden).
- **Body CSS**: `display: flex; align-items: center; justify-content: center; min-height: 100vh; overflow: auto; background: #222;` — centers the canvas; scrolls when viewport is smaller than the canvas.
- **JS hooks**: still load `frb-host.js` from the package; add an inline override script that uses the `frbBeforeTick` hook to re-pin canvas dimensions each frame. KNI BlazorGL auto-resizes the drawing buffer when the browser resizes; the per-frame lock undoes that:

```html
<script src="_content/FlatRedBall2.BlazorGL/frb-host.js"></script>
<script>
    var lockW = 0, lockH = 0;
    window.frbAfterInit = function (canvas, holder) {
        lockW = holder.clientWidth;
        lockH = holder.clientHeight;
    };
    window.frbBeforeTick = function () {
        var c = document.getElementById('theCanvas');
        if (c) { if (c.width !== lockW) c.width = lockW; if (c.height !== lockH) c.height = lockH; }
    };
</script>
```

All parts must be present together — omit any one and the buffer or viewport drifts on browser resize.

## Game code must avoid `System.IO.File` for content

Browsers have no filesystem. Any `File.ReadAllText` / `File.OpenRead` / `File.Exists` / `Path.GetFullPath` call against a content path crashes on WASM with `Could not find a part of the path '/Content/...'` (the leading `/` is `Path.GetFullPath` resolving against the WASM working directory `/`).

Route every content read through `Microsoft.Xna.Framework.TitleContainer.OpenStream(path)` instead — it dispatches to File IO on Desktop and HTTP fetch in the browser, single code path:

```csharp
using var stream = TitleContainer.OpenStream(path);
using var reader = new StreamReader(stream);
var json = reader.ReadToEnd();
```

This applies to game-specific config loaders (the engine's own `PlatformerConfig.FromJson`, `TopDownConfig.FromJson`, `TileMap`, animation/atlas loaders, and `ContentLoader` already do the right thing internally). Save data and user settings go through `Engine.Storage`, not `File`: same code on both platforms. See the `persistence` skill.

## Content pipeline — single source of truth in Common

Keep `Content/` in `Common`. Both heads consume it without duplication.

**Desktop**: link Common's raw runtime-loaded assets (TMX, JSON, animation PNGs — anything not built to XNB) into the Desktop output's `Content/` folder:

```xml
<Content Include="..\GameName.Common\Content\Tiled\**"
         Link="Content\Tiled\%(RecursiveDir)%(Filename)%(Extension)"
         CopyToOutputDirectory="PreserveNewest" />
```

Only a head that builds XNBs needs a `.mgcb`; see `content-and-assets`.

**BlazorGL** is the trap. `<Content Link="wwwroot\…">` copies to `bin/.../wwwroot/` but the file is **not** registered as a static web asset, so the dev server returns 404. Files must land in the project's **physical** `wwwroot/Content/` directory before the static-web-asset manifest is gathered. Use a `<Copy>` target:

```xml
<Target Name="CopyCommonRawAssetsToWwwroot"
        BeforeTargets="GenerateStaticWebAssetsManifest;AssignTargetPaths"
        Inputs="@(_CommonRawAssets)"
        Outputs="@(_CommonRawAssets -> '$(MSBuildProjectDirectory)\wwwroot\Content\%(RecursiveDir)%(Filename)%(Extension)')">
  <Copy SourceFiles="@(_CommonRawAssets)"
        DestinationFiles="@(_CommonRawAssets -> '$(MSBuildProjectDirectory)\wwwroot\Content\%(RecursiveDir)%(Filename)%(Extension)')"
        SkipUnchangedFiles="true" />
</Target>
```

Same pattern as the `RedirectKniContentToWwwroot` target, which writes XNBs to the physical wwwroot. Gitignore the destination tree (`wwwroot/Content/.gitignore` excluding `*` except itself) so the copies aren't committed.

## BlazorGL head — minimum setup

Reference: `templates/frb2-multiplatform/MyGame.BlazorGL`. Each sample's `.BlazorGL` head owns only:

- **`.csproj`** — SDK = `Microsoft.NET.Sdk.BlazorWebAssembly`, `<KniPlatform>BlazorGL</KniPlatform>`, the nkast.Xna / nkast.Kni.Platform.Blazor.GL package list, the `RedirectKniContentToWwwroot` target. **`<ProjectReference>` to `src/FlatRedBall2.BlazorGL/FlatRedBall2.BlazorGL.csproj`** (the host package, not the engine itself).
- **`Program.cs`** — standard Blazor WASM bootstrap. Two FRB-specific lines:
  ```csharp
  builder.RootComponents.Add<FlatRedBall2.BlazorGL.App>("#app");
  builder.Services.AddSingleton<Func<Game>>(_ => () => new MyNamespace.Game1());
  ```
- **`wwwroot/index.html`** — the standard Blazor scaffold + script tags:
  ```html
  <script src="_framework/blazor.webassembly.js" autostart="false"></script>
  <script src="_content/FlatRedBall2.BlazorGL/frb-host.js"></script>
  ```
  `autostart="false"` is required: `frb-host.js` calls `Blazor.start` itself to cap parallel `_framework/` downloads at 50. Blazor's default is unthrottled, and a dev server with 100 HTTP/2 streams then queues the rest, which a fresh browser can fail with `ERR_CERT_VERIFIER_CHANGED` (the boot screen shows "Failed to start"). A page that keeps autostart still works, uncapped.
  The package's `frb-host.js` defines `tickJS`, `initRenderJS`, optional hooks
  (`window.frbBeforeTick`, `window.frbAfterInit`), content prefetching via
  `content-manifest.json`, and keyboard/mouse scroll prevention for itch.io iframes.
  It also defines `frbDiagPlatform` and `frbDiagPublish` for the `?diag=perf` opt-in (see the
  `performance` skill); a vendored older copy without them still works but reports platform `unknown`.
  The template's `Index.razor.cs` does not call the probe yet.
- **Optional loading screen** — `<script src="_content/FlatRedBall2.BlazorGL/boot-screen.js"></script>` before `blazor.webassembly.js` shows the logo, an animated bar and the `_framework/` download size, and removes itself once frames run. It injects its own DOM, chains `frbBeforeTick` in either load order (Pattern B hooks keep working), and shows a message plus a Reload button if boot fails (a first https load can fail with `ERR_CERT_VERIFIER_CHANGED`; see issue #1467). A head that references only `FlatRedBall2.Kni` (the template) vendors it as `wwwroot/boot-screen.js`, since `_content/` is BlazorGL's. Landmine: engine init runs synchronously inside the first real `TickDotNet`, so nothing but the compositor-driven bar can animate during it; there is deliberately no status text, and `frb-host.js` skips the first tick (via `window.frbSkipTick`) so the screen paints once before init blocks the thread.
- **`Properties/launchSettings.json`** — pick a unique launch port (check the other samples' `launchSettings.json`). Concurrent debugging across samples breaks if ports collide.

**Do not duplicate** `App.razor`, `MainLayout.razor`, `_Imports.razor`, `Pages/Index.razor`, or the `tickJS`/`initRenderJS` JS block. They ship from `FlatRedBall2.BlazorGL` and are wired by the `RootComponents.Add<App>` and `frb-host.js` reference above. The package's Index resolves `Func<Game>` from DI on the first tick — that's why `Program.cs` must register it.

## Content prefetching (automatic)

The `FlatRedBall2.BlazorGL` NuGet package includes a `.targets` file that auto-imports
into consuming projects. On every build it enumerates all files under `wwwroot/Content/`
and writes `wwwroot/content-manifest.json` — a JSON array of paths relative to `wwwroot`,
each prefixed `Content/`.

At runtime, `frb-host.js` fetches `content-manifest.json` and fires background `fetch()`
for every listed file during `initRenderJS`. Both `fetch()` and the synchronous
`XMLHttpRequest` used by `TitleContainer.OpenStream` share the browser HTTP cache, so
by the time game content loads, the files are served from local cache instead of the
network. Errors are silently swallowed (fire-and-forget).

**No game author action required.** The manifest is generated automatically from whatever
files exist in `wwwroot/Content/` at build time. Add or remove content files and the
manifest stays in sync on the next build.

**Landmine:** `build/*.targets` auto-import only happens for NuGet `PackageReference`
consumers — a `ProjectReference` to the host project does **not** import them. That's why
repo samples carry an explicit
`<Import Project="...\FlatRedBall2.BlazorGL\build\FlatRedBall2.BlazorGL.targets" />`;
without it, no manifest is generated and KNI's `js/streamProcessor2.js` (the `DynamicSoundEffectInstance` AudioWorklet) is never served. NuGet consumers need nothing.

## WASM runtime landmines

These compile clean, pass every desktop and xunit run, and fail only in the browser.

- **`System.Text.Json` reflection serialization is off on WebAssembly**, in Debug and Release alike, and throws `JsonSerializerIsReflectionDisabled`. Serialize through a source-generated `JsonSerializerContext` (`[JsonSerializable(typeof(T))]`), the same way the engine's own `*JsonContext` classes do.
- **`TickDotNet` runs synchronously inside a `requestAnimationFrame` callback** (`src/FlatRedBall2.BlazorGL/wwwroot/frb-host.js`), and the browser runtime is single-threaded. Never block on an `IJSRuntime` task (`.GetAwaiter().GetResult()`, `.Wait()`) from game code: the JS side can run while the managed wait still fails, so a write lands in storage yet reports failure. Call JS through synchronous `[JSImport]` bindings instead.
- **`index.html` is never content-hashed, and neither is a plain `wwwroot/*.js` file.** The template's `index.html` references `_framework/blazor.webassembly.js` without the `#[.{fingerprint}]` placeholder, so that file and `dotnet.js` stay unhashed too; the assemblies and runtime are hashed. A cached copy pairs an old script with new code, and the fix appears not to work because the fix is in the stale file. Keep shims and diagnostics in C#, or fingerprint the JS file with a `<StaticWebAssetFingerprintPattern>` item, `<OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>`, and a `name#[.{fingerprint}].js` reference. Otherwise hard-reload before trusting a JS error that contradicts the page.
- **A console 404 attributed to `XHR.<version>.js` is not that script.** The shim loads fine; the browser names the script that issued the failing request, and KNI's `TitleContainer` makes every content read through it. The usual culprit is Gum's optional `Content/Shader.xnb` probe (`Renderer.Initialize`, which logs a "benign" notice), so check the Network tab for the real URL. Keep the XHR shim in `frb-host.js`: removing it breaks all content loading.
- **`dotnet publish` has no `blazor.boot.json` on .NET 10.** The boot resource list is inlined into `_framework/dotnet.js`, so a missing `blazor.boot.json` says nothing about whether AOT ran.

### `[JSImport]` interop

A head that adds `[JSImport]` needs `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (SYSLIB1074); the BlazorGL heads in this repo do not set it.

- **Default to a module bridge:** a thin ES module in `wwwroot` loaded once with `JSHost.ImportAsync`, then `[JSImport("getItem", ModuleName)]`. The generator checks signatures only, never paths.
- **`JSHost.ImportAsync` resolves relative to `_framework/`**, not the `wwwroot` root. Use `"../localStorage.js"`; `"./localStorage.js"` fails at runtime with `Failed to fetch dynamically imported module: .../_framework/localStorage.js`.
- **Module-less names start from an empty object, not `window`.** Only a literal `globalThis.` prefix re-roots onto the real global, so `[JSImport("globalThis.localStorage.setItem")]` works and `[JSImport("localStorage.setItem")]` throws. A bare name like `[JSImport("myFn")]` throws `must be a Function but was undefined` even when the console shows `typeof myFn === 'function'`; trust the console, the error is the resolver. The function is bound to its parent object (`localStorage` above). Use this form only when you want no shim file.
- **`IJSRuntime.InvokeAsync` resolves the same dotted string from `window`**, so a name that works there can fail under `[JSImport]`.
- **The argument order is `[JSImport(functionName, moduleName)]`.** Swapped, it throws `ES6 module ... was not imported yet`.
- **`[JSImport]` can call functions but not read properties.** `localStorage.length` is a property, so `LocalStorageApi` (`src/FlatRedBall2.BlazorGL`) reads it with `JSHost.GlobalThis.GetPropertyAsJSObject("localStorage")`.
- **Strings cross cheaply; byte arrays don't.** A `Task<byte[]>` return fails with SYSLIB1072. Pass bytes as base64 and decode with `Convert.FromBase64String`.

## Browser input and user activation

- **Audio, fullscreen, file pickers, and permission prompts need a real user gesture.** Autoplay at startup is blocked, and a click your own JS dispatches is untrusted, so it does not count. Start audio or open a picker from a genuine `pointerdown`/`keydown`/`touchend`, and have a "click to start" affordance if music plays on screen entry.
- **Input the game reads during a frame is not a DOM user activation.** KNI polls input inside `TickDotNet`, after the DOM event has returned, so a Gum button that opens a file picker or requests fullscreen can run its handler and still be refused, often silently. If that happens, wire the action to a DOM event listener in JS.
- **The browser keeps some shortcuts.** `frb-host.js` only suppresses arrows, space, and wheel scrolling. Ctrl+S, F5, and similar keys reach the game but also trigger the browser action unless the head calls `preventDefault` on that `keydown`. Ctrl+W, Ctrl+T, and Ctrl+N are reserved by the browser and never reach the page, so don't bind game actions to them.
- **"The click didn't register" is usually a stalled frame, not lost input.** The first frames after a screen transition can stall for hundreds of milliseconds with nothing drawn. Attribute it before touching input code: a capture-phase JS `pointerdown` timestamp plus a managed `Console.WriteLine` (which shows up in the browser console) tells you whether the press reached the game in the same frame.
- **Frame timings are not comparable across browsers.** Firefox's clock is coarser and its WebAssembly is often slower on the same frames. See the `performance` skill for measurement discipline.

## Publishing: AOT vs the interpreter

Without AOT, a published head runs .NET IL through an interpreter in WebAssembly (often called "JIT"). AOT compiles to native WebAssembly: much faster per frame, but a far larger download, a multi-minute publish, and it needs the `wasm-tools` workload. Neither is a default to copy blindly: AOT suits a build shipped to players with CPU-heavy frames, and the interpreter suits day-to-day iteration. Nothing in this repo publishes AOT. Comparing frame timings between the two belongs in the `performance` skill.

```xml
<PropertyGroup>
  <RunAOTCompilation>true</RunAOTCompilation>
  <!-- Must stay false: stripping IL removes code the interpreter still needs for
       engine reflection paths (screen creation via Activator.CreateInstance, Gum runtime
       registration), which reportedly fails as a per-frame NullReferenceException flood. -->
  <WasmStripILAfterAOT>false</WasmStripILAfterAOT>
</PropertyGroup>
```

- **AOT runs at `dotnet publish -c Release` only.** `dotnet run` and the dev server always use the interpreter, so a dev-server number is never an AOT baseline. Pass `-p:RunAOTCompilation=false` on the publish command to skip AOT without editing the csproj.
- **Delete the publish directory before publishing or measuring payload size.** A publish into an existing folder leaves the previous build's content-hashed files behind; the loader ignores them, but they inflate any size total and a stale `wwwroot` can mask edits.

## Verification

1. `dotnet build GameName.Desktop/` clean.
2. `dotnet build GameName.BlazorGL/` clean.
3. `dotnet run --project GameName.Desktop/` plays the original game unchanged.
4. `dotnet run --project GameName.BlazorGL/` serves the dev URL; canvas fills viewport; resizing the window keeps rendering correct (proves `AllowUserResizing` is set).

## Known limitations (as of 2026-08-20)

- **No gamepad polling guarantee on web.** Browser gamepad APIs require a connected-device gesture before reporting state.
- **`DynamicSoundEffectInstance` sample rate must match the browser's `AudioContext` rate on Blazor.GL, or `SubmitBuffer` throws** (`Sample rate 44100 does not match AudioContext sample rate 48000`). Desktop OpenAL resamples any source rate for free; Blazor.GL does not, and Kni exposes no public way to read the actual `AudioContext` rate (feature-requested: kniEngine/kni#2690). Until that lands, fall back to a couple of common candidate rates (48000, then 44100) and retry on failure.
- **`Pitch` on `SoundEffectInstance`/`DynamicSoundEffectInstance`** no longer throws as of `nkast.Kni.Platform.Blazor.GL` 4.3.9001, but nobody has yet confirmed it is audible in a browser. `diagnostics/MusicPitchWebSpike` is the check.
