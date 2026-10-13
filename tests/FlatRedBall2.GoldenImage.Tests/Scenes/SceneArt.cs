using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.GoldenImage.Tests.Scenes;

/// <summary>
/// Textures generated in code so goldens depend on no content files and no asset pipeline. Each
/// has an asymmetric marker so a flip, rotation or Y inversion shows up in the picture.
/// </summary>
internal static class SceneArt
{
    private static Texture2D FromPixels(GraphicsDevice device, int width, int height, Color[] pixels)
    {
        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>16x16: red top-left, green top-right, blue bottom-left, yellow bottom-right, white 3x3 marker in the top-left corner.</summary>
    public static Texture2D Quadrants(GraphicsDevice device)
    {
        const int size = 16;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool right = x >= size / 2;
                bool bottom = y >= size / 2;
                pixels[y * size + x] = (right, bottom) switch
                {
                    (false, false) => new Color(220, 40, 40),
                    (true, false) => new Color(40, 200, 60),
                    (false, true) => new Color(50, 90, 230),
                    _ => new Color(240, 210, 50),
                };
                if (x < 3 && y < 3) pixels[y * size + x] = Color.White;
            }
        }
        return FromPixels(device, size, size, pixels);
    }

    public const int TileSize = 16;

    /// <summary>
    /// 64x16 tileset strip of four 16-pixel tiles: 0 grass, 1 dirt, 2 water, 3 a red marker tile
    /// with a white top-left square (so flips are visible).
    /// </summary>
    public static Texture2D TileStrip(GraphicsDevice device)
    {
        const int tiles = 4;
        const int width = TileSize * tiles;
        var pixels = new Color[width * TileSize];
        for (int tile = 0; tile < tiles; tile++)
        {
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    Color c = tile switch
                    {
                        0 => (x * 7 + y * 13) % 11 == 0 ? new Color(110, 200, 90) : new Color(60, 150, 60),
                        1 => (x + y) % 6 == 0 ? new Color(90, 60, 35) : new Color(150, 105, 60),
                        2 => y == 4 || y == 11 ? Color.White : new Color(50, 90, 200),
                        _ => x < 5 && y < 5 ? Color.White : new Color(200, 40, 50),
                    };
                    // 1px darker outline so tile boundaries (and any seams) are visible.
                    if (x == 0 || y == 0 || x == TileSize - 1 || y == TileSize - 1)
                        c = new Color(c.R / 2, c.G / 2, c.B / 2);
                    pixels[y * width + tile * TileSize + x] = c;
                }
            }
        }
        return FromPixels(device, width, TileSize, pixels);
    }
}
