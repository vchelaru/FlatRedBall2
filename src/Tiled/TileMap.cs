using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Tilemaps;
using MonoGame.Extended.Tilemaps.Rendering;
using MonoGame.Extended.Tilemaps.Tiled;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;

namespace FlatRedBall2.Tiled;

/// <summary>
/// A loaded Tiled map positioned in world space. Wraps MonoGame.Extended's tilemap parsing
/// and rendering so game code never touches those types directly.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="X"/> and <see cref="Y"/> define the <b>top-left corner</b> of the map
/// (Tiled convention, matching FRB1). The default position (0, 0) places the top-left
/// at the world origin; the map extends right (+X) and down (−Y in Y-up engine space).
/// </para>
/// <para>
/// Layers are assigned Z values automatically: 1 apart in TMX order, with the layer
/// named "GameplayLayer" at Z = 0 if it exists. This means entities at Z = 0 naturally
/// interleave at the gameplay layer without manual Z assignment.
/// </para>
/// </remarks>
public class TileMap
{
    private readonly Tilemap _tilemap;
    private readonly TilemapSpriteBatchRenderer _renderer;
    private readonly List<TileMapLayer> _layers;
    private readonly Dictionary<string, TileMapLayer> _layersByName;
    private readonly float _width;
    private readonly float _height;
    private readonly int _tileWidth;
    private readonly int _tileHeight;
    private float _x;
    private float _y;

    /// <summary>
    /// Loads a TMX file and positions the map in world space.
    /// </summary>
    /// <param name="tmxPath">Path to the .tmx file (e.g., "Content/Tiled/Level1.tmx").</param>
    /// <param name="graphicsDevice">The graphics device — pass <c>Engine.GraphicsDevice</c>.</param>
    /// <param name="x">Left edge of the map in world space. Default 0.</param>
    /// <param name="y">Top edge of the map in world space (Tiled convention). Default 0.</param>
    public TileMap(string tmxPath, GraphicsDevice graphicsDevice, float x = 0f, float y = 0f)
    {
        var parser = new TiledTmxParser();
        _tilemap = parser.ParseFromFile(tmxPath, graphicsDevice);

        _width = _tilemap.Width * _tilemap.TileWidth;
        _height = _tilemap.Height * _tilemap.TileHeight;
        _tileWidth = _tilemap.TileWidth;
        _tileHeight = _tilemap.TileHeight;

        _renderer = new TilemapSpriteBatchRenderer();
        _renderer.LoadTilemap(_tilemap);

        _layers = new List<TileMapLayer>();
        _layersByName = new Dictionary<string, TileMapLayer>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in _tilemap.Layers)
        {
            if (layer is TilemapTileLayer tileLayer)
            {
                var renderable = new TileMapLayerRenderable(_renderer, tileLayer);
                var mapLayer = new TileMapLayer(tileLayer.Name, renderable);
                _layers.Add(mapLayer);
                _layersByName[tileLayer.Name] = mapLayer;
            }
        }

        AssignDefaultZ();

        // Set position after layers are created so propagation works.
        _x = x;
        _y = y;
        PropagatePosition();
    }

    /// <summary>
    /// Internal constructor for unit testing — creates a TileMap without loading a TMX file.
    /// </summary>
    internal TileMap(float width, float height, int tileWidth, int tileHeight,
        List<TileMapLayer> layers, float x = 0f, float y = 0f)
    {
        _tilemap = null!;
        _renderer = null!;
        _width = width;
        _height = height;
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;
        _layers = layers;
        _layersByName = new Dictionary<string, TileMapLayer>(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in layers)
            _layersByName[layer.Name] = layer;

        AssignDefaultZ();
        _x = x;
        _y = y;
        PropagatePosition();
    }

    /// <summary>Left edge of the map in world space. Setting this repositions all layers.</summary>
    public float X
    {
        get => _x;
        set
        {
            _x = value;
            PropagatePosition();
        }
    }

    /// <summary>
    /// Top edge of the map in world space (Tiled convention — the map extends downward
    /// into negative Y). Setting this repositions all layers.
    /// </summary>
    public float Y
    {
        get => _y;
        set
        {
            _y = value;
            PropagatePosition();
        }
    }

    /// <summary>Map width in world units (tile columns × tile width).</summary>
    public float Width => _width;

    /// <summary>Map height in world units (tile rows × tile height).</summary>
    public float Height => _height;

    /// <summary>Width of a single tile in world units.</summary>
    public int TileWidth => _tileWidth;

    /// <summary>Height of a single tile in world units.</summary>
    public int TileHeight => _tileHeight;

    /// <summary>
    /// Returns a <see cref="BoundsRectangle"/> suitable for
    /// <see cref="Entities.CameraControllingEntity.Map"/>.
    /// Computed from the current <see cref="X"/>, <see cref="Y"/>,
    /// <see cref="Width"/>, and <see cref="Height"/>.
    /// </summary>
    public BoundsRectangle Bounds => new(
        X + Width / 2f,
        Y - Height / 2f,
        Width,
        Height);

    /// <summary>All tile layers in TMX order.</summary>
    public IReadOnlyList<TileMapLayer> Layers => _layers;

    /// <summary>
    /// Gets a layer by name (case-insensitive).
    /// </summary>
    /// <exception cref="KeyNotFoundException">No tile layer with that name exists.</exception>
    public TileMapLayer GetLayer(string name) => _layersByName[name];

    /// <summary>
    /// Tries to get a layer by name (case-insensitive).
    /// </summary>
    public bool TryGetLayer(string name, out TileMapLayer layer) =>
        _layersByName.TryGetValue(name, out layer!);

    /// <summary>
    /// Generates a <see cref="TileShapeCollection"/> from tiles whose
    /// <see cref="TilemapTileData.Class"/> matches <paramref name="className"/>.
    /// </summary>
    /// <param name="className">The tile class to match (case-insensitive).</param>
    /// <param name="layerName">
    /// If specified, restricts the search to this layer. If <c>null</c> (default),
    /// scans all tile layers.
    /// </param>
    public TileShapeCollection GenerateCollisionFromClass(string className, string? layerName = null)
    {
        if (layerName != null)
        {
            var layer = GetInternalLayer(layerName);
            return TileMapCollisionGenerator.GenerateFromClass(_tilemap, layer, className, _x, _y);
        }

        return TileMapCollisionGenerator.GenerateFromClass(_tilemap, className, _x, _y);
    }

    /// <summary>
    /// Generates a <see cref="TileShapeCollection"/> from tiles that have a custom
    /// property named <paramref name="propertyName"/>.
    /// </summary>
    /// <param name="propertyName">The custom property to match (presence only, value ignored).</param>
    /// <param name="layerName">
    /// If specified, restricts the search to this layer. If <c>null</c> (default),
    /// scans all tile layers.
    /// </param>
    public TileShapeCollection GenerateCollisionFromProperty(string propertyName, string? layerName = null)
    {
        if (layerName != null)
        {
            var layer = GetInternalLayer(layerName);
            return TileMapCollisionGenerator.GenerateFromProperty(_tilemap, layer, propertyName, _x, _y);
        }

        return TileMapCollisionGenerator.GenerateFromProperty(_tilemap, propertyName, _x, _y);
    }

    /// <summary>
    /// Repositions the map so its center aligns with the given world-space point.
    /// </summary>
    public void CenterOn(float worldX, float worldY)
    {
        X = worldX - Width / 2f;
        Y = worldY + Height / 2f;
    }

    private TilemapTileLayer GetInternalLayer(string name)
    {
        foreach (var layer in _tilemap.Layers)
        {
            if (layer is TilemapTileLayer tileLayer &&
                string.Equals(tileLayer.Name, name, StringComparison.OrdinalIgnoreCase))
                return tileLayer;
        }

        throw new KeyNotFoundException($"No tile layer named '{name}' exists in this map.");
    }

    private void AssignDefaultZ()
    {
        int gameplayIndex = -1;

        for (int i = 0; i < _layers.Count; i++)
        {
            if (string.Equals(_layers[i].Name, "GameplayLayer", StringComparison.OrdinalIgnoreCase))
            {
                gameplayIndex = i;
                break;
            }
        }

        float offset = gameplayIndex >= 0 ? -gameplayIndex : 0f;

        for (int i = 0; i < _layers.Count; i++)
            _layers[i].Z = i + offset;
    }

    private void PropagatePosition()
    {
        foreach (var layer in _layers)
        {
            if (layer.Renderable == null) continue;
            layer.Renderable.X = _x;
            layer.Renderable.Y = _y;
        }
    }
}
