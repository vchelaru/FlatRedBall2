---
name: aecli
description: "aecli, the command-line tool for reading and editing .achx/.achj animation files without the AnimationEditor window. Use when scripting or agent-editing animation chains: describe, check, new, edit --ops, --schema."
---

# aecli

Headless front end to the AnimationEditor's editing code (`tools/AnimationEditorAvalonia/src/AnimationEditor.Cli`, logic in `AnimationEditor.Editing`). Run it from the repo with `dotnet run --project tools/AnimationEditorAvalonia/src/AnimationEditor.Cli -- <args>`. For the animation format itself see the `animation` skill.

| Command | Does |
|---|---|
| `describe <file> [--json]` | Chains, frames by index (texture, length, region) and shapes, as stored on disk. |
| `check <file> [--json]` | Missing textures, duplicate chain or shape names, undefined unit. Exit 1 on errors. |
| `new <file>` | Empty `.achx` or `.achj`, chosen by extension. Never overwrites. |
| `edit <file> --ops ops.json [--json]` | Applies the ops in order, then saves once. |
| `--schema [--json]` | Lists every op with its fields, types and allowed values. Run it before writing an ops file; it is the source of truth for ops. |

Exit codes: 0 ok, 1 the command ran and found a problem or failed, 2 bad command line or ops file. `--json` output on `edit` and `new` is `{success, changed, errors:[{op, code, message}], warnings}`.

## Addresses

Chain `Walk`, frame `Walk/2` (zero-based), shape `Walk/2/rect/Hitbox` (type is `rect`, `circle` or `polygon`). Names containing `/` cannot be addressed; rename them in the editor.

## Landmines

- Ops apply in order, so after `rename-chain` later ops must use the new name.
- `edit` writes nothing unless every op succeeds and no new `check` error appears. A batch that changes nothing leaves the file byte-identical.
- A file that already has check errors can still be edited, with one exception: a `.achx` with a missing texture cannot be saved in pixel coordinates (the editor's save path needs the texture size), so every edit of it fails. Fix the texture path first.
- A file in legacy UV coordinates is refused. Open and save it in the editor once.
- `set-frame-texture` takes a path relative to the animation file or absolute. Outside the file's folder it references the texture in place unless `copy` is `copy` or `overwrite`. The image check reads the file header only, so a truncated PNG passes.
