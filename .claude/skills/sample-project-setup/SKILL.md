---
name: sample-project-setup
description: "Sample Project Setup for FlatRedBall2. Use when creating a new sample project, setting up a .csproj, configuring MonoGame content pipeline, or troubleshooting 'Cannot find a manifest file' / 'dotnet-mgcb does not exist' build errors. Covers the complete checklist for new sample projects."
---

# Sample Project Setup

How to create a new sample project (`.csproj`) under `samples/`. Follow this checklist exactly — two of these steps are easy to forget and cause hard-to-diagnose build failures.

> **Do not read existing sample files to verify these templates.** The content below is authoritative. Only read source files if something fails and you have a specific reason to doubt the template.

---

## Checklist

### 1. Create the directory and `.csproj`

Copy the structure from an existing sample (e.g., `PlatformerSample`). The minimal `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RollForward>Major</RollForward>
    <PublishReadyToRun>false</PublishReadyToRun>
    <TieredCompilation>false</TieredCompilation>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Apos.Shapes" Version="0.6.8" />
    <PackageReference Include="MonoGame.Framework.DesktopGL" Version="3.8.*" />
    <PackageReference Include="MonoGame.Content.Builder.Task" Version="3.8.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\FlatRedBall2.csproj" />
  </ItemGroup>
</Project>
```

### 2. Add `.config/dotnet-tools.json` (REQUIRED — easy to forget)

Without this file, the first build fails with **"Cannot find a manifest file"** / **"dotnet-mgcb does not exist"**, even though other samples build fine (they have the file already).

Copy from any existing sample:
```
samples/PlatformerSample/.config/dotnet-tools.json  →  samples/YourSample/.config/dotnet-tools.json
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

### 3. Ask about Gum mode (REQUIRED — do not skip)

Before writing any game code, ask the user:

> "Will this project use Gum for UI (menus, HUD, score labels, any text)? If so, which mode?
> 1. **Code-only** — UI defined in C#, no .gumx file
> 2. **Project + dynamic** — .gumx editable in the Gum editor, runtime string lookup
> 3. **Project + codegen** — .gumx + generated strongly-typed C# classes"

Then invoke the `gumcli` skill and follow its instructions for the chosen mode before writing any screen or entity code.

### 4. Add `Program.cs` and `Game1.cs`

```csharp
// Program.cs
using var game = new YourSample.Game1();
game.Run();

// Game1.cs
protected override void Initialize()
{
    base.Initialize();
    FlatRedBall2.FlatRedBallService.Default.Initialize(this);
    FlatRedBall2.FlatRedBallService.Default.Start<YourScreen>();
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

### 5. Build

```
dotnet build samples/YourSample/YourSample.csproj
```

---

## Why the Tools File Is Needed

`MonoGame.Content.Builder.Task` invokes `mgcb` as a local dotnet tool to build any MonoGame content (including content from `Apos.Shapes` via `buildTransitive`). Local tools require a manifest file (`.config/dotnet-tools.json`) to locate the tool. Existing samples work because their manifests are already present and `dotnet tool restore` was run when the repo was first set up.

A new project directory has no manifest, so the content build fails. The fix is to add the manifest (identical to all other samples) and run `dotnet tool restore` once.
