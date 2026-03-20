using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Tiled;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Tiled;

public class TiledMapLayerRenderable : IRenderable, IAttachable
{
    private readonly TiledMap _tiledMap;
    private readonly TiledMapTileLayer _layer;

    public TiledMapLayerRenderable(TiledMap tiledMap, TiledMapTileLayer layer)
    {
        _tiledMap = tiledMap;
        _layer = layer;
    }

    public bool IsVisible { get; set; } = true;
    public Color Color { get; set; } = Color.White;

    // IAttachable
    public Entity? Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;
    public void Destroy() { }

    // IRenderable
    public float Z { get; set; }
    public Layer Layer { get; set; } = null!;
    public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
    public string? Name { get; set; }

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible) return;

        int tileWidth = _tiledMap.TileWidth;
        int tileHeight = _tiledMap.TileHeight;

        for (ushort row = 0; row < _layer.Height; row++)
        {
            for (ushort col = 0; col < _layer.Width; col++)
            {
                if (!_layer.TryGetTile(col, row, out TiledMapTile? tileNullable) || !tileNullable.HasValue || tileNullable.Value.IsBlank)
                    continue;

                TiledMapTile tile = tileNullable.Value;
                TiledMapTileset tileset = _tiledMap.GetTilesetByTileGlobalIdentifier(tile.GlobalIdentifier);

                int firstGid = _tiledMap.GetTilesetFirstGlobalIdentifier(tileset);
                int localId = (int)tile.GlobalIdentifier - firstGid;
                int columns = tileset.Texture.Width / tileset.TileWidth;
                int srcCol = localId % columns;
                int srcRow = localId / columns;
                var sourceRect = new Rectangle(srcCol * tileset.TileWidth, srcRow * tileset.TileHeight, tileset.TileWidth, tileset.TileHeight);

                float worldX = AbsoluteX + col * tileWidth + tileWidth / 2f;
                float worldY = AbsoluteY - row * tileHeight - tileHeight / 2f;
                var position = new Vector2(worldX, worldY);
                var origin = new Vector2(tileset.TileWidth / 2f, tileset.TileHeight / 2f);

                // FlipVertically compensates WorldSpaceBatch's Y-flip so the texture appears right-side-up.
                var effects = SpriteEffects.FlipVertically;
                if (tile.IsFlippedHorizontally) effects ^= SpriteEffects.FlipHorizontally;
                if (tile.IsFlippedVertically) effects ^= SpriteEffects.FlipVertically;

                // TODO: IsFlippedDiagonally requires a 90-degree rotation — not yet supported; drawn unrotated.

                spriteBatch.Draw(tileset.Texture, position, sourceRect, Color, 0f, origin, 1f, effects, 0f);
            }
        }
    }
}
