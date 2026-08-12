---
name: sample-project-setup
description: "Sample Project Setup for FlatRedBall2. Use when creating a new sample project, setting up a .csproj, configuring MonoGame content pipeline, or troubleshooting 'Cannot find a manifest file' / 'dotnet-mgcb does not exist' build errors. Covers the complete checklist for new sample projects."
---

# Sample Project Setup

> **See `content-boundary` skill first.** New projects should scaffold placeholder content files (TMX, Gum, coefficients JSON) rather than hardcoding content in C#. Set the project up so the human can drop in real art, levels, and UI without recompiling.

How to create a new sample project (`.csproj`) under `samples/`. Follow this checklist exactly — two of these steps are easy to forget and cause hard-to-diagnose build failures.

> **Do not read existing sample files to verify these templates.** The content below is authoritative. Only read source files if something fails and you have a specific reason to doubt the template.

---

## Checklist

### 1. Create the directory and `.csproj`

Copy the structure from an existing sample (e.g., `AnimationChainSample`). The minimal `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RollForward>Major</RollForward>
    <PublishReadyToRun>false</PublishReadyToRun>
    <TieredCompilation>false</TieredCompilation>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MonoGame.Framework.DesktopGL" />
    <PackageReference Include="MonoGame.Content.Builder.Task" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\FlatRedBall2.csproj" />
  </ItemGroup>
</Project>
```

No `Version` attribute on either `PackageReference` — the repo uses NuGet Central Package Management, so every version is pinned once in the root `Directory.Packages.props`. If restore complains a package has no version, add it there instead of on the `PackageReference`.

Do **not** pin `Apos.Shapes` — version flows transitively from the engine. Its shader is embedded in the assembly, so it needs no content-pipeline wiring.

### 1b. Add `YourSample.slnx` (REQUIRED — easy to forget)

A sibling solution file lets the user open the sample in VS / Rider without loading every other sample in the repo. Minimal content — the sample csproj, the engine csproj, and the engine's `Animation.Content` dependency:

> **Anti-precedent warning.** Roughly a third of the existing samples in `samples/auto/` are missing this file — that's drift, not the rule. If you scaffolded a new project by copying a sibling sample, the `.slnx` may not be there to copy. Add it from the template below; do not infer the pattern from the directory listing of one neighbor.


```xml
<Solution>
  <Project Path="../../src/FlatRedBall2.csproj" />
  <Project Path="../../src/Animation.Content/FlatRedBall2.Animation.Content.csproj" />
  <Project Path="YourSample.csproj" />
</Solution>
```

Include `Animation.Content` even though the sample never references it directly: `FlatRedBall2.csproj` project-references it, and IDE solution restore (Rider/VS) needs every project in the reference graph. Omit it and the solution fails to restore with `NU1105: Unable to find project information for ...FlatRedBall2.Animation.Content.csproj`. The `dotnet` CLI follows the `ProjectReference` transitively, so a `dotnet build`/`run` of the csproj hides the gap — the error only surfaces when someone opens the `.slnx`.

### 2. Add `.config/dotnet-tools.json` (REQUIRED — easy to forget)

Without this file, the first build fails with **"Cannot find a manifest file"** / **"dotnet-mgcb does not exist"**, even though other samples build fine (they have the file already).

Copy from any existing sample:
```
samples/AnimationChainSample/.config/dotnet-tools.json  →  samples/YourSample/.config/dotnet-tools.json
```

Content (do not modify versions):
```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-mgcb": {
      "version": "3.8.4.1",
      "commands": ["mgcb"]
    },
    "dotnet-mgcb-editor": {
      "version": "3.8.4.1",
      "commands": ["mgcb-editor"]
    },
    "dotnet-mgcb-editor-linux": {
      "version": "3.8.4.1",
      "commands": ["mgcb-editor-linux"]
    },
    "dotnet-mgcb-editor-windows": {
      "version": "3.8.4.1",
      "commands": ["mgcb-editor-windows"]
    },
    "dotnet-mgcb-editor-mac": {
      "version": "3.8.4.1",
      "commands": ["mgcb-editor-mac"]
    }
  }
}
```

Then restore the tool (once per project directory):
```
cd samples/YourSample
dotnet tool restore
```

### 3. Add `Content/Content.mgcb` (REQUIRED — easy to forget)

`MonoGame.Content.Builder.Task` needs this file to drive the content pipeline for any textures, fonts, or audio the project loads via `ContentManager`. Without it, that content fails to build.

Create `Content/Content.mgcb` in the project directory with this minimal content:

```
#----------------------------- Global Properties ----------------------------#

/outputDir:bin/$(Platform)
/intermediateDir:obj/$(Platform)
/platform:DesktopGL
/config:
/profile:Reach
/compress:False

#-------------------------------- References --------------------------------#


#---------------------------------- Content ---------------------------------#

```

Even a project with no custom content yet should keep this file present — `MonoGame.Content.Builder.Task` expects it, and it's one less thing to add later.

### 4. Ask about Gum mode (REQUIRED — do not skip)

Before writing any game code, ask the user:

> "Will this project use Gum for UI (menus, HUD, score labels, any text)? If so, which mode?
> 1. **Code-only** — UI defined in C#, no .gumx file
> 2. **Project + dynamic** — .gumx editable in the Gum editor, runtime string lookup
> 3. **Project + codegen** — .gumx + generated strongly-typed C# classes"

Then invoke the `gumcli` skill and follow its instructions for the chosen mode before writing any screen or entity code.

### 5. Add `Program.cs` and `Game1.cs`

```csharp
// Program.cs
using var game = new YourSample.Game1();
game.Run();

// Game1.cs — needs `using Microsoft.Xna.Framework.Graphics;` for GraphicsProfile.
public Game1()
{
    _graphics = new GraphicsDeviceManager(this);
    // REQUIRED — Apos.Shapes needs SM 4.0+. Default GraphicsProfile is Reach (SM 2.0),
    // which crashes at startup with "Shader model 4.0 is not supported by the current
    // graphics profile 'Reach'". MonoGame tops out at HiDef; KNI uses FL10_0.
#if KNI
    _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
#else
    _graphics.GraphicsProfile = GraphicsProfile.HiDef;
#endif
    Content.RootDirectory = "Content";  // REQUIRED for ContentManager.Load of textures/fonts/audio
    IsMouseVisible = true;              // set to false only for keyboard/gamepad-only games
}

protected override void Initialize()
{
    base.Initialize();
    // Sizes the window, initializes, and starts the screen. A Glue project instead:
    // Initialize(this, "Content/FrbEditor/YourGame.gluj") — path relative, never rooted.
    FlatRedBall2.FlatRedBallService.Default.Initialize<YourScreen>(this);
}
protected override void Update(GameTime gt)
{
    if (Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();
    FlatRedBall2.FlatRedBallService.Default.Update(gt);
    base.Update(gt);
}
protected override void Draw(GameTime gt)
{
    FlatRedBall2.FlatRedBallService.Default.Draw();
    base.Draw(gt);
}
```

### 6. Build

```
dotnet build samples/YourSample/YourSample.csproj
```

---

## Why the Tools File Is Needed

`MonoGame.Content.Builder.Task` invokes `mgcb` as a local dotnet tool to build any MonoGame content. Local tools require a manifest file (`.config/dotnet-tools.json`) to locate the tool. Existing samples work because their manifests are already present and `dotnet tool restore` was run when the repo was first set up.

A new project directory has no manifest, so the content build fails. The fix is to add the manifest (identical to all other samples) and run `dotnet tool restore` once.
