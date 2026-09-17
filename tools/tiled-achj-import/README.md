# tiled-achj-import

A Tiled (mapeditor.org) scripting extension that imports a FlatRedBall2 AnimationEditor
`.achj` (JSON) or `.achx` (FRB1 XML) file's animation chains into the currently open
tileset, as Tiled tile animations (`Tile.frames`).

## This is a lossy conversion

Tiled's tile-animation model is strictly poorer than `.achj`/`.achx`: a tile's `frames`
is just `{ tileId, duration }[]`, where every frame must be another tile *already in
the same tileset, on the same grid*. There's no per-frame flip, offset, tint, or
cross-texture frame, and no concept of a named animation chain - Tiled animates from a
starting tile, not a name.

What this importer does about that, per chain:

- Matches each frame's pixel rect to a tile in the open tileset by position. Frames
  must be grid-aligned and exactly one tile in size - anything else is skipped with a
  warning. `"coordinateType": "UV"` frames are converted to pixels using the tileset
  image's actual pixel size (loaded via Tiled's `Image` class); if that load fails,
  UV frames are skipped with a warning instead.
- Skips frames whose `textureName` doesn't match the tileset's image (a chain can span
  multiple textures; Tiled tile animations can't).
- Keeps a flipped frame's tile but drops the flip, with a warning - the tile still
  looks *close*, not correct.
- Only maps tilesets with zero margin/spacing (tile-id arithmetic elsewhere assumes a
  plain grid).
- Sets `frames` on the *first frame's* tile, and stamps a custom property
  `achjAnimationName` on it so the chain's name survives as metadata, even though
  Tiled itself never reads it.

If none of that fits your chain (non-grid frames, per-frame flips that matter,
multi-texture chains), this tool isn't the answer for it - author the animation in
Tiled's own Tile Animation Editor instead.

## Install

Both files must stay `.mjs` (not `.js`) - Tiled only allows `import`/`export` between
extension files when they're loaded as ES modules, which requires the `.mjs`
extension.

Copy this folder into Tiled's extensions directory. Tiled loads extensions on startup;
use **Edit > Reload Extensions** if your version has that menu item, otherwise restart
Tiled:

- Windows: `%LOCALAPPDATA%\Tiled\extensions\`
- Linux/macOS: `~/.local/share/Tiled/extensions/`

(Or **Edit > Preferences > Plugins and Extensions** to find the folder Tiled is
already watching.)

## Use

With a tileset open in Tiled: **Edit > Import AnimationChain Frames...**, pick a
`.achj` or `.achx` file. Warnings and a summary go to the Console view (**View > Views
and Toolbars > Console**) and a dialog.

## Development

`achj-mapper.mjs` is the pure conversion logic (no dependency on Tiled's `tiled`/
`Tileset`/`TextFile`/`Image` scripting globals) and is unit tested under plain Node:

```
node --test
```

`achj-import.mjs` is the thin wiring layer that calls into it from inside Tiled's
scripting engine; it can only be exercised by running it inside Tiled itself.
