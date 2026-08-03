using FlatRedBall2;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Strikers1945Sample.Screens;

/// <summary>
/// Renders an infinitely scrolling grid of tile sprites.
/// Tiles that scroll off the bottom are recycled to the top with new textures.
/// A density parameter controls what fraction of tiles are visible (rest show background color).
/// </summary>
public class ScrollingBackground
{
    private readonly Sprite[,] _tiles;
    private readonly Texture2D[] _tileTextures;
    private readonly int _cols;
    private readonly int _rows;
    private readonly float _tileSize;
    private readonly float _scrollSpeed;
    private readonly float _density;
    private readonly Random _random;

    private readonly float _screenBottom;
    private readonly float _screenTop;

    private float _topRowY;

    /// <param name="density">0.0–1.0: fraction of tiles that are visible. 1.0 = all tiles shown.</param>
    public ScrollingBackground(Screen screen, Texture2D[] tileTextures, float scrollSpeed = 70f, float tileScale = 2.5f, float density = 1f)
    {
        _scrollSpeed = scrollSpeed;
        _tileSize = 16f * tileScale;
        _tileTextures = tileTextures;
        _density = Math.Clamp(density, 0f, 1f);
        _random = new Random(42);

        var cam = screen.Camera;
        _cols = (int)MathF.Ceiling(cam.TargetWidth / _tileSize) + 1;
        _rows = (int)MathF.Ceiling(cam.TargetHeight / _tileSize) + 3;

        var gridLeft = -cam.TargetWidth / 2f;
        _screenTop = cam.TargetHeight / 2f;
        _screenBottom = -cam.TargetHeight / 2f;

        _tiles = new Sprite[_cols, _rows];
        _topRowY = _screenTop + _tileSize;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                var tex = tileTextures[_random.Next(tileTextures.Length)];
                var sprite = new Sprite
                {
                    Texture = tex,
                    TextureScale = tileScale,
                    Z = -10f,
                    IsVisible = _random.NextSingle() < _density,
                };
                sprite.X = gridLeft + x * _tileSize + _tileSize / 2f;
                sprite.Y = _topRowY - y * _tileSize;
                screen.Add(sprite);
                _tiles[x, y] = sprite;
            }
        }
    }

    public void Update(float deltaSeconds)
    {
        float moveAmount = _scrollSpeed * deltaSeconds;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                _tiles[x, y].Y -= moveAmount;
            }
        }

        _topRowY -= moveAmount;

        float recycleThreshold = _screenBottom - _tileSize;
        float newTopY = _topRowY + _tileSize;
        bool recycled = false;

        for (int x = 0; x < _cols; x++)
        {
            for (int y = 0; y < _rows; y++)
            {
                if (_tiles[x, y].Y < recycleThreshold)
                {
                    _tiles[x, y].Y = newTopY;
                    _tiles[x, y].Texture = _tileTextures[_random.Next(_tileTextures.Length)];
                    _tiles[x, y].IsVisible = _random.NextSingle() < _density;
                    recycled = true;
                }
            }
        }

        if (recycled)
        {
            _topRowY = newTopY;
        }
    }
}
