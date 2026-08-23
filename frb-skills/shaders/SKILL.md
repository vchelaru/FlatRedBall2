---
name: shaders
description: "Custom shaders in FlatRedBall2. Use when adding .fx shader files or troubleshooting shader compilation errors (libmojoshader, Wine)."
---

# Shaders in FlatRedBall2

## Apos.Shapes

FlatRedBall2's built-in shape rendering (`ShapesBatch`) uses Apos.Shapes, whose shader is embedded directly in the package assembly (0.7.2+) — no content pipeline, no `.xnb`, no Wine dependency. The version is centralized in `$(AposShapesVersion)` in the repo's `Directory.Packages.props`; sample and template projects inherit it transitively and must not pin it themselves (see issue #495).

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

For OpenGL/WebGL/DirectX targets, **`ShadowDusk`** compiles cross-platform without Wine — see the `shadowdusk` skill. Metal still requires the Wine path above.

### KNI Backend

Shader compilation is not supported on macOS or Linux with the KNI backend. KNI projects must have their shaders compiled on Windows.
