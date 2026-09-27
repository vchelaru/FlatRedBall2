---
name: desktop-distribution
description: Desktop game distribution — self-contained per-RID publish, macOS .app bundles, payload pruning. Triggers: Info.plist, Contents/MacOS, PublishProfile, SelfContained, RuntimeIdentifier, osx-x64.
---

# Desktop Distribution

Shipping a desktop FRB2 game means cutting one package per platform you list it on. The engine
generates no publish profiles and ships no packaging targets, so this is entirely the game
project's job — `templates/frb2-desktop/` stops at a runnable `dotnet run`. This skill covers the
three things that silently produce an unshippable package: a framework-dependent publish, natives
for every RID, and a macOS payload that is not a real `.app`. For the web head see
`multiplatform-conversion`; for the `.gumpkg` deployment toggle see `gum-packaging`.

## One publish profile per RID

`Properties/PublishProfiles/` is the only folder the SDK auto-discovers, and the profile is the unit
of distribution: name it with `-p:PublishProfile=<name>` and set `RuntimeIdentifier`,
`SelfContained=true`, `Configuration=Release`, and `PublishDir` in the profile itself.

**Landmine — framework-dependent output builds clean.** Without `SelfContained=true` the publish
succeeds and the `runtimeconfig.json` demands `Microsoft.NETCore.App`, a runtime no player has
installed. Nothing fails until someone launches the build. Assert on the artifact, not the build
log: `runtimeconfig.json` contains no `framework` key and `System.Private.CoreLib.dll` is present.

## Native libraries follow the RID, not the target

SDL2, OpenAL and FreeType arrive as native assets for every RID in the graph. A publish that does
not pin `RuntimeIdentifier` carries all of them; pin it and only the target's copy ships. Each RID
also needs its own publish — there is no single artifact that runs everywhere.

## macOS needs a real .app bundle

The published payload has to be wrapped, and the layout is not negotiable: files go in
`<App>.app/Contents/MacOS/`, **not** `Contents/Resources/`, because `Contents/MacOS` is what
`AppContext.BaseDirectory` resolves to inside a bundled .NET app and the game locates its
`Content/` relative to that. A `Contents/Resources` layout boots and then finds no content.

`Contents/Info.plist` names the apphost in `CFBundleExecutable`.

**Landmine — the apphost's file extension follows the BUILD host, not the target RID.** Publishing
`osx-x64` from Windows can produce `<TargetName>.exe` (still a Mach-O binary) or the extension-less
name, depending on SDK version. Resolve which file exists on disk; never assume.

## Landmines: the MSBuild that wraps it

Redirect `PublishDir` to the payload *before* the copy runs
(`BeforeTargets="PrepareForPublish"`) and write the plist after (`AfterTargets="Publish"`), so the
hundreds of published files land in their final place in one step instead of being moved.

- **A target runs at most once per build.** One target cannot carry both hooks — it does the first
  pass and the plist never appears. Two targets are required.
- **`AfterTargets` must name exactly one target that exists.** There is no
  `CopyFilesToPublishDirectory` in the current SDK (`Publish` depends on the copy targets directly),
  and a multi-entry `AfterTargets="Publish _CopyResolvedFilesToPublishAlways"` makes MSBuild skip the
  hook with no warning. Both failures produce a complete payload with no `Info.plist`, which macOS
  refuses to launch.
- **Don't normalize the payload path with `$([System.IO.Path]::GetFullPath(...))` inside the
  target's `PropertyGroup`** — it returns an empty string there, and the plist gets written relative
  to the build's working directory instead of into the bundle. Carry `PublishDir` forward with
  `$([MSBuild]::EnsureTrailingSlash(...))` and a relative step up to `Contents/`.

## Prune: what is safe to drop

Prune at the publish-item list (`AfterTargets="ComputeResolvedFilesToPublishList"`,
`ResolvedFileToPublish Remove=...`), never the compile item list, and keep it behind a property
only the distribution profiles set — a local `dotnet publish` and `dotnet run` should keep
everything.

| Extension | Why it is safe |
|---|---|
| `.pdb` | debug symbols |
| `.mgstats` | MonoGame build stats |
| `.gumfcs` | Gum scene caches |
| `.setj` | Gum/KernSmith per-user settings |
| `.gumpkg` | only when `GUM_BUNDLE` is off — see `gum-packaging` for the toggle |

Pruning by extension is silent content loss, so pin the list in a test that fails if a
runtime-loaded extension (`.wav`, `.fnt`, `.png`, `.achx`, `.tmx`, `.json`, `.dll`, …) lands in it.

## Fail fast on the baked font atlases

Desktop Gum rendering reads pre-baked `Content/GumProject/FontCache/*.fnt` atlases, regenerated with
`gumcli fonts <GumProject.gumx>` (see `gumcli`). They are build output, not source. A package built
without them boots normally and renders **no text anywhere** — nothing crashes, so this is worth an
`<Error>` in the publish rather than a bug report from a player.

## Zip permission bits

The apphost needs mode `0755`, other files `0644`, directories `0755`, or macOS refuses to launch the
bundle. `Compress-Archive` stores no Unix mode; from Windows, write the entries with
`System.IO.Compression.ZipArchive` and set the mode in `ExternalAttributes` (the high 16 bits).

## Not covered

Notarization, signing and Gatekeeper (do those on a Mac); Steam/upload CLIs; Android and iOS.

## Precedent in this repo

`samples/Solitaire/Solitaire.GumPackage.targets` is the pattern to copy for game-owned build
packaging: a targets file in the game repo, imported by that game's csprojs, inert unless a property
turns it on. `src/FlatRedBall2.BlazorGL/build/FlatRedBall2.BlazorGL.targets` shows the other shape —
engine-provided targets packed into the NuGet (`PackagePath="build\"`) and auto-imported by
consumers.
