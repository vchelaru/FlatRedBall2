---
name: multiplatform-conversion
description: "Converting a single-target FlatRedBall2 desktop sample into a dual-target desktop + KNI BlazorGL (Blazor WebAssembly / browser) project. Use when the user mentions web deployment, browser/WASM/itch.io targets, KNI, or asks to add web support to an existing game. Assumes you already have a working desktop sample — see sample-project-setup for the desktop bootstrap."
---

# Multi-Platform Conversion (Desktop + KNI BlazorGL)

> Reference samples: `samples/auto/AutoEvalKniBlazorSample/` (minimal — one XNB, no real content) and `samples/PlatformKing/` (content-rich — TMX, JSON, PNG animations). Read PlatformKing first when porting any non-trivial game; AutoEval only proves the wiring, not the content story.

## Backend selection is by TFM

`src/FlatRedBall2.csproj` multi-targets `net8.0;net10.0` and conditions the backend:
- `net8.0` → KNI (`nkast.Xna.Framework.*`) — used by browser/Blazor
- `net10.0` → MonoGame (`MonoGame.Framework.DesktopGL`) — used by desktop

Consumers do not pick the backend. They pick a TFM. The engine's transitive references then drag in the matching XNA packages. This is the linchpin — every gotcha below comes from violating it.

## Project layout

Three projects, mirroring `AutoEvalKniBlazorSample`:

```
GameName/
  GameName.Common/     net8.0;net10.0   game code + Content/
  GameName.Desktop/    net10.0          Program.cs, MonoGame
  GameName.BlazorGL/   net8.0           Blazor WASM host
  GameName.slnx
```

The `Game` subclass (Game1) lives in `Common` so both heads instantiate the same type. Heads own only their entry points and platform-specific csproj wiring.

## Common csproj — must multi-target

When the heads sit on different TFMs (Desktop net10.0, BlazorGL net8.0), `Common` must multi-target both. A single-TFM Common forces the transitive engine reference to one backend, and the wrong-TFM head loads an assembly built against the other backend's XNA. Symptom: `MissingMethodException` on the first engine call.

```xml
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

XNA framework packages must be conditioned per TFM — the engine's transitive flow is not always reliable through `ProjectReference` at compile time:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <PackageReference Include="nkast.Xna.Framework.Game" Version="4.2.9001" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.*" />
</ItemGroup>
```

Common must NOT add `MonoGame.Content.Builder.Task` or `Apos.Shapes` — those belong on the heads that drive content compilation.

## Backend-conditional code

The engine sets `KNI` / `MONOGAME` defines in its own csproj, but `DefineConstants` does not flow through `ProjectReference`. To use `#if KNI` in Common, redefine per TFM in Common's csproj:

```xml
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <DefineConstants>$(DefineConstants);KNI</DefineConstants>
</PropertyGroup>
```

The two known places `#if KNI` is needed today:

- **`GraphicsProfile`** — Apos.Shapes ships SM 4.0+ shaders. MonoGame's top profile is `HiDef`. KNI's equivalent is `FL10_0`, which doesn't exist on MonoGame. The `Reach` default (SM 2.0) crashes at runtime with "Shader model 4.0 is not supported."
- **Anywhere a backend exposes a type the other doesn't.** Stay vigilant; most XNA surface is shared.

## Two canvas patterns — pick one before writing Game1

Stretch-to-viewport (canvas fills the browser) and fixed-size canvas (matches the desktop window) need opposite engine settings. Each pattern is a coordinated set across Game1, holder CSS, body CSS, and the JS host script — mixing them produces the squashing / shifting bugs the engine gates were added to prevent.

`DisplaySettings.AllowUserResizing` is the source-of-truth signal — it propagates to `Game.Window.AllowUserResizing` at init and gates two engine behaviors: the `externallyManaged` check that skips `ApplyWindowSettings`, and `HandleClientSizeChanged` short-circuiting browser resize echoes.

### Pattern A — Stretch-to-viewport

Canvas fills the browser; world content rescales as the viewport changes. Reference: `samples/PlatformKing`.

- **Game1**: `Window.AllowUserResizing = true;`. Don't set `PreferredWindowWidth/Height` on KNI (`PrepareWindow<T>` writes them onto `GraphicsDeviceManager.PreferredBackBufferWidth/Height` in the constructor — clamps the buffer before the externally-managed branch runs). Guard with `#if !KNI` if your desktop build wants a fixed window size. Leave per-screen `PreferredDisplaySettings` unset.
- **Holder CSS** (`Pages/Index.razor`): `position: fixed; top: 0; left: 0; right: 0; bottom: 0` — fills viewport.
- **Body CSS** (`wwwroot/index.html`): default — no flex centering needed.
- **JS** (`wwwroot/index.html`): default `initRenderJS` from AutoEvalKniBlazorSample (sets canvas buffer once from holder size).

### Pattern B — Fixed-size canvas

Canvas locked at the desktop window's dimensions; browser resize doesn't reshape gameplay. Reference: `samples/ShmupSpace`.

- **Game1**: set `PreferredWindowWidth/Height` on **both** backends, plus `ds.AllowUserResizing = false`. Don't set `Window.AllowUserResizing = true`. Per-screen `PreferredDisplaySettings` left unset (same as Pattern A).
- **Holder CSS**: `width: NNNpx; height: MMMpx; flex-shrink: 0;` — explicit dims, won't shrink in flex centering.
- **Canvas CSS**: `width: NNNpx; height: MMMpx; display: block;` — explicit dims, not `100%` (defense in depth if holder is overridden).
- **Body CSS**: `display: flex; align-items: center; justify-content: center; min-height: 100vh; overflow: auto; background: #222;` — centers the canvas; scrolls when viewport is smaller than the canvas.
- **JS**: replace `tickJS` with a version that re-pins `canvas.width` / `canvas.height` every frame. KNI BlazorGL auto-resizes the drawing buffer when the browser resizes; the per-frame lock undoes that:

```js
var lockW = 0, lockH = 0;
function tickJS() {
    var c = document.getElementById('theCanvas');
    if (c) { if (c.width !== lockW) c.width = lockW; if (c.height !== lockH) c.height = lockH; }
    window.theInstance.invokeMethod('TickDotNet');
    window.requestAnimationFrame(tickJS);
}
// In initRenderJS, set lockW/lockH from holder.clientWidth/Height before scheduling tickJS.
```

All four parts must be present together — omit any one and the buffer or viewport drifts on browser resize.

## Game code must avoid `System.IO.File` for content

Browsers have no filesystem. Any `File.ReadAllText` / `File.OpenRead` / `File.Exists` / `Path.GetFullPath` call against a content path crashes on WASM with `Could not find a part of the path '/Content/...'` (the leading `/` is `Path.GetFullPath` resolving against the WASM working directory `/`).

Route every content read through `Microsoft.Xna.Framework.TitleContainer.OpenStream(path)` instead — it dispatches to File IO on Desktop and HTTP fetch in the browser, single code path:

```csharp
using var stream = TitleContainer.OpenStream(path);
using var reader = new StreamReader(stream);
var json = reader.ReadToEnd();
```

This applies to game-specific config loaders (the engine's own `PlatformerConfig.FromJson`, `TopDownConfig.FromJson`, `TileMap`, animation/atlas loaders, and `ContentManagerService` already do the right thing internally). Save data and user settings legitimately need `File` and stay desktop-only — gate them with `#if !KNI`.

## Content pipeline — single source of truth in Common

Keep `Content/` in `Common`. Both heads consume it without duplication.

**Desktop** uses MGCB. Link Common's raw runtime-loaded assets (TMX, JSON, animation PNGs — anything not built to XNB) into the Desktop output's `Content/` folder:

```xml
<Content Include="..\GameName.Common\Content\Tiled\**"
         Link="Content\Tiled\%(RecursiveDir)%(Filename)%(Extension)"
         CopyToOutputDirectory="PreserveNewest" />
```

`MonoGameContentReference` is project-local; Desktop needs its own minimal `Content/Content.mgcb` (see `sample-project-setup`). Apos.Shapes' shader is built via `buildTransitive` on the head, not from Common.

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

## BlazorGL csproj boilerplate

Copy verbatim from `AutoEvalKniBlazorSample.BlazorGL.csproj` and namespace-rename:
- SDK = `Microsoft.NET.Sdk.BlazorWebAssembly`, `<KniPlatform>BlazorGL</KniPlatform>`, the full nkast.Xna and nkast.Kni.Platform.Blazor.GL package list, the `RedirectKniContentToWwwroot` target.
- Owns `Program.cs`, `App.razor`, `_Imports.razor`, `MainLayout.razor`, `Pages/Index.razor` + `.razor.cs`, `wwwroot/index.html`, `Properties/launchSettings.json`. Same files, namespace-renamed, with the Game1 type swapped to your Common's Game1.
- **Pick a unique launch port** in `launchSettings.json`. AutoEvalKniBlazorSample uses 50470/50471; pick something else. Concurrent debugging across samples breaks if ports collide.

## Verification

1. `dotnet build GameName.Desktop/` clean.
2. `dotnet build GameName.BlazorGL/` clean (one upstream Apos.Shapes shader warning is expected; not your code).
3. `dotnet run --project GameName.Desktop/` plays the original game unchanged.
4. `dotnet run --project GameName.BlazorGL/` serves the dev URL; canvas fills viewport; resizing the window keeps rendering correct (proves `AllowUserResizing` is set).

## Known limitations (as of 2026-04-25)

- **Tiled external tileset/image resolution on WASM is broken.** `TileMap` itself loads via `TitleContainer.OpenStream`, but `MonoGame.Extended`'s parser still uses `File.IO` for external TSX and image references inside the TMX. Symptom: `External tileset 'Foo.tsx' could not be found`. Tracked in `design/TODOS.md` until upstream exposes a resource-resolution callback or we route around it. A game with no Tiled content (e.g. `samples/ShmupSpace/`) is unaffected.
- **No gamepad polling guarantee on web.** Browser gamepad APIs require a connected-device gesture before reporting state.
- **Audio gated by user gesture.** Browsers block audio playback until the user interacts with the page once. Have a "click to start" affordance if music plays on screen entry.
