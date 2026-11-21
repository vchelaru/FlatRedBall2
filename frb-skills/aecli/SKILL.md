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
| `convert <file> --to achj\|achx [--json]` | Writes the other format next to the file. Keeps the source, never overwrites (exit 1 if the target exists). |
| `edit <file> --ops ops.json [--json]` | Applies the ops in order, then saves once. |
| `--schema [--json]` | Lists every op with its fields, types and allowed values. Run it before writing an ops file; it is the source of truth for ops. |

Exit codes: 0 ok, 1 the command ran and found a problem or failed, 2 bad command line or ops file. `--json` output on `edit` and `new` is `{success, changed, errors:[{op, code, message}], warnings}`.

## Addresses

Chain `Walk`, frame `Walk/2` (zero-based), shape `Walk/2/rect/Hitbox` (type is `rect`, `circle` or `polygon`). Names containing `/` cannot be addressed; rename them in the editor.

Ops cover renaming, texture changes, and structure: `add-chain`, `delete-chain`, `duplicate-chain`, `reorder-chain`, `add-frame`, `delete-frame`, `duplicate-frame`, `reorder-frame`. Property ops: `set-chain-loop|pingpong|reverse|locked` (`value` true/false), `set-frame-length|region|offset|color|flip`, and file-level `set-unit`. Shape ops: `add-shape`, `delete-shape`, `duplicate-shape`, `set-rect`, `set-circle`, `set-polygon`, `set-shape-common` (x, y only). Fields are in `--schema`.

## Landmines

- `reorder-*` takes the final zero-based position. Moving to the current position changes nothing.
- `add-chain` and a named `duplicate-chain` fail on a name already in use (the editor renames silently, aecli does not). An unnamed `duplicate-chain` numbers the copy (`Walk` becomes `Walk2`).
- `add-frame` shows the whole texture unless `region` (`x,y,width,height` in pixels) is given. `region` needs a PNG, because the size is read from the PNG header. `length` is in the file's own time unit; leave it out for that unit's default.
- Setting a value a thing already has is a no-op. Every edit of a locked chain fails with code `Locked`, except `set-chain-locked` itself; `set-unit` converts locked chains too.
- `set-frame-length` is in the file's unit and range-checked (0.001-60 s, 1-60000 ms). `set-unit` converts all lengths so playback time stays the same; later ops in the batch use the new unit.
- `set-frame-region` needs the frame's texture to exist and be a PNG. `set-frame-flip` sets flags (not a toggle) and mirrors the offset and shapes like the editor. `set-frame-color` cannot clear a channel back to inherited.
- Frame indexes shift after `delete-frame`, `duplicate-frame` and `add-frame`; address later frames of the same batch by their new index.

- Ops apply in order, so after `rename-chain` later ops must use the new name.
- `edit` writes nothing unless every op succeeds and no new `check` error appears. A batch that changes nothing leaves the file byte-identical.
- A file that already has check errors can still be edited, with one exception: a `.achx` with a missing texture cannot be saved in pixel coordinates (the editor's save path needs the texture size), so every edit of it fails. Fix the texture path first.
- A file in legacy UV coordinates is refused. Open and save it in the editor once.
- `set-frame-texture` takes a path relative to the animation file or absolute. Outside the file's folder it references the texture in place unless `copy` is `copy` or `overwrite`. The image check reads the file header only, so a truncated PNG passes.
- `add-shape` takes `frame`, `type` and an optional `name` (default unique per frame; unique across all three types). Defaults match the editor's Add buttons, centred on the frame offset. Not yet available: moving a shape to another frame, shape color or keyframe flags.
- `set-rect|circle|polygon` need that exact type in the address, change only the fields given, and require scale and radius above 0. `set-polygon` `points` is `[[x,y],...]`, at least 3, stored exactly as given: the editor's closed outlines repeat the first point at the end, so repeat it to keep one closed.
