---
name: sample-project-setup
description: "Sample Project Setup for FlatRedBall2. Use when creating a new sample project, setting up a .csproj or .slnx, or wiring the KNI content pipeline. Covers the checklist for new sample projects."
---

# Sample Project Setup

> **See `content-boundary` skill first.** New projects should scaffold placeholder content files (TMX, Gum, coefficients JSON) rather than hardcoding content in C#. Set the project up so the human can drop in real art, levels, and UI without recompiling.

How to create a new sample project (`.csproj`) under `samples/`. Follow this checklist exactly.

> **Do not read existing sample files to verify these templates.** The content below is authoritative. Only read source files if something fails and you have a specific reason to doubt the template.

---

## Checklist

### 1. Create the directory and `.csproj`

The minimal `.csproj` (KNI desktop, SDL2/OpenGL):

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
    <KniPlatform>DesktopGL</KniPlatform>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="nkast.Xna.Framework" />
    <PackageReference Include="nkast.Xna.Framework.Graphics" />
    <PackageReference Include="nkast.Xna.Framework.Game" />
    <PackageReference Include="nkast.Xna.Framework.Input" />
    <PackageReference Include="nkast.Xna.Framework.Content" />
    <PackageReference Include="nkast.Xna.Framework.Audio" />
    <PackageReference Include="nkast.Xna.Framework.Media" />
    <PackageReference Include="nkast.Kni.Platform.SDL2.GL" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Kni\FlatRedBall2.Kni.csproj" />
  </ItemGroup>
</Project>
```

No `Version` attribute on any `PackageReference` — the repo uses NuGet Central Package Management, so every version is pinned once in the root `Directory.Packages.props`. If restore complains a package has no version, add it there.

Do **not** pin `Apos.Shapes` — version flows transitively from the engine. Its shader is embedded in the assembly, so it needs no content-pipeline wiring.

### 1b. Add `YourSample.slnx` (REQUIRED — easy to forget)

A sibling solution file lets the user open the sample in VS / Rider without loading every other sample in the repo. List the sample, the engine, and the engine's `AnimationChain.Common` dependency:

```xml
<Solution>
  <Project Path="../../src/Kni/FlatRedBall2.Kni.csproj" />
  <Project Path="../../src/AnimationChain.Common/AnimationChain.Common.csproj" />
  <Project Path="YourSample.csproj" />
</Solution>
```

Include `AnimationChain.Common` even though the sample never references it directly: the engine project-references it, and IDE solution restore needs every project in the reference graph. Omit it and the solution fails to restore with `NU1105`; `dotnet build` hides the gap because the CLI follows `ProjectReference` transitively.

Samples with a BlazorGL head also list `FlatRedBall2.BlazorGL`. Don't infer the layout from a neighbor that lacks a `.slnx`; use this template. `SolutionMembershipTests` fails if a referenced project isn't a member.

### 2. Content pipeline (only if the project builds XNBs)

Raw assets (PNG, JSON, TMX, OGG, `.achx`) need no pipeline: put them under `Content/` and copy them to the output with `<Content Include=... CopyToOutputDirectory="PreserveNewest" />`. A head that really needs XNBs adds `nkast.Xna.Framework.Content.Pipeline.Builder` and an explicit `<KniContentReference Include="Content/Content.mgcb" />`. The builder does not discover `.mgcb` files on its own, so without that item it silently builds nothing. Output lands in `Content/bin/DesktopGL/` and is copied beside the exe.

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

// Game1.cs — needs `using Microsoft.Xna.Framework.Graphics;` for GraphicsProfile.
public Game1()
{
    _graphics = new GraphicsDeviceManager(this);
    // REQUIRED — Apos.Shapes needs SM 4.0+. Default GraphicsProfile is Reach (SM 2.0),
    // which crashes at startup with "Shader model 4.0 is not supported by the current
    // graphics profile 'Reach'".
    _graphics.GraphicsProfile = GraphicsProfile.FL10_0;
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

### 5. Build

```
dotnet build samples/YourSample/YourSample.csproj
```
