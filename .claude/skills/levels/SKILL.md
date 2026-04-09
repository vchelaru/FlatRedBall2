---
name: levels
description: "Level Data in FlatRedBall2. Use when working with level layouts, level progression, loading TMX maps, generating collision from tile layers, or transitioning between levels. Covers TMX-based level setup, TileShapeCollection generation, and level advancement patterns."
---

# Level Data in FlatRedBall2

Levels are defined as TMX files using the Tiled map format. Use the `tmx` skill to create or edit TMX files.

## Level Setup in a Screen

Parse the TMX file, set up rendering for visual layers, and generate collision from the GameplayLayer:

```csharp
public class GameScreen : Screen
{
    private TileShapeCollection _solidCollision;

    public override void CustomInitialize()
    {
        var parser = new TiledTmxParser();
        var tilemap = parser.ParseFromFile("Content/Tiled/Level1.tmx", Engine.GraphicsDevice);

        var renderer = new TilemapSpriteBatchRenderer();
        renderer.LoadTilemap(tilemap);

        // Place map so its center aligns with the world origin.
        float mapX = -(float)tilemap.WorldBounds.Width / 2f;
        float mapY = (float)tilemap.WorldBounds.Height / 2f;

        foreach (var layer in tilemap.Layers)
        {
            if (layer is TilemapTileLayer tileLayer)
            {
                // Render all visual layers.
                var renderable = new TileMapLayerRenderable(renderer, tileLayer)
                {
                    X = mapX,
                    Y = mapY,
                };
                Add(renderable);

                // Generate collision from the GameplayLayer.
                if (tileLayer.Name == "GameplayLayer")
                {
                    _solidCollision = TileMapCollisionGenerator.GenerateFromClass(
                        tilemap, tileLayer, "SolidCollision", mapX, mapY);
                    Add(_solidCollision);
                }
            }
        }
    }
}
```

Key types and their namespaces:
- `TiledTmxParser` — `MonoGame.Extended.Tilemaps.Tiled`
- `TilemapSpriteBatchRenderer` — `MonoGame.Extended.Tilemaps.Rendering`
- `TilemapTileLayer`, `Tilemap` — `MonoGame.Extended.Tilemaps`
- `TileMapLayerRenderable`, `TileMapCollisionGenerator` — `FlatRedBall2.Tiled`

`GenerateFromClass` matches tiles whose `type` attribute equals the class name (case-insensitive). Use `GenerateFromProperty` to match on a custom property instead.

## Multiple Collision Types

Generate a separate `TileShapeCollection` for each type inside the GameplayLayer loop:

```csharp
if (tileLayer.Name == "GameplayLayer")
{
    _solidCollision = TileMapCollisionGenerator.GenerateFromClass(
        tilemap, tileLayer, "SolidCollision", mapX, mapY);
    _cloudCollision = TileMapCollisionGenerator.GenerateFromClass(
        tilemap, tileLayer, "CloudCollision", mapX, mapY);
    Add(_solidCollision);
    Add(_cloudCollision);
}
```

Each collection can then have its own collision relationship with the player (e.g., solid blocks movement, cloud allows jump-through).

## Rooms as Separate Screens

Each distinct room or area is its own `Screen` subclass. There is no built-in "room manager" — `MoveToScreen<T>` is the room transition mechanism. Room state (which enemies are alive, whether the exit is unlocked) is passed via the configure callback:

```csharp
MoveToScreen<Room2Screen>(s => s.RoomState = _sharedState);
```

This is simpler than managing multiple rooms within a single screen and composes naturally with the screen lifecycle (automatic entity/factory cleanup, `CustomInitialize` for fresh setup).

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
