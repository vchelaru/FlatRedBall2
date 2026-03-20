using MonoGame.Extended.Tiled;
using FlatRedBall2.Collision;

namespace FlatRedBall2.Tiled;

public static class TiledCollisionGenerator
{
    public static TileShapeCollection Generate(
        TiledMap tiledMap,
        TiledMapTileLayer layer,
        string propertyName,
        float mapX = 0f,
        float mapY = 0f)
    {
        var collection = new TileShapeCollection
        {
            X = mapX,
            Y = mapY - layer.Height * tiledMap.TileHeight,
            GridSize = tiledMap.TileWidth
        };

        for (ushort row = 0; row < layer.Height; row++)
        {
            for (ushort col = 0; col < layer.Width; col++)
            {
                if (!layer.TryGetTile(col, row, out TiledMapTile? tileNullable) || !tileNullable.HasValue || tileNullable.Value.IsBlank)
                    continue;

                TiledMapTile tile = tileNullable.Value;
                TiledMapTileset tileset = tiledMap.GetTilesetByTileGlobalIdentifier(tile.GlobalIdentifier);

                int firstGid = tiledMap.GetTilesetFirstGlobalIdentifier(tileset);
                int localId = (int)tile.GlobalIdentifier - firstGid;

                TiledMapTilesetTile? tilesetTile = null;
                foreach (var t in tileset.Tiles)
                {
                    if (t.LocalTileIdentifier == localId)
                    {
                        tilesetTile = t;
                        break;
                    }
                }

                if (tilesetTile == null || !tilesetTile.Properties.ContainsKey(propertyName))
                    continue;

                // Tiled is Y-down; TileShapeCollection is Y-up. Flip the row.
                int flippedRow = layer.Height - 1 - row;
                collection.AddTileAtCell(col, flippedRow);
            }
        }

        return collection;
    }
}
