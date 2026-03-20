using System;
using MonoGame.Extended.Tilemaps;
using FlatRedBall2.Collision;

namespace FlatRedBall2.Tiled;

/// <summary>
/// Generates a <see cref="TileShapeCollection"/> from a <see cref="TilemapTileLayer"/>
/// by matching tiles on their <see cref="TilemapTileData.Class"/> attribute or a custom property.
/// </summary>
public static class TileMapCollisionGenerator
{
    /// <summary>
    /// Scans every tile in <paramref name="layer"/> and adds a collision rectangle for each tile
    /// whose tileset <see cref="TilemapTileData.Class"/> equals <paramref name="className"/>
    /// (case-insensitive).
    /// </summary>
    public static TileShapeCollection GenerateFromClass(
        Tilemap tilemap,
        TilemapTileLayer layer,
        string className,
        float mapX = 0f,
        float mapY = 0f)
    {
        return Generate(tilemap, layer, mapX, mapY,
            tileData => string.Equals(tileData.Class, className, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Scans every tile in <paramref name="layer"/> and adds a collision rectangle for each tile
    /// whose tileset definition contains a custom property named <paramref name="propertyName"/>.
    /// </summary>
    public static TileShapeCollection GenerateFromProperty(
        Tilemap tilemap,
        TilemapTileLayer layer,
        string propertyName,
        float mapX = 0f,
        float mapY = 0f)
    {
        return Generate(tilemap, layer, mapX, mapY,
            tileData => tileData.Properties.TryGetValue(propertyName, out _));
    }

    private static TileShapeCollection Generate(
        Tilemap tilemap,
        TilemapTileLayer layer,
        float mapX,
        float mapY,
        Func<TilemapTileData, bool> predicate)
    {
        var collection = new TileShapeCollection
        {
            X = mapX,
            Y = mapY - layer.Height * tilemap.TileHeight,
            GridSize = tilemap.TileWidth
        };

        for (int row = 0; row < layer.Height; row++)
        {
            for (int col = 0; col < layer.Width; col++)
            {
                TilemapTile? tileNullable = layer.GetTile(col, row);
                if (!tileNullable.HasValue || tileNullable.Value.GlobalId == 0)
                    continue;

                TilemapTile tile = tileNullable.Value;

                TilemapTileData? tileData = tile.GetTileData(tilemap.Tilesets);
                if (tileData == null || !predicate(tileData))
                    continue;

                // Tiled is Y-down; TileShapeCollection is Y-up. Flip the row.
                int flippedRow = layer.Height - 1 - row;
                collection.AddTileAtCell(col, flippedRow);
            }
        }

        return collection;
    }
}
