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
  multiple textures; Tiled tile animations can't). Warned about in single-file mode;
  silent in project-folder mode, where it's the expected case for most files scanned.
- Keeps a flipped frame's tile but drops the flip, with a warning - the tile still
  looks *close*, not correct.
- Only maps tilesets with zero margin/spacing (tile-id arithmetic elsewhere assumes a
  plain grid).
- Sets `frames` on the *first frame's* tile, and stamps custom properties
  `achjAnimationName` (the chain's name) and `achjSourceFile` (the `.achx`/`.achj` path
  it came from) on it, since Tiled itself never reads either.

If none of that fits your chain (non-grid frames, per-frame flips that matter,
multi-texture chains), this tool isn't the answer for it - author the animation in
Tiled's own Tile Animation Editor instead.

## Install

Copy only **`achj-mapper.mjs` and `achj-import.mjs`** (not `achj-mapper.test.mjs` -
Tiled auto-loads *every* script file in an extensions folder, and the test file uses
syntax `node --test` accepts but Tiled's embedded JS engine doesn't) into Tiled's
extensions directory. Both must stay `.mjs` (not `.js`) - Tiled only allows
`import`/`export` between extension files when they're loaded as ES modules, which
requires the `.mjs` extension.

Tiled loads extensions on startup and auto-reloads them when a file there changes,
so no restart is needed after the first install:

- Windows: `%LOCALAPPDATA%\Tiled\extensions\`
- Linux/macOS: `~/.local/share/Tiled/extensions/`

(Or **Edit > Preferences > Plugins and Extensions** to find the folder Tiled is
already watching.)

Tiled's embedded JS engine is also missing some newer `Array.prototype` methods that
exist under Node (confirmed so far: `flatMap`, which throws a runtime `TypeError`
despite being valid syntax) - if you see a similar `TypeError: Property '...' ... is
not a function` in the Console after editing either file, that's most likely another
one; replace it with an equivalent built from `.map`/`.filter`/`.reduce`/spread instead.

## Use

Two ways to run it, both under **Edit** with a tileset open:

- **Import AnimationChain Frames...** - pick a single `.achj`/`.achx` file.
- **Set AnimationChain Project Folder...** - pick a project's root folder once. Every
  `.achx`/`.achj` file under it (recursively) is scanned, and any frame whose
  `textureName` matches *this tileset's* image is applied - everything else is silently
  skipped, since in a big project most chains belong to some other tileset. The folder
  is remembered as a tileset custom property (`achjProjectRoot`), stored *relative to
  the tileset's own `.tsx` location* so the `.tsx` stays portable across machines and
  checkouts (a differently-checked-out repo, a teammate's machine) instead of baking in
  one absolute path, and reapplied
  automatically every time this tileset is opened in Tiled (`tiled.assetOpened`), so
  edits made in AnimationEditor since the last session show up without a manual step -
  though only *on open*, not live while both apps are running side by side (Tiled's
  scripting engine has no confirmed timer API to poll for changes on an interval). Use
  **Re-import AnimationChain Project** to force a re-run without reopening the tileset.

Warnings and a summary go to the Console view (**View > Views and Toolbars >
Console**); manual runs also show a summary dialog. The automatic on-open run only
logs, since a popup on every tileset open would get old fast.

## Development

`achj-mapper.mjs` is the pure conversion logic (no dependency on Tiled's `tiled`/
`Tileset`/`TextFile`/`Image`/`File` scripting globals) and is unit tested under plain
Node:

```
node --test
```

`achj-import.mjs` is the thin wiring layer that calls into it from inside Tiled's
scripting engine; it can only be exercised by running it inside Tiled itself.
