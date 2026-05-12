---
name: gum-packaging
description: "Bundle a Gum project into a single .gumpkg file (tar+brotli) for distribution. Trigger when shipping a built game, optimizing initial load on web/BlazorGL, or when the user mentions 'gum pack', '.gumpkg', or wants fewer loose Content files. Covers gumcli pack, runtime loading, and the loose-vs-bundle toggle for diagnostics."
---

# Gum Packaging (.gumpkg)

`gumcli pack` walks a `.gumx` project's dependencies and writes a single tar+brotli bundle (`.gumpkg`) containing the elements, font cache, and external textures. At runtime the engine transparently loads from the bundle when the loose `.gumx` is absent.

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

See the [gumcli skill](../gumcli/SKILL.md) for how to locate `gumcli.exe`.

## Categories (`--include`)

Default is `core,fontcache,external` — everything. Trim if your build pipeline regenerates pieces:

- `core` — `.gumx` + `.gusx`/`.gucx`/`.gutx`/`.behx`
- `fontcache` — generated `.fnt`/`.png` under `FontCache/`
- `external` — sprite-source `.png`s and custom fonts referenced by the project but living outside Core/FontCache

```bash
"$GUMCLI" pack Content/GumProject/GumProject.gumx --include core,external
```

## Runtime loading

**Code does not change.** Continue passing the `.gumx` path:

```csharp
FlatRedBallService.Default.Initialize(this, new EngineInitSettings
{
    GumProjectFile = "GumProject/GumProject.gumx"
});
```

The loader looks for a sibling `.gumpkg` (same directory, same basename) and uses it when the loose `.gumx` is **not** present in the deployed Content directory.

**Loose wins when both exist.** This is by design: in dev you keep loose files (with hot reload); in a published build you ship only the `.gumpkg`. To test the bundle path, you must exclude the loose files from your output — having both deployed silently falls back to loose mode.

## .NET version requirement

The bundle loader requires **.NET 7+**. On older targets the `.gumpkg` is ignored and the loader falls back to loose-file resolution.

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
  <Exec Command="&quot;$(GumCliPath)&quot; pack Content\GumProject\GumProject.gumx" />
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

For a quick log line at runtime, watch for whether `GumService` reports loose vs. bundle source — or temporarily rename the deployed `.gumx` to force bundle mode.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Bundle written |
| 1 | Dependency files missing on disk |
| 2 | Project failed to load, or invalid `--include` |
