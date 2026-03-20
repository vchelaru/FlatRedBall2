using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Tilemaps;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Tiled;

/// <summary>
/// Renders a single <see cref="TilemapTileLayer"/> from a <see cref="Tilemap"/>,
/// integrating with FlatRedBall2's Z-ordered rendering pipeline.
/// </summary>
public class TilemapLayerRenderable : IRenderable, IAttachable
{
    private readonly Tilemap _tilemap;
    private readonly TilemapTileLayer _layer;

    public TilemapLayerRenderable(Tilemap tilemap, TilemapTileLayer layer)
    {
        _tilemap = tilemap;
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

        int tileWidth = _tilemap.TileWidth;
        int tileHeight = _tilemap.TileHeight;

        for (int row = 0; row < _layer.Height; row++)
        {
            for (int col = 0; col < _layer.Width; col++)
            {
                TilemapTile? tileNullable = _layer.GetTile(col, row);
                if (!tileNullable.HasValue || tileNullable.Value.GlobalId == 0)
                    continue;

                TilemapTile tile = tileNullable.Value;
                TilemapTileset tileset = tile.GetTileset(_tilemap.Tilesets);
                int localId = tile.GetLocalId(_tilemap.Tilesets);
                Rectangle sourceRect = tileset.GetTileRegion(localId);

                float worldX = AbsoluteX + col * tileWidth + tileWidth / 2f;
                float worldY = AbsoluteY - row * tileHeight - tileHeight / 2f;
                var position = new Vector2(worldX, worldY);
                var origin = new Vector2(tileset.TileWidth / 2f, tileset.TileHeight / 2f);

                // FlipVertically compensates WorldSpaceBatch's Y-flip so the texture appears right-side-up.
                var effects = SpriteEffects.FlipVertically;
                if ((tile.FlipFlags & TilemapTileFlipFlags.FlipHorizontally) != 0)
                    effects ^= SpriteEffects.FlipHorizontally;
                if ((tile.FlipFlags & TilemapTileFlipFlags.FlipVertically) != 0)
                    effects ^= SpriteEffects.FlipVertically;

                float rotation = 0f;
                if ((tile.FlipFlags & TilemapTileFlipFlags.FlipDiagonally) != 0)
                    rotation = MathHelper.PiOver2;

                spriteBatch.Draw(tileset.Texture, position, sourceRect, Color, rotation, origin, 1f, effects, 0f);
            }
        }
    }
}
