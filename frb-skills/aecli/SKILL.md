---
name: aecli
description: "aecli, the command-line tool for reading and editing .achx/.achj animation files, Tiled .tsx tilesets and .seqj sequence files without the AnimationEditor window. Use when scripting or agent-editing animation chains: describe, check, new, edit --ops, --schema."
---

# aecli

Headless front end to the AnimationEditor's editing code (`tools/AnimationEditorAvalonia/src/AnimationEditor.Cli`, logic in `AnimationEditor.Editing`). Build once with `dotnet build tools/AnimationEditorAvalonia/src/AnimationEditor.Cli`, then call the exe directly: `tools/AnimationEditorAvalonia/src/AnimationEditor.Cli/bin/Debug/net10.0/aecli.exe <args>` (~0.3 s per call; `dotnet run` costs 5-9 s and `dotnet run --no-build` 2-3 s on every call). Skip the build when that exe exists and the CLI sources are unchanged; rebuild only after a `git pull` of that folder. Never use `dotnet run`, and never rebuild between calls. Every command below is the exe's arguments.

A game project that copied this skill has no FRB2 checkout beside it: set the full path of the built `aecli.exe` here, or in the environment variable `AECLI_EXE`, and use it for every call: `<not set>`. For the animation format itself see the `animation` skill.

| Command | Does |
|---|---|
| `describe <file> [--json]` | Chains, frames by index (texture, length, region as `x,y,width,height`, the form region ops take) and shapes, as stored on disk. For a `.seqj`: sources, sequences, tracks and blocks with computed end and `chainLength`. `--json` gives each `.achx` chain a `length` in seconds (unit-independent). |
| `check <file> [--json]` | Missing textures, duplicate chain or shape names, undefined unit. Exit 1 on errors. For a `.seqj`: missing sources or chains, reference problems (errors), cut-short blocks (warnings). |
| `new <file>` | Empty `.achx`, `.achj` or `.seqj`, chosen by extension. Never overwrites. |
| `convert <file> --to achj\|achx [--json]` | Writes the other format next to the file. Keeps the source, never overwrites (exit 1 if the target exists). |
| `render <file> --out <dir> [options]` | Writes PNGs without the editor, see Rendering below. |
| `edit <file> --ops ops.json [--json]` | Applies the ops in order, then saves once. |
| `--schema [--json]` | Lists every op (animation ops, then `sequenceOps` in JSON) with its fields, types and allowed values. Starts with a one-line example ops file (a `{"ops":[...]}` object, never a bare array). Run it before writing an ops file; it is the source of truth for ops. |

Exit codes: 0 ok, 1 the command ran and found a problem or failed, 2 bad command line or ops file (with `--json` also `{success:false, errors:[{code:"UsageError", message}]}` on stdout; text mode prints the message and one hint line). Not-found names carry "Did you mean ..." or the list of valid names in the message. `--json` output on `edit` and `new` is `{success, changed, errors:[{op, code, message}], warnings}`. Branch on `code` (full list in `--schema`): `NotFound` (unknown chain, shape or file), `IndexOutOfRange`, `InvalidValue` (out of range), `InvalidAddress` (malformed address or selector), `NoMatch` (selector matched nothing), `WrongShapeType`, `Ambiguous`, `Refused`; `Unspecified` is only a fallback.

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
- Keyframe flags: a chain's first frame, and the first shape of a run of frames with the same name, is always a keyframe, so turning that off fails. A curve can only be set on a keyframe; the curve is the glide to the next keyframe. `describe` lists each frame's non-zero `offset x,y` and set flips (`flip H V D`; always present in `--json`), plus `offset=skip` for a keyframe turned off and `offset-curve=Type/Easing` for a stored curve.
- `reorder-shape` takes the final position among shapes of the same type in the frame; shapes never cross types. `set-frame-event` keeps an omitted name or data, and empty `data` clears it.

## Rendering (`render`)

`render hero.seqj --sequence Run --at 0,0.4 --out shots` writes `Run_0.png`, `Run_0.4.png` (composited like the editor's sequence preview: track 0 in front) and prints the paths. `.achx/.achj`: `--chain Walk --frame 0,1` (`Walk_frame0.png`) or `--at 0.3` (`Walk_0.3.png`). `--sequence`/`--chain` may be left out when the file has only one; `--at` defaults to 0, `--frame` to 0.

- `--scale <1-16>` (default 4), `--background transparent|black|white|gray|#RRGGBB` (default transparent), `--grid` (origin axes plus a 16-unit grid).
- `--bounds auto` (default) fits one canvas to every sprite across all requested times, so images line up and are the same size. `--bounds x,y,w,h` fixes it: x,y is the lower-left corner in offset units, Y up like frame offsets; sprites outside are cropped.
- `--json` gives `{success, files, errors:[{code,message}]}`. Unknown names fail with exit 1 and a Did-you-mean; a bad option or missing `--out` is exit 2.
- A track is hidden outside its chains, so a time can legitimately draw nothing, but all requested times drawing nothing fails.

## Sequence files (.seqj)

The extension picks the op set for `describe`, `check`, `new` and `edit`; `--schema` lists both. Logic is `SequenceEdit`/`SequenceOps` in `AnimationEditor.Editing`, over the editor's `SequenceDocument`.

Addresses: sequence `Run`, track `Run/Body` or `Run/0` (a name wins over an index), block `Run/Body/1` (index in the track). A reference track (plays another sequence) has one block, its clip, at index 0. Ops: add/rename/delete/reorder `sequence`, `duplicate-sequence` (optional `name`, else `Run 2`; copy lands right after), `set-sequence-loop`; add/rename/delete/reorder `track`, `duplicate-track` (`track`, `to` sequence, optional `index`; keeps the name if free, else `Body 2`; reference tracks that would cycle fail), `set-track-continuous`, `add-reference-track`; `add-source`, `remove-source`, `replace-source`; `add-block`, `set-block-start`, `set-block-length` (`length` seconds, or `clear: true` back to auto), `set-block-end` (length so the block ends at `end` seconds, using its current start; clips too), `set-block-chain` (`chain`, optional `source`; keeps start and length; not for clips), `delete-block`. A block with no length plays one cycle of its chain (a clip, one run of its target) and follows edits to it; `describe` shows it as `length auto`. Setters and `delete-*` take selectors (`Run/*`, `Run/0-1`, `Run/Body/0,2`, globs); see `--schema`.

- `add-block` needs the source in the library (`add-source` first) and the chain in that source.
- `add-track` and `add-reference-track` insert at `index` (default 0, the front: index 0 draws on top).
- A block is cut short by the next block on its track; `check` warns, `describe` shows the cut end.
- Not available: moving a block between tracks, track flip fields (the model has none).

## Tiled tilesets (.tsx)

`describe`, `check` and `edit` open a `.tsx` as the editor's native project (`TsxOpPolicy`, `TsxEdit` in `AnimationEditor.Editing`). A tileset the editor cannot open (image collection, or one already an association target) is refused up front with the editor's reason (`UnsupportedTsx`). `describe` shows pixels and each chain's `ownerTile`.

- A tile animation stores only frame rectangles and durations, so only chain ops (`rename|add|delete|duplicate|reorder-chain`), frame ops (`add-frame`, `add-frames-grid`, `delete|duplicate|reorder-frame`, `set-frame-length|region`) and `set-chain-owner-tile` work. Anything else fails with `NotSupportedInTsx` and the reason. `add-frame` needs `region` and the tileset's own image.
- `set-chain-owner-tile` takes `tile` or `clear` (back to the first frame's tile). A tile owned by another chain fails; a duplicated chain shares its source's tile, so give it one in the same batch or the batch fails (`DuplicateOwnerTile`). Frames that are not whole tiles fail the batch (`UnmappableChain`).
- Save goes through the editor's `TsxWriter`, so wangsets, object layers, properties and other tiles survive (XML spacing and `tintcolor` may be normalised). Save notes arrive as `warnings`.
- `associate-tileset` (`path`) is for `.achx/.achj`: it writes the `.tiledsync` file, and the batch's save then pushes the animations onto that tileset (reported in `changed`; sync failures are `warnings`). A tileset that is an association target cannot be opened natively.
- `new` and `convert` do not handle `.tsx`; author the tileset in Tiled.
