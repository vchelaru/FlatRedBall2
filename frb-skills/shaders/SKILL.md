---
name: shaders
description: "Custom shaders in FlatRedBall2. Use when adding .fx shader files, troubleshooting shader compilation errors (libmojoshader, Wine), or working with precompiled shader XNBs."
---

# Shaders in FlatRedBall2

## Precompiled Shaders

FlatRedBall2 ships precompiled `.xnb` shaders for its built-in Apos.Shapes dependency. These live in `src/PrecompiledShaders/` with one subfolder per platform:

| Platform | Subfolder | Set by |
|----------|-----------|--------|
| MonoGame DesktopGL | `DesktopGL/` | `MonoGamePlatform` property |
| KNI BlazorGL | `BlazorGL/` | `KniPlatform` property |

Projects import `AposShapesPrecompiled.props` to use these instead of compiling from source. Unknown platforms fall back to normal Apos.Shapes shader compilation.

**These xnbs are duplicated in a second place: the dotnet templates' build folders** — `templates/frb2-desktop/build/` and `templates/frb2-multiplatform/build/` (each has a `DesktopGL/`; multiplatform also `BlazorGL/`), plus a copy of `AposShapesPrecompiled.props`. Any edit to a `src/PrecompiledShaders/<platform>/apos-shapes.xnb` must be copied byte-for-byte into the matching template folder(s). `TemplatePackageReferenceTests` fails CI on drift — run it after touching an xnb. This is invariant for *any* xnb change; the `AposShapes.props` rebuild checklist frames the sync under a version bump, but it isn't bump-specific.

The package version is centralized in `$(AposShapesVersion)` (`src/PrecompiledShaders/AposShapes.props`), imported by the engine. Apos.Shapes ships only `buildTransitive` assets, so both the version and the `SkipAposShapeContent` precompiled-XNB skip flow identically whether the package is referenced directly or transitively — sample Desktop projects inherit it from the engine and don't pin it. Re-adding a per-sample pin only reintroduces the drift `NU1605` then fails the build on; bump the prop instead.

### Version Guard

`ShapesBatch.AposShapesVersion` (in `src/Rendering/Batches/ShapesBatch.cs`) is a C# const that can't read MSBuild, so it mirrors `$(AposShapesVersion)` by hand. In Debug builds, a mismatch against the loaded Apos.Shapes assembly throws `InvalidOperationException`. See the comment on that constant (and `AposShapes.props`) for the rebuild checklist.

## Custom Shaders

Adding your own `.fx` files to a project requires the MonoGame/KNI content pipeline to compile them at build time.

### Windows

Install the **Visual C++ 2013 Redistributable (x64)** — the content pipeline's `libmojoshader_64.dll` depends on it:

```
winget install Microsoft.VCRedist.2013.x64
```

Or download from https://www.microsoft.com/download/details.aspx?id=40784.

Without it, builds fail with:
```
Unable to load DLL 'libmojoshader_64.dll' or one of its dependencies.
```

### macOS and Linux

The MonoGame content pipeline uses Windows-only tools for shader compilation. **Wine** must be installed.

Follow the MonoGame setup guide:
https://docs.monogame.net/articles/tutorials/building_2d_games/02_getting_started/index.html?tabs=macos#setup-wine-for-effect-compilation-macos-and-linux-only

### KNI Backend

Shader compilation is not supported on macOS or Linux with the KNI backend. KNI projects must have their shaders compiled on Windows.
