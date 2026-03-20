using MonoGame.Extended.Tilemaps;
using FlatRedBall2.Collision;

namespace FlatRedBall2.Tiled;

/// <summary>
/// Generates a <see cref="TileShapeCollection"/> from a <see cref="TilemapTileLayer"/>
/// by inspecting per-tile custom properties in the tileset.
/// </summary>
public static class TilemapCollisionGenerator
{
    /// <summary>
    /// Scans every tile in <paramref name="layer"/> and adds a collision cell for each tile
    /// whose tileset definition contains a custom property named <paramref name="propertyName"/>.
    /// </summary>
    public static TileShapeCollection Generate(
        Tilemap tilemap,
        TilemapTileLayer layer,
        string propertyName,
        float mapX = 0f,
        float mapY = 0f)
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

                // Look up per-tile metadata in the tileset for the custom property.
                TilemapTileData? tileData = tile.GetTileData(tilemap.Tilesets);
                if (tileData == null || !tileData.Properties.TryGetValue(propertyName, out _))
                    continue;

                // Tiled is Y-down; TileShapeCollection is Y-up. Flip the row.
                int flippedRow = layer.Height - 1 - row;
                collection.AddTileAtCell(col, flippedRow);
            }
        }

        return collection;
    }
}
