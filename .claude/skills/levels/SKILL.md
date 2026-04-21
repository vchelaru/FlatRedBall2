---
name: levels
description: "Level Data in FlatRedBall2. Use when working with level layouts, level progression, loading TMX maps, generating collision from tile layers, or transitioning between levels. Covers TMX-based level setup, TileShapeCollection generation, and level advancement patterns."
---

# Level Data in FlatRedBall2

Levels are defined as TMX files using the Tiled map format. Use the `tmx` skill to create or edit TMX files.

## Level Setup in a Screen

Load a TMX file with `TileMap`, add it to the screen, and generate collision:

```csharp
using FlatRedBall2.Tiled;

public class GameScreen : Screen
{
    private TileShapeCollection _solidCollision = null!;

    public override void CustomInitialize()
    {
        var map = new TileMap("Content/Tiled/Level1.tmx", Engine.GraphicsDevice);
        map.CenterOn(0, 0); // center map on world origin
        Add(map);

        _solidCollision = map.GenerateCollisionFromClass("SolidCollision");
        Add(_solidCollision);
    }
}
```

**Key types** (all in `FlatRedBall2.Tiled`):
- `TileMap` — loads a TMX file, wraps layers, generates collision
- `TileMapLayer` — per-layer Z, visibility, and render layer control
- `TileShapeCollection` — collision grid (`FlatRedBall2.Collision`)

## TileMap Position

`TileMap.X` and `TileMap.Y` define the **top-left corner** of the map (Tiled convention). Default (0, 0) places the top-left at the world origin; the map extends right (+X) and down (−Y).

`CenterOn(worldX, worldY)` repositions the map so its center is at the given point.

## Layer Z-Order

Layers are assigned Z values automatically: 1 apart in TMX order, with `GameplayLayer` at Z = 0 if it exists. Entities at Z = 0 naturally interleave at the gameplay layer. Override per-layer Z only when needed:

```csharp
map.GetLayer("Foreground").Z = 100f;
```

## Multiple Collision Types

```csharp
var solid = map.GenerateCollisionFromClass("SolidCollision");
var jumpThrough = map.GenerateCollisionFromClass("JumpThroughCollision");
Add(solid);
Add(jumpThrough);
```

Each collection can have its own collision relationship. For jump-through platforms, set `OneWayDirection = OneWayDirection.Up` and `AllowDropThrough = true` on the relationship — the player passes through from below, lands from above, and can drop down with Down+Jump. For hard one-way barriers (`OneWayCollision` tile class — Yoshi-style ratchet doors), set `OneWayDirection = OneWayDirection.Up` and leave `AllowDropThrough = false`. See the `collision-relationships` skill for details. By default all tile layers are scanned; restrict to a specific layer with the optional `layerName` parameter:

```csharp
var solid = map.GenerateCollisionFromClass("SolidCollision", layerName: "GameplayLayer");
```

## Slope Tiles and Sub-Cell Shapes

Tiles in the tileset can declare custom collision via an `<objectgroup>` containing polygons and/or plain `<object>` rectangles. `GenerateCollisionFromClass` emits polygons as `Polygon` tiles and rectangles as sub-cell `AxisAlignedRectangle`s, instead of the default full-cell rect. Polygons and rects can coexist on the same tile. For platformer floors, set `SlopeMode = PlatformerFloor` on the **collision relationship** (not on the collection) so vertical separation uses a heightmap instead of SAT:

```csharp
var solid = map.GenerateCollisionFromClass("SolidCollision");
Add(solid);

var playerVsSolid = AddCollisionRelationship(_playerFactory, solid);
playerVsSolid.SlopeMode = SlopeCollisionMode.PlatformerFloor;
playerVsSolid.BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);
```

`SlopeMode` is a per-relationship concern: the same `solid` collection can simultaneously back a player relationship (`PlatformerFloor`) and a ball relationship (default `Standard` SAT) without conflict.

See the `tmx` skill for how to author the polygon on the tileset tile.

## Spawning Entities from Object Layers or Painted Tile Layers

`CreateEntities` scans both **object layers** (for precisely-placed tile objects) and **regular tile layers** (for tiles painted with Tiled's brush). Any tile whose Class matches the requested name becomes one entity — use whichever authoring path fits the designer's workflow. Painted tile layers have no per-cell custom properties; reflection-based property mapping is a no-op for that path.

The Class-name convention disambiguates intent via *which method you call*: `GenerateCollisionFromClass("SolidCollision")` turns matching tiles into static `TileShapeCollection` geometry; `CreateEntities("Coin", factory)` turns matching tiles into factory-spawned entities. The two methods never fight over the same tile — pick one per Class.

Place tile objects on Tiled object layers (using tiles with a Class set in the tileset). Game code spawns entities from them with `CreateEntities`:

```csharp
// Coins — center origin (default)
map.CreateEntities("Coin", _coinFactory);

// Player — feet at bottom of tile
var player = map.CreateEntities("PlayerSpawn", _playerFactory, Origin.BottomCenter)[0];

// Ceiling turret — top of tile
map.CreateEntities("CeilingTurret", _turretFactory, Origin.TopCenter);
```

The engine converts Tiled's pixel coordinates (Y-down) to world space (Y-up) and sets each spawned entity's `X`/`Y` to the world-space position of the tile object, adjusted by the `Origin` parameter.

**Source tiles are removed by default.** After spawning, `CreateEntities` clears the painted cell and removes the tile-object from its object layer so the source tile doesn't double-draw under the spawned entity. The mutation is in-memory only — the `.tmx` file is untouched, and a hot-reload repopulates the source tiles so the next spawn pass picks them up again. Pass `removeSourceTiles: false` to keep the source tile visible (e.g., the tile doubles as intentional background art). This is the opposite default from `GenerateCollisionFromClass`, which leaves source tiles visible because there the tile *is* the visual.

**Custom properties** set on tile objects in Tiled are automatically applied to matching entity properties via reflection. If a Coin entity has `public int Worth { get; set; }` and the Tiled object has a custom property `Worth=50`, it's set automatically. Supported types: `string`, `int`, `float`, `bool`.

**Class matching** checks the object's Class first, then falls back to the tile definition's Class in the tileset. Case-insensitive.

## Camera Bounds

`TileMap.Bounds` returns a `BoundsRectangle` for `CameraControllingEntity.Map`:

```csharp
cam.Map = map.Bounds;
```

## Map Dimensions

```csharp
map.Width       // total width in world units
map.Height      // total height in world units
map.TileWidth   // single tile width
map.TileHeight  // single tile height
```

## Rooms as Separate Screens

Each distinct room or area is its own `Screen` subclass. There is no built-in "room manager" — `MoveToScreen<T>` is the room transition mechanism. Room state is passed via the configure callback:

```csharp
MoveToScreen<Room2Screen>(s => s.RoomState = _sharedState);
```

## Level Advancement

Pass the next level index when transitioning screens:

```csharp
if (levelComplete)
{
    int next = LevelIndex + 1;
    if (next < TotalLevels)
        MoveToScreen<GameScreen>(s => s.LevelIndex = next);
    else
        MoveToScreen<GameOverScreen>(s => s.Win = true);
}
```
