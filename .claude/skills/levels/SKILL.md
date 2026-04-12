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
var cloud = map.GenerateCollisionFromClass("CloudCollision");
Add(solid);
Add(cloud);
```

Each collection can have its own collision relationship (solid blocks movement, cloud allows jump-through). By default all tile layers are scanned; restrict to a specific layer with the optional `layerName` parameter:

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
