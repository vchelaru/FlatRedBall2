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

### Version Guard

`ShapesBatch.AposShapesVersion` (in `src/Rendering/Batches/ShapesBatch.cs`) must match the Apos.Shapes NuGet version. In Debug builds, a mismatch throws `InvalidOperationException`. See the comment on that constant for the update checklist.

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
