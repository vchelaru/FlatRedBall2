---
name: tmx
description: "TMX map file creation and editing for FlatRedBall2. Use when creating or modifying Tiled .tmx level files, placing collision tiles, resizing maps, or adding layers. Covers the base template, StandardTileset tile IDs, layer conventions, and CSV tile data format."
---

# TMX Map Files in FlatRedBall2

TMX files are the standard level format. A base template and standard tileset live in `.claude/templates/Tiled/`.

## Setup — Copying Template Files

When a game needs a TMX level, copy these three files into the game's content directory:

1. `base.tmx` → rename to the level name (e.g., `Level1.tmx`)
2. `StandardTileset.tsx` → copy alongside the `.tmx`
3. `StandardTilesetIcons.png` → copy alongside the `.tsx`

The `.tmx` references the `.tsx` by relative path (`source="StandardTileset.tsx"`), so they must be in the same directory.

## .csproj — Copy Tiled Files to Output

TMX files are loaded at runtime via `ParseFromFile`, not the content pipeline. All Tiled files must be copied to the output directory. Add this to the `.csproj`:

```xml
<ItemGroup>
  <Content Include="Content/Tiled/**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

This covers `.tmx`, `.tsx`, and `.png` files in the `Content/Tiled/` directory.

## Base Template

`base.tmx` provides:
- 30×30 map, 16×16 pixel tiles
- StandardTileset as the only tileset (firstgid="1")
- One layer: `GameplayLayer` — a walled room (solid border, empty interior)

## StandardTileset Tile IDs

The tileset's `firstgid` is 1, so **GID in CSV = tile id + 1**. Use these GIDs in the CSV data:

| GID | Tile Type | Notes |
|-----|-----------|-------|
| 0 | Empty | No tile |
| 1 | SolidCollision | Primary solid wall/floor tile |
| 2 | SolidCollision | Visual variant |
| 3 | SolidCollision | Visual variant |
| 4 | CloudCollision | |
| 7 | OneWayCollision | |
| 10 | MovingPlatform | |
| 33 | Water | |
| 34 | BreakableCollision | |
| 35 | IceCollision | |
| 65 | Door | |
| 97 | Ladder | |

Use **GID 1** for standard solid collision. Use **GID 0** for empty space.

## Layer Conventions

- **GameplayLayer** (required) — collision/gameplay tiles using StandardTileset. Set `visible="0"` so collision tiles don't render over visual art.
- Additional visual layers are game-specific. Add them above or below GameplayLayer as needed. Visual layers use a separate art tileset (not StandardTileset).

## CSV Tile Data Format

Tile data is CSV-encoded, row-major, left-to-right, top-to-bottom. Each row has `width` comma-separated GIDs. There are `height` rows total.

```
GID,GID,GID,...,GID,
GID,GID,GID,...,GID,
...
GID,GID,GID,...,GID
```

> **Gotcha:** Every row ends with a trailing comma EXCEPT the last row. Match this exactly.

## Common Operations

### Resize the map

Change `width` and `height` on both the `<map>` element and every `<layer>` element. Regenerate CSV data to match the new dimensions.

### Place tiles

Modify CSV values in the GameplayLayer. Row 0 is the top of the map. Use the GID table above.

### Add a visual layer

Add a new `<layer>` element. Increment the `id` and update `nextlayerid` on `<map>`. If the layer uses a different tileset, add a `<tileset>` element with the next available `firstgid`.

```xml
<layer id="2" name="Background" width="30" height="30">
 <data encoding="csv">
0,0,0,...
 </data>
</layer>
```

### Add a tileset

Add before the first `<layer>`. The `firstgid` must not overlap with existing tilesets. StandardTileset has 1024 tiles (firstgid=1), so the next tileset should use `firstgid="1025"`.

```xml
<tileset firstgid="1025" source="MyArtTileset.tsx"/>
```
