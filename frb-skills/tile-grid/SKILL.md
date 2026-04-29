---
name: tile-grid
description: "Programmatic tile grid in FlatRedBall2. Use when building a code-driven grid at runtime (city builders, dungeon generators, puzzle games, cellular automata) — NOT loading a TMX map from Tiled. Covers the data/visual split, world↔tile coordinate conversion, per-tile visual spawning, and performance guidance for large grids (128×128+). Trigger on any question about tile coordinates, world-to-tile math, programmatic maps, or large grids without TMX files."
---

# Programmatic Tile Grid

Use this skill when the grid is created at runtime in C#, not loaded from a `.tmx` file. Typical cases: city builders, procedural dungeons, puzzle games, cellular automata.

If you have a designer-authored level in Tiled, use the **levels** and **tmx** skills instead.

---

## Data / Visual Split (Required Pattern)

Keep the authoritative state in a C# array and the visuals as a separate layer. Never derive game logic from what's drawn on screen.

```csharp
// Data layer — the truth:
public enum TileType { Empty, Road, ZoneR, ZoneC, ZoneI, PowerPlant, WaterTower }

private TileType[,] _tiles;        // [col, row] indexed
private int _cols, _rows;
private float _tileSize;
private Vector2 _gridOrigin;       // world position of tile (0,0) center

// Visual layer — one ColoredRectangleRuntime per visible tile:
private ColoredRectangleRuntime?[,] _visuals;
```

Always update `_tiles` first, then sync the visual. Never infer tile type from the visual's color.

---

## Grid Setup

```csharp
_cols       = 128;
_rows       = 128;
_tileSize   = 16f;
// Center the grid at world origin:
_gridOrigin = new Vector2(-(_cols * _tileSize) / 2f + _tileSize / 2f,
                          -(_rows * _tileSize) / 2f + _tileSize / 2f);

_tiles   = new TileType[_cols, _rows];
_visuals = new ColoredRectangleRuntime[_cols, _rows];
```

`_gridOrigin` is the **center** of tile (0,0). Add `col * _tileSize` / `row * _tileSize` to get centers of other tiles.

---

## World ↔ Tile Coordinate Conversion

This is the most-called math in any tile game. Use `Math.Floor` — not `(int)` cast — to handle negative world coordinates correctly.

```csharp
// World position → tile indices (returns false if out of bounds):
bool WorldToTile(Vector2 worldPos, out int col, out int row)
{
    col = (int)Math.Floor((worldPos.X - _gridOrigin.X + _tileSize / 2f) / _tileSize);
    row = (int)Math.Floor((worldPos.Y - _gridOrigin.Y + _tileSize / 2f) / _tileSize);
    return col >= 0 && col < _cols && row >= 0 && row < _rows;
}

// Tile indices → world center position:
Vector2 TileToWorld(int col, int row)
    => new Vector2(_gridOrigin.X + col * _tileSize,
                   _gridOrigin.Y + row * _tileSize);
```

> **Gotcha:** `(int)(-0.3f)` rounds toward zero and gives `0`, but the tile at world X = -0.3 is tile `-1`. Always use `Math.Floor`.

---

## Spawning Tile Visuals

For grids up to ~4,000 tiles, spawn one `ColoredRectangleRuntime` per tile in Gum (screen-space; camera does not move these automatically). This is **Option B (canvas-space overlay)** and is separate from the world-space path described in the World ↔ Tile section above. It uses a dedicated `_uiGridOrigin` in canvas-space:

```csharp
private readonly Vector2 _uiGridOrigin = Vector2.Zero;

void SpawnVisual(int col, int row)
{
    var rect = new ColoredRectangleRuntime
    {
        Width  = _tileSize,
        Height = _tileSize,
    };
    // This snippet is canvas-space only (UI overlay grid).
    Add(rect); // screen-space — anchored to Gum canvas
    _visuals[col, row] = rect;
    SyncVisual(col, row);
}

void SyncVisual(int col, int row)
{
    var rect = _visuals[col, row];
    if (rect == null) return;
    // Canvas-space placement for Gum visuals.
    rect.X = _uiGridOrigin.X + col * _tileSize;
    rect.Y = _uiGridOrigin.Y + row * _tileSize;
    rect.Color = TileColor(_tiles[col, row]);
}
```

For very large grids (16,000+ tiles), spawn only the tiles visible in the canvas viewport and re-cull when your UI scroll offset changes. At 128×128 = 16,384 tiles, spawning all at once is slow but acceptable at startup (~500ms). Profile before optimizing.

If your grid lives in world-space instead, prefer world renderables (`AARect`/`Sprite`) so camera transforms are automatic.

---

## Updating a Tile

Always go data-first, then sync visual:

```csharp
void SetTile(int col, int row, TileType type)
{
    if (col < 0 || col >= _cols || row < 0 || row >= _rows) return;
    _tiles[col, row] = type;
    SyncVisual(col, row);
}
```

---

## Adjacency / Neighbor Iteration

```csharp
// 4-way neighbors (N/S/E/W):
static readonly (int dc, int dr)[] Cardinal = [(0,1),(0,-1),(1,0),(-1,0)];

bool HasRoadAccess(int col, int row)
{
    foreach (var (dc, dr) in Cardinal)
    {
        int nc = col + dc, nr = row + dr;
        if (nc >= 0 && nc < _cols && nr >= 0 && nr < _rows)
            if (_tiles[nc, nr] == TileType.Road)
                return true;
    }
    return false;
}
```

---

## Radius Queries (Power, Water Coverage)

```csharp
bool IsInRadius(int srcCol, int srcRow, int targetCol, int targetRow, float radius)
{
    float dx = (targetCol - srcCol) * _tileSize;
    float dy = (targetRow - srcRow) * _tileSize;
    return dx * dx + dy * dy <= radius * radius;
}
```

Use integer math (column/row distances) when possible — it avoids the square root and is significantly faster for large grids.

---

## Performance Notes

- **16,384 tiles (128×128):** Initial spawn is slow (~300–600ms for `ColoredRectangleRuntime` objects). Acceptable for city builders. If it's a problem, spawn only the visible viewport and lazy-spawn as camera pans.
- **Per-tick full-grid scan:** Scanning all 16,384 tiles each sim tick is fast (< 1ms). Do not premature-optimize. Only cache derived data (e.g., "powered tiles" bitset) if profiling reveals it's a bottleneck.
- **Do not use `TileShapes` for visual-only grids.** `TileShapes` is a collision structure, not a rendering structure. Use `ColoredRectangleRuntime` for programmer-art visual tiles.
- **Entity-per-tile is expensive at this scale.** Avoid spawning a full `Entity` subclass for each tile — the per-entity overhead (factory registration, physics update, etc.) at 16k tiles adds up. Use lightweight Gum visuals or `ColoredRectangleRuntime` instead.
