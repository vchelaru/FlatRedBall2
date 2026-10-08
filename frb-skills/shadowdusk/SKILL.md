---
name: shadowdusk
description: "Cross-platform HLSL shader compiler for MonoGame/KNI/FNA — avoids the Windows/Wine requirement. Triggers: ShadowDusk, mgfxc replacement, compiling .fx on macOS/Linux, ShadowDusk.Compiler, ShadowDuskCLI."
---

# ShadowDusk

Compiles `.fx` HLSL into `.mgfx` (MonoGame/KNI) or `.fxb` (FNA) on any OS — no DirectX SDK, no Wine. Docs: https://kaltinril.github.io/ShadowDusk/

Two forms: `ShadowDusk.Compiler` (NuGet, in-memory `EffectCompiler.CompileAsync(hlslSource, options)` — use this for build-time embedding of engine-shipped shaders) and `ShadowDuskCLI` (drop-in `mgfxc` replacement for existing content-pipeline builds).

**Gotcha:** Metal (macOS/iOS) isn't implemented yet — Metal targets still need the traditional Windows/Wine path (see `shaders` skill). Output is behaviorally equivalent to `mgfxc`, not byte-identical — don't diff bytecode across compilers in a test.
