---
name: aecli
description: "aecli, the command-line tool for reading and editing .achx/.achj animation files and .seqj sequence files without the AnimationEditor window. Use when scripting or agent-editing animation chains: describe, check, new, edit --ops, --schema."
---

# aecli

Headless front end to the AnimationEditor's editing code (`tools/AnimationEditorAvalonia/src/AnimationEditor.Cli`, logic in `AnimationEditor.Editing`). Run it from the repo with `dotnet run --project tools/AnimationEditorAvalonia/src/AnimationEditor.Cli -- <args>`. For the animation format itself see the `animation` skill.

| Command | Does |
|---|---|
| `describe <file> [--json]` | Chains, frames by index (texture, length, region) and shapes, as stored on disk. For a `.seqj`: sources, sequences, tracks and blocks with computed end. |
| `check <file> [--json]` | Missing textures, duplicate chain or shape names, undefined unit. Exit 1 on errors. For a `.seqj`: missing sources or chains, reference problems (errors), cut-short blocks (warnings). |
| `new <file>` | Empty `.achx`, `.achj` or `.seqj`, chosen by extension. Never overwrites. |
| `convert <file> --to achj\|achx [--json]` | Writes the other format next to the file. Keeps the source, never overwrites (exit 1 if the target exists). |
| `edit <file> --ops ops.json [--json]` | Applies the ops in order, then saves once. |
| `--schema [--json]` | Lists every op (animation ops, then `sequenceOps` in JSON) with its fields, types and allowed values. Run it before writing an ops file; it is the source of truth for ops. |

Exit codes: 0 ok, 1 the command ran and found a problem or failed, 2 bad command line or ops file. `--json` output on `edit` and `new` is `{success, changed, errors:[{op, code, message}], warnings}`.

## Addresses

Chain `Walk`, frame `Walk/2` (zero-based), shape `Walk/2/rect/Hitbox` (type is `rect`, `circle` or `polygon`). Names containing `/` cannot be addressed; rename them in the editor. Frame event `Walk/2/event/0` (zero-based position; `describe` prints these).

Ops cover renaming, texture changes, and structure: `add-chain`, `delete-chain`, `duplicate-chain`, `reorder-chain`, `add-frame`, `delete-frame`, `duplicate-frame`, `reorder-frame`. Property ops: `set-chain-loop|pingpong|reverse|locked` (`value` true/false), `set-frame-length|region|offset|color|flip`, and file-level `set-unit`. Shape ops: `add-shape`, `delete-shape`, `duplicate-shape`, `set-rect`, `set-circle`, `set-polygon`, `set-shape-common` (x, y only). Bulk: `add-frames-grid` (one frame per sprite sheet cell). Fields are in `--schema`. Detail ops: `add-frame-event`, `set-frame-event`, `delete-frame-event`, `set-frame-keyframe|-curve` (group offset or color), `set-shape-keyframe|-curve` (group offset, size or points), `reorder-shape`, `clear-frame-color`.

**Bulk selectors.** Frame ops take `Walk/*`, `Walk/2-5`, `Walk/0,3,7`; chain flag setters and the chain part of frame addresses take globs (`Walk*`); shape addresses take the same (`Walk/*/rect/Hitbox`). A selector matching nothing, or any index past a chain end, is an error, and one failing target fails the whole batch. `--schema` has the full rules.

## Landmines

- `reorder-*` takes the final zero-based position. Moving to the current position changes nothing.
- `add-chain` and a named `duplicate-chain` fail on a name already in use (the editor renames silently, aecli does not). An unnamed `duplicate-chain` numbers the copy (`Walk` becomes `Walk2`).
- `add-frame` shows the whole texture unless `region` (`x,y,width,height` in pixels) is given. `region` needs a PNG, because the size is read from the PNG header. `length` is in the file's own time unit; leave it out for that unit's default.
- Setting a value a thing already has is a no-op. Every edit of a locked chain fails with code `Locked`, except `set-chain-locked` itself; `set-unit` converts locked chains too.
- `set-frame-length` is in the file's unit and range-checked (0.001-60 s, 1-60000 ms). `set-unit` converts all lengths so playback time stays the same; later ops in the batch use the new unit.
- `set-frame-region` needs the frame's texture to exist and be a PNG. `set-frame-flip` sets flags (not a toggle) and mirrors the offset and shapes like the editor. `set-frame-color` only sets; `clear-frame-color` returns parts to inherited.
- Frame indexes shift after `delete-frame`, `duplicate-frame` and `add-frame`; address later frames of the same batch by their new index.

- Ops apply in order, so after `rename-chain` later ops must use the new name.
- `edit` writes nothing unless every op succeeds and no new `check` error appears. A batch that changes nothing leaves the file byte-identical.
- A file that already has check errors can still be edited, with one exception: a `.achx` with a missing texture cannot be saved in pixel coordinates (the editor's save path needs the texture size), so every edit of it fails. Fix the texture path first.
- A file in legacy UV coordinates is refused. Open and save it in the editor once.
- `set-frame-texture` takes a path relative to the animation file or absolute. Outside the file's folder it references the texture in place unless `copy` is `copy` or `overwrite`. The image check reads the file header only, so a truncated PNG passes.
- `add-shape` takes `frame`, `type` and an optional `name` (default unique per frame; unique across all three types). Defaults match the editor's Add buttons, centred on the frame offset. Not yet available: moving a shape to another frame, shape color.
- `set-rect|circle|polygon` need that exact type in the address, change only the fields given, and require scale and radius above 0. `set-polygon` `points` is `[[x,y],...]`, at least 3, stored exactly as given: the editor's closed outlines repeat the first point at the end, so repeat it to keep one closed.
- Keyframe flags: a chain's first frame, and the first shape of a run of frames with the same name, is always a keyframe, so turning that off fails. A curve can only be set on a keyframe; the curve is the glide to the next keyframe. `describe` lists `offset=skip` for a keyframe turned off and `offset-curve=Type/Easing` for a stored curve.
- `reorder-shape` takes the final position among shapes of the same type in the frame; shapes never cross types. `set-frame-event` keeps an omitted name or data, and empty `data` clears it.

## Sequence files (.seqj)

The extension picks the op set for `describe`, `check`, `new` and `edit`; `--schema` lists both. Logic is `SequenceEdit`/`SequenceOps` in `AnimationEditor.Editing`, over the editor's `SequenceDocument`.

Addresses: sequence `Run`, track `Run/Body` or `Run/0` (a name wins over an index), block `Run/Body/1` (index in the track). A reference track (plays another sequence) has one block, its clip, at index 0. Ops: add/rename/delete/reorder `sequence`, `set-sequence-loop`; add/rename/delete/reorder `track`, `set-track-continuous`, `add-reference-track`; `add-source`, `remove-source`, `replace-source`; `add-block`, `set-block-start`, `set-block-loops`, `delete-block`. Setters and `delete-*` take selectors (`Run/*`, `Run/0-1`, `Run/Body/0,2`, globs); see `--schema`.

- `add-block` needs the source in the library (`add-source` first) and the chain in that source.
- `add-track` and `add-reference-track` insert at `index` (default 0, the front: index 0 draws on top).
- A block is cut short by the next block on its track; `check` warns, `describe` shows the cut end.
- Not available: moving a block between tracks, track flip fields (the model has none).
