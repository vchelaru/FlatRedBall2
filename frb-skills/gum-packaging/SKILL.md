---
name: gum-packaging
description: "Bundle a Gum project into a single .gumpkg file (tar+brotli) for distribution. Trigger when shipping a built game, optimizing initial load on web/BlazorGL, or when the user mentions 'gum pack', '.gumpkg', or wants fewer loose Content files. Covers gumcli pack, runtime loading, and the loose-vs-bundle toggle for diagnostics."
---

# Gum Packaging (.gumpkg)

`gumcli pack` walks a `.gumx` project's dependencies and writes a single tar+brotli bundle (`.gumpkg`) containing the elements, font cache, and external textures. At runtime, Gum (2026.6+) picks loose vs. bundle **from the extension of the path you pass** — `.gumx` loads loose, `.gumpkg` loads the bundle. There is **no sibling probing**: the extension is the single source of truth (a probe would be a guaranteed-404 HTTP request on Blazor/WASM). So the path your code passes must match the artifact your build deployed.

## When to use

- **Shipping a build** — fewer files to copy, faster startup on web targets, no static-asset manifest bloat.
- **BlazorGL/WASM specifically** — many small `.gusx`/`.gucx`/`.png` files are slow to fetch over HTTP; one `.gumpkg` is one request.
- **Don't pack during authoring** — the Gum editor saves loose files. Keep loose during development; pack for distribution.

## Pack command

Run from the project directory that contains the `.gumx`:

```bash
"$GUMCLI" pack Content/GumProject/GumProject.gumx
```

Default output is `GumProject.gumpkg` next to the `.gumx`. Override with `-o`:

```bash
"$GUMCLI" pack Content/GumProject/GumProject.gumx -o build/GumProject.gumpkg
```

gumcli is the `GumCli` .NET tool — `dotnet tool install -g GumCli`, or pin it in a local `.config/dotnet-tools.json` and invoke via `dotnet gumcli` (preferred in a build, so the tool version tracks the Gum NuGet version). See the [gumcli skill](../gumcli/SKILL.md).

## Categories (`--include`)

Default is `core,fontcache,external` — everything. Trim if your build pipeline regenerates pieces:

- `core` — `.gumx` + `.gusx`/`.gucx`/`.gutx`/`.behx`
- `fontcache` — generated `.fnt`/`.png` under `FontCache/`
- `external` — sprite-source `.png`s and custom fonts referenced by the project but living outside Core/FontCache

```bash
"$GUMCLI" pack Content/GumProject/GumProject.gumx --include core,external
```

## Runtime loading

**Pass the extension that matches the deployed artifact** — `.gumpkg` in bundle builds, `.gumx` in loose builds. The build already knows the mode, so surface it as a compile constant and switch on it:

```csharp
FlatRedBallService.Default.Initialize(this, new EngineInitSettings
{
#if GUM_BUNDLE
    GumProjectFile = "GumProject/GumProject.gumpkg"
#else
    GumProjectFile = "GumProject/GumProject.gumx"
#endif
});
```

Define `GUM_BUNDLE` from the same MSBuild property that flips deployment (see the csproj pattern below). **Do not** probe `File.Exists` to choose at runtime — on streaming platforms (Blazor/WASM) that miss is a 404, which is exactly what the extension-as-source-of-truth design avoids.

**Web must bundle.** Loose `.ganx` animation files can't be enumerated over HTTP, so in loose mode on WASM animations silently don't load. Ship web as `.gumpkg`.

## .NET version requirement

The bundle loader requires **.NET 7+** (pure-managed brotli + tar). FRB2 targets net8.0+, so this is always satisfied.

## Blazor / KNI WASM target

Loading on Blazor WebAssembly works out of the box with FRB2 — `FlatRedBallService.Initialize` installs a `TitleContainer.OpenStream` hook on Gum's `FileManager` so all bundle and asset reads route through the static-web-asset manifest. No game-side code change is needed.

There's nothing to do per project, but be aware of two constraints if you ever pull bundles in outside FRB2:
- The decompression must be pure-managed (Gum uses `BrotliSharpLib` + `SharpCompress`). The BCL `System.IO.Compression.BrotliStream` and `System.Formats.Tar` both throw `PlatformNotSupportedException` on browser-WASM.
- TitleContainer rejects rooted paths (`/Content/...`). FRB2's hook strips leading separators automatically; a custom hook must do the same.

## FRB2 csproj integration pattern

Gate pack-vs-loose behind an MSBuild property so you can flip between modes for diagnostics:

```xml
<PropertyGroup>
  <UseGumPackage Condition="'$(UseGumPackage)' == ''">false</UseGumPackage>
  <!-- Surface the mode to game code; Game1 switches GumProjectFile's extension on it. -->
  <DefineConstants Condition="'$(UseGumPackage)' == 'true'">$(DefineConstants);GUM_BUNDLE</DefineConstants>
</PropertyGroup>

<!-- Loose mode: copy every Gum file into Content/GumProject/. -->
<ItemGroup Condition="'$(UseGumPackage)' != 'true'">
  <Content Include="Content\GumProject\**\*.*"
           Exclude="Content\GumProject\**\*.gumpkg"
           CopyToOutputDirectory="PreserveNewest" />
  <None Remove="Content\GumProject\**\*.*" />
</ItemGroup>

<!-- Bundle mode: pack on build, copy only the .gumpkg. -->
<ItemGroup Condition="'$(UseGumPackage)' == 'true'">
  <Content Include="Content\GumProject\GumProject.gumpkg"
           Link="Content\GumProject\GumProject.gumpkg"
           CopyToOutputDirectory="PreserveNewest" />
  <None Remove="Content\GumProject\**\*.*" />
</ItemGroup>

<Target Name="PackGumProject"
        BeforeTargets="AssignTargetPaths"
        Condition="'$(UseGumPackage)' == 'true'"
        Inputs="@(GumSourceFiles)"
        Outputs="Content\GumProject\GumProject.gumpkg">
  <!-- gumcli = the GumCli dotnet tool pinned in .config/dotnet-tools.json; restore is idempotent. -->
  <Exec Command="dotnet tool restore" />
  <Exec Command="dotnet gumcli pack Content\GumProject\GumProject.gumx" />
</Target>
```

Toggle from the command line:

```bash
dotnet build -p:UseGumPackage=true
dotnet build -p:UseGumPackage=false   # back to loose
```

Always `.gitignore` the generated `.gumpkg` — it's a build output, not source.

## Verification

After a packed build, confirm the deployed `Content/GumProject/` contains **only** `GumProject.gumpkg` (no `.gumx`, no `Screens/`, no `FontCache/`). If you still see loose files, the bundle code path won't run and you're not actually testing it.

To confirm which mode actually loaded at runtime, check `GumService.Default.CurrentProjectResolution?.UsedBundle`. The mode is driven by the extension your code passes (the `GUM_BUNDLE` switch), not by which files happen to be on disk — renaming deployed files won't change it.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Bundle written |
| 1 | Dependency files missing on disk |
| 2 | Project failed to load, or invalid `--include` |
