---
name: tmx
description: "TMX map file creation and editing for FlatRedBall2. Use when creating or modifying Tiled .tmx level files, placing collision tiles, resizing maps, or adding layers. Covers the base template, StandardTileset tile IDs, layer conventions, and CSV tile data format."
---

# TMX Map Files in FlatRedBall2

> **See `content-boundary` skill first.** TMX files are a human-edited content format — AI should scaffold a minimal valid TMX and tell the user to open it in Tiled for real level design. Do not try to author detailed levels in XML.

TMX files are the standard level format. A base template and standard tileset live in `.claude/templates/Tiled/`.

## Communication convention — reference tiles by numeric ID

When discussing a tile (its polygon, class, position, collision shape, etc.), always refer to it by its numeric **tile ID** — e.g., "tile ID 11's polygon" rather than "the slope triangle". The ID is the unambiguous handle that matches the `.tsx` file and Tiled's editor; descriptions alone are ambiguous when multiple tiles have similar shapes.

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
| 4 | JumpThroughCollision | Cloud-style: solid from above, drop-through with Down+Jump |
| 7 | OneWayCollision | Hard one-way barrier: pass once, never return (e.g., Yoshi door) |
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

### Author a slope tile (polygon collision)

Slope collision is defined on a tileset tile via an `<objectgroup>` containing one `<polygon>`. `TileMapCollisionGenerator` converts any such polygon to a local-space `Polygon` prototype (centered on the cell, Y-up) and emits it via `TileShapeCollection.AddPolygonTileAtCell` instead of a rect. For platformer floor slopes, set `SlopeMode = PlatformerFloor` on the **player's collision relationship** (not on the collection) — see the `collision-relationships` and `platformer-movement` skills.

```xml
<tile id="11" type="SolidCollision">
 <objectgroup draworder="index" id="2">
  <object id="1" x="0" y="0">
   <polygon points="0,0 16,16 0,16"/>
  </object>
 </objectgroup>
</tile>
```

Points are pixels in tile-local space, **Y-down** (Tiled's convention — origin at tile top-left, `y` increases downward). **FlatRedBall2 uses Y-up** in world space, so the two conventions disagree. You do NOT need to convert — `TileMapCollisionGenerator` does the Y-flip and cell-centering for you. Write polygons using Tiled's native coords. The example above is a lower-left triangle inside a 16×16 tile. `StandardTileset.tsx` ships with several slope tiles already authored this way (tile ids 11, 12, 13, 107).

**Polygon authoring — fill the cell, not just the surface.** A slope polygon needs the walking surface AND the solid mass below it, typically as a 4-point shape with the base on y=16 and a side on x=0 or x=16. Don't author a thin wedge triangle (e.g., `(0,12) (16,8) (0,16)`) — the walking edge `(0,12)→(16,8)` is correct but the hypotenuse back to the bottom-left leaves the bottom-right of the cell empty, so collision fails for anything approaching from below or from the right. For a gentle up-slope use a trapezoid like `(0,12) (16,8) (16,16) (0,16)`.

Tile flip flags (H, V, diagonal — the flip buttons in Tiled's editor, also applied when rotating a tile) are honored: a flipped or rotated slope tile emits the transformed polygon. You can author a single slope variant in the tileset and orient it freely in the map.

**Hand-authoring flipped tiles in CSV data** — Tiled's flip flags are packed into the high bits of the GID:

| Flip | Bit mask (hex) | Decimal |
|------|----------------|---------|
| Horizontal | `0x80000000` | `2147483648` |
| Vertical | `0x40000000` | `1073741824` |
| Diagonal | `0x20000000` | `536870912` |

OR the appropriate mask(s) with the base GID. Example: tile ID 11 (GID 12) flipped horizontally → `12 + 2147483648 = 2147483660`. If you author TMX through the Tiled editor, this encoding is automatic. If you hand-edit CSV data, you must OR the bits yourself.

### Sub-cell rectangles

A tileset tile can also carry one or more `<object>` elements with no child shape — plain Tiled rectangles. These emit as `AxisAlignedRectangle`s placed inside the cell (not the default full-cell rect). Use them for spikes, half-height platforms, or any box that doesn't fill the whole tile.

```xml
<tile id="20" type="SolidCollision">
 <objectgroup draworder="index" id="2">
  <object id="1" x="0" y="8" width="16" height="8"/>  <!-- bottom half of the tile -->
 </objectgroup>
</tile>
```

Multiple rects per tile are fine (e.g., two separate spike-rects). Rects and polygons can coexist on the same tile — both emit. Tile flip flags (H, V, diagonal) are honored for rects the same way they are for polygons.

Adjacent sub-cell rects participate in `RepositionDirections` seam suppression: if two sub-cell rects share an aligned, overlapping face (e.g., two bottom-half curbs in neighbor cells), or if a sub-cell rect's face aligns with a full-cell neighbor tile's face, the sub-cell rect's face is suppressed. When a sub-cell rect fully covers the adjacent full-cell tile's face (endpoints coincide), the matching face on the full-cell tile is suppressed as well, so a mover crossing the seam sees one clean surface. Partial coverage (e.g., a short spike next to a tall wall) leaves the full-cell face live so the exposed portion still repositions correctly. A sub-cell rect face that is fully covered by an axis-aligned polygon edge along the shared cell boundary (e.g., a slope polygon's vertical back-edge meeting a half-height rect) is also suppressed on the rect side so the mover doesn't snag at the seam; the polygon side is not modified.

**Current limitations:**
- `<ellipse>` and polyline collision objects are ignored — only `<polygon>` and plain `<object>` rectangles are honored.
- Only one `<polygon>` per tile is supported. Authoring a second polygon on the same tile throws `InvalidOperationException` at load time — merge the shapes into a single polygon in Tiled instead. (Rectangles have no such limit.)

### Add a tileset

Add before the first `<layer>`. The `firstgid` must not overlap with existing tilesets. StandardTileset has 1024 tiles (firstgid=1), so the next tileset should use `firstgid="1025"`.

```xml
<tileset firstgid="1025" source="MyArtTileset.tsx"/>
```
