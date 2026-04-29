using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using XnaTitleContainer = Microsoft.Xna.Framework.TitleContainer;
using MonoGame.Extended.Content;
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
    private readonly GraphicsDevice? _graphicsDevice;
    private readonly List<TileMapLayer> _layers;
    private readonly Dictionary<string, TileMapLayer> _layersByName;
    private readonly float _width;
    private readonly float _height;
    private readonly int _tileWidth;
    private readonly int _tileHeight;
    private float _x;
    private float _y;

    // Test seam — and the layer that routes TMX reads through TitleContainer instead of File.IO,
    // so the same code path works on backends without a filesystem (KNI Blazor / WASM).
    // TitleContainer.OpenStream resolves relative paths against the title location on every
    // backend: the working directory on DesktopGL, an HTTP fetch on Blazor.
    internal static Func<string, GraphicsDevice, Tilemap> TmxLoader { get; set; } = DefaultTmxLoader;

    private static Tilemap DefaultTmxLoader(string tmxPath, GraphicsDevice graphicsDevice)
    {
        // basePath is what the parser uses to resolve referenced TSX/PNG files. We hand it
        // the directory portion of the TMX path so child references resolve relative to the
        // TMX, matching ParseFromFile's behavior.
        string basePath = System.IO.Path.GetDirectoryName(tmxPath) ?? string.Empty;
        var parser = new TiledTmxParser(
            baseDirectory: basePath,
            resourceResolver: ExternalResourceResolvers.OpenTitleContainerStream);
        using var stream = XnaTitleContainer.OpenStream(tmxPath);
        return parser.ParseFromStream(stream, graphicsDevice, basePath);
    }

    // Tracked TSCs registered by GenerateCollisionFromClass / GenerateCollisionFromProperty.
    // On TryReload, each is cleared and rebuilt against the updated tilemap so cell membership
    // reflects the new tile data without the caller needing to rewire collision relationships.
    private readonly List<TrackedCollection> _trackedCollections = new();

    /// <summary>
    /// Owns lazy-spawn records produced by <see cref="CreateEntities{T}"/> when the target
    /// factory has <see cref="Factory{T}.LazySpawn"/> set to a non-<see cref="LazySpawnMode.Disabled"/>
    /// value. Tick this each frame with the camera's activation rect (or register the tilemap
    /// with a <see cref="Screen"/> via <c>Screen.Add(tileMap)</c>, which schedules the tick
    /// automatically).
    /// </summary>
    public LazySpawner LazySpawner { get; } = new LazySpawner();

    private readonly record struct TrackedCollection(
        Func<TilemapTileData, bool> Predicate,
        string? LayerName,
        TileShapes Collection);

    /// <summary>
    /// Loads a TMX file and positions the map in world space.
    /// </summary>
    /// <param name="tmxPath">Path to the .tmx file (e.g., "Content/Tiled/Level1.tmx").</param>
    /// <param name="graphicsDevice">The graphics device — pass <c>Engine.GraphicsDevice</c>.</param>
    /// <param name="x">Left edge of the map in world space. Default 0.</param>
    /// <param name="y">Top edge of the map in world space (Tiled convention). Default 0.</param>
    public TileMap(string tmxPath, GraphicsDevice graphicsDevice, float x = 0f, float y = 0f)
    {
        _graphicsDevice = graphicsDevice;
        _tilemap = TmxLoader(tmxPath, graphicsDevice);

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
                var mapLayer = new TileMapLayer(tileLayer.Name, renderable, tileLayer, _tilemap.Tilesets);
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
    /// Internal constructor for unit testing — creates a TileMap without loading a TMX file or
    /// constructing a renderer. Pass a hand-built <see cref="Tilemap"/> from MonoGame.Extended.
    /// </summary>
    internal TileMap(Tilemap tilemap, float x = 0f, float y = 0f)
    {
        _tilemap = tilemap;
        _renderer = null!;
        _graphicsDevice = null;
        _width = _tilemap.Width * _tilemap.TileWidth;
        _height = _tilemap.Height * _tilemap.TileHeight;
        _tileWidth = _tilemap.TileWidth;
        _tileHeight = _tilemap.TileHeight;

        _layers = new List<TileMapLayer>();
        _layersByName = new Dictionary<string, TileMapLayer>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in _tilemap.Layers)
        {
            if (layer is TilemapTileLayer tileLayer)
            {
                var mapLayer = new TileMapLayer(tileLayer.Name, tileLayer, _tilemap.Tilesets);
                _layers.Add(mapLayer);
                _layersByName[tileLayer.Name] = mapLayer;
            }
        }

        AssignDefaultZ();
        _x = x;
        _y = y;
        PropagatePosition();
    }

    /// <summary>
    /// Legacy internal constructor used by older tests that don't need a real
    /// <see cref="Tilemap"/>. Kept for backward compatibility with TileMapTests.
    /// </summary>
    internal TileMap(float width, float height, int tileWidth, int tileHeight,
        List<TileMapLayer> layers, float x = 0f, float y = 0f)
    {
        _tilemap = null!;
        _renderer = null!;
        _graphicsDevice = null;
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
    /// Returns the world-space center of the tile at (<paramref name="col"/>, <paramref name="row"/>),
    /// using Tiled's row convention — row 0 is the top row, increasing row moves downward
    /// (decreasing world Y). No bounds check — callers may pass coordinates outside the map.
    /// Useful for procedurally spawning entities at specific tile coordinates without
    /// duplicating the <c>X + col * TileWidth + TileWidth / 2</c> arithmetic.
    /// </summary>
    public Vector2 GetCellWorldPosition(int col, int row) => new(
        _x + col * _tileWidth + _tileWidth / 2f,
        _y - row * _tileHeight - _tileHeight / 2f);

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
    /// Generates a <see cref="TileShapes"/> from tiles whose
    /// <see cref="TilemapTileData.Class"/> matches <paramref name="className"/>.
    /// </summary>
    /// <param name="className">The tile class to match (case-insensitive).</param>
    /// <param name="layerName">
    /// If specified, restricts the search to this layer. If <c>null</c> (default),
    /// scans all tile layers.
    /// </param>
    public TileShapes GenerateCollisionFromClass(string className, string? layerName = null)
    {
        Func<TilemapTileData, bool> predicate = td =>
            string.Equals(td.Class, className, StringComparison.OrdinalIgnoreCase);

        TileShapes tsc = layerName != null
            ? TileMapCollisions.GenerateFromClass(_tilemap, GetInternalLayer(layerName), className, _x, _y)
            : TileMapCollisions.GenerateFromClass(_tilemap, className, _x, _y);

        tsc.Name = className;

        _trackedCollections.Add(new TrackedCollection(predicate, layerName, tsc));
        return tsc;
    }

    /// <summary>
    /// Generates a <see cref="TileShapes"/> from tiles that have a custom
    /// property named <paramref name="propertyName"/>.
    /// </summary>
    /// <param name="propertyName">The custom property to match (presence only, value ignored).</param>
    /// <param name="layerName">
    /// If specified, restricts the search to this layer. If <c>null</c> (default),
    /// scans all tile layers.
    /// </param>
    public TileShapes GenerateCollisionFromProperty(string propertyName, string? layerName = null)
    {
        Func<TilemapTileData, bool> predicate = td =>
            td.Properties.TryGetValue(propertyName, out _);

        TileShapes tsc = layerName != null
            ? TileMapCollisions.GenerateFromProperty(_tilemap, GetInternalLayer(layerName), propertyName, _x, _y)
            : TileMapCollisions.GenerateFromProperty(_tilemap, propertyName, _x, _y);

        _trackedCollections.Add(new TrackedCollection(predicate, layerName, tsc));
        return tsc;
    }

    /// <summary>
    /// Repositions the map so its center aligns with the given world-space point.
    /// </summary>
    public void CenterOn(float worldX, float worldY)
    {
        X = worldX - Width / 2f;
        Y = worldY + Height / 2f;
    }

    /// <summary>
    /// Creates entities from tiles whose class matches <paramref name="className"/>. Scans
    /// both object layers (for precisely-placed tile objects) and regular tile layers (for
    /// painted grid cells). Both sources feed into the same factory call.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tiled custom properties on each object are automatically applied to matching public
    /// instance properties on the entity via reflection (case-insensitive name match).
    /// Supported property types: <c>string</c>, <c>int</c>, <c>float</c>, <c>bool</c>.
    /// Tile-layer cells have no per-cell custom properties; reflection mapping is a no-op
    /// for that path.
    /// </para>
    /// <para>
    /// If the factory has <see cref="Factory{T}.IsSolidGrid"/> set, the whole scan is wrapped
    /// in a single grid batch so reposition-direction adjacency is recomputed once at the end.
    /// </para>
    /// </remarks>
    /// <param name="className">
    /// The tile class to match (case-insensitive). Checked on the object first, then on the
    /// tile definition in the tileset.
    /// </param>
    /// <param name="factory">The factory to create entities with.</param>
    /// <param name="origin">
    /// Which point on the tile becomes the entity's position. Default is <see cref="Origin.Center"/>.
    /// </param>
    /// <param name="removeSourceTiles">
    /// When <c>true</c> (default), each source tile is cleared after the entity spawns — the
    /// painted cell is set to empty and the tile-object is removed from its object layer — so
    /// the tile visual does not double-draw under the spawned entity. Contrast with
    /// <see cref="GenerateCollisionFromClass"/>, which leaves source tiles visible because the
    /// tile itself is the visual. In-memory only; the on-disk TMX is never modified and a fresh
    /// load re-applies the removal. Pass <c>false</c> to keep the source tile visible (e.g.,
    /// the tile is both a spawn marker and intentional background art).
    /// </param>
    /// <param name="configure">
    /// Optional callback invoked on each spawned entity after position assignment and Tiled
    /// custom-property reflection have run. Use this to wire references the entity needs at
    /// runtime (collision collections, target references, etc.). With
    /// <see cref="Factory{T}.LazySpawn"/> set, the callback is replayed each time the placement
    /// spawns (deferred to camera-reaches-rect; replayed again per re-spawn in
    /// <see cref="LazySpawnMode.Reloadable"/>) — so each fresh instance gets fresh wiring.
    /// </param>
    /// <returns>
    /// In eager mode (<see cref="Factory{T}.LazySpawn"/> is <see cref="LazySpawnMode.Disabled"/>),
    /// the entities created during this call. In lazy mode the list is empty — entities don't
    /// exist yet. Iterate <see cref="Factory{T}.Instances"/> on the factory to find live
    /// instances at any later moment.
    /// </returns>
    public IReadOnlyList<T> CreateEntities<T>(
        string className,
        Factory<T> factory,
        Origin origin = Origin.Center,
        bool removeSourceTiles = true,
        Action<T>? configure = null)
        where T : Entity, new()
    {
        if (_tilemap == null)
            throw new InvalidOperationException("CreateEntities requires a loaded TMX file.");

        var created = new List<T>();
        var entityProps = BuildPropertyMap<T>();

        // Batch grid updates across the whole scan when the factory is an IsSolidGrid — a row of
        // painted bricks should recompute SolidSides once at the end, not per-tile.
        IDisposable? batch = factory.IsSolidGrid ? factory.BeginGridBatch() : null;

        foreach (var layer in _tilemap.Layers)
        {
            switch (layer)
            {
                case TilemapObjectLayer objectLayer:
                    ScanObjectLayer(objectLayer, className, origin, factory, entityProps, created, removeSourceTiles, configure);
                    break;
                case TilemapTileLayer tileLayer:
                    ScanTileLayer(tileLayer, className, origin, factory, created, removeSourceTiles, configure);
                    break;
            }
        }

        batch?.Dispose();
        return created;
    }

    private void ScanObjectLayer<T>(
        TilemapObjectLayer objectLayer,
        string className,
        Origin origin,
        Factory<T> factory,
        Dictionary<string, PropertyInfo> entityProps,
        List<T> created,
        bool removeSourceTiles,
        Action<T>? configure) where T : Entity, new()
    {
        // Collect matches first so we can mutate the layer's object list after iteration.
        List<TilemapTileObject>? toRemove = null;
        bool lazy = factory.LazySpawn != LazySpawnMode.Disabled;

        foreach (var obj in objectLayer.Objects)
        {
            if (obj is not TilemapTileObject tileObj)
                continue;

            if (!MatchesClass(tileObj, className))
                continue;

            var (worldX, worldY) = ConvertToWorldSpace(tileObj, origin);

            if (lazy)
            {
                // Snapshot the property bag now — the live TilemapProperties is owned by the
                // tile object, which removeSourceTiles is about to drop. The snapshot keeps the
                // record self-contained for replay at spawn time.
                var capturedProps = SnapshotProperties(tileObj.Properties);
                LazySpawner.Add(factory, worldX, worldY, applyAfterInit: e =>
                {
                    ApplyCapturedProperties(e, capturedProps, entityProps);
                    configure?.Invoke(e);
                });
            }
            else
            {
                var entity = factory.Create();
                entity.X = worldX;
                entity.Y = worldY;
                ApplyCustomProperties(entity, tileObj.Properties, entityProps);
                configure?.Invoke(entity);
                created.Add(entity);
            }

            if (removeSourceTiles)
                (toRemove ??= new List<TilemapTileObject>()).Add(tileObj);
        }

        if (toRemove != null)
            foreach (var obj in toRemove)
                objectLayer.RemoveObject(obj);
    }

    private void ScanTileLayer<T>(
        TilemapTileLayer tileLayer,
        string className,
        Origin origin,
        Factory<T> factory,
        List<T> created,
        bool removeSourceTiles,
        Action<T>? configure) where T : Entity, new()
    {
        int tw = _tileWidth;
        int th = _tileHeight;
        bool lazy = factory.LazySpawn != LazySpawnMode.Disabled;

        for (int row = 0; row < tileLayer.Height; row++)
        {
            for (int col = 0; col < tileLayer.Width; col++)
            {
                var tileNullable = tileLayer.GetTile(col, row);
                if (!tileNullable.HasValue || tileNullable.Value.GlobalId == 0)
                    continue;

                var tileData = tileNullable.Value.GetTileData(_tilemap.Tilesets);
                if (tileData == null ||
                    !string.Equals(tileData.Class, className, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Tile (col, row) in Tiled pixel coords has its bottom-left at
                // (col*tw, (row+1)*th) measured from the map's top-left, Y-down.
                // Convert to world space (Y-up) using _x/_y = top-left.
                float bottomLeftX = _x + col * tw;
                float bottomLeftY = _y - (row + 1) * th;

                var (worldX, worldY) = OriginOffset(bottomLeftX, bottomLeftY, tw, th, origin);

                if (lazy)
                {
                    LazySpawner.Add(factory, worldX, worldY, applyAfterInit: configure);
                }
                else
                {
                    var entity = factory.Create();
                    entity.X = worldX;
                    entity.Y = worldY;
                    // Tile layers have no per-cell custom properties — skip reflection mapping.
                    configure?.Invoke(entity);
                    created.Add(entity);
                }

                if (removeSourceTiles)
                    tileLayer.SetTile(col, row, null);
            }
        }
    }

    private static Dictionary<string, TilemapPropertyValue> SnapshotProperties(TilemapProperties src)
    {
        var copy = new Dictionary<string, TilemapPropertyValue>(src.Count);
        foreach (var kvp in src)
            copy[kvp.Key] = kvp.Value;
        return copy;
    }

    private static void ApplyCapturedProperties<T>(
        T entity,
        Dictionary<string, TilemapPropertyValue> captured,
        Dictionary<string, PropertyInfo> entityProps) where T : Entity
    {
        if (captured.Count == 0) return;
        foreach (var (name, propInfo) in entityProps)
        {
            if (!captured.TryGetValue(name, out var tiledValue))
                continue;
            object? converted = propInfo.PropertyType switch
            {
                Type t when t == typeof(string) => tiledValue.AsString(),
                Type t when t == typeof(int) => tiledValue.AsInt(),
                Type t when t == typeof(float) => tiledValue.AsFloat(),
                Type t when t == typeof(bool) => tiledValue.AsBool(),
                _ => null,
            };
            if (converted != null)
                propInfo.SetValue(entity, converted);
        }
    }

    private static (float x, float y) OriginOffset(float blX, float blY, float w, float h, Origin origin)
    {
        return origin switch
        {
            Origin.Center => (blX + w / 2f, blY + h / 2f),
            Origin.BottomCenter => (blX + w / 2f, blY),
            Origin.TopCenter => (blX + w / 2f, blY + h),
            Origin.BottomLeft => (blX, blY),
            Origin.TopLeft => (blX, blY + h),
            Origin.BottomRight => (blX + w, blY),
            Origin.TopRight => (blX + w, blY + h),
            _ => (blX + w / 2f, blY + h / 2f),
        };
    }

    private bool MatchesClass(TilemapTileObject tileObj, string className)
    {
        // Check the object's own Class first (inherits from tile class in Tiled).
        if (!string.IsNullOrEmpty(tileObj.Class))
            return string.Equals(tileObj.Class, className, StringComparison.OrdinalIgnoreCase);

        // Fall back to the tile definition's Class in the tileset.
        var tileData = tileObj.Tile.GetTileData(_tilemap!.Tilesets);
        return tileData != null &&
               string.Equals(tileData.Class, className, StringComparison.OrdinalIgnoreCase);
    }

    private (float x, float y) ConvertToWorldSpace(TilemapTileObject tileObj, Origin origin)
    {
        // Tiled tile objects have position at the bottom-left corner, Y-down from map top-left.
        float bottomLeftX = _x + tileObj.Position.X;
        float bottomLeftY = _y - tileObj.Position.Y;

        float w = tileObj.Size.X;
        float h = tileObj.Size.Y;

        return origin switch
        {
            Origin.Center => (bottomLeftX + w / 2f, bottomLeftY + h / 2f),
            Origin.BottomCenter => (bottomLeftX + w / 2f, bottomLeftY),
            Origin.TopCenter => (bottomLeftX + w / 2f, bottomLeftY + h),
            Origin.BottomLeft => (bottomLeftX, bottomLeftY),
            Origin.TopLeft => (bottomLeftX, bottomLeftY + h),
            Origin.BottomRight => (bottomLeftX + w, bottomLeftY),
            Origin.TopRight => (bottomLeftX + w, bottomLeftY + h),
            _ => (bottomLeftX + w / 2f, bottomLeftY + h / 2f),
        };
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMap<T>()
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanWrite)
                map[prop.Name] = prop;
        }
        return map;
    }

    private static void ApplyCustomProperties<T>(
        T entity,
        TilemapProperties? tiledProps,
        Dictionary<string, PropertyInfo> entityProps)
        where T : Entity
    {
        if (tiledProps == null || tiledProps.Count == 0)
            return;

        foreach (var (name, propInfo) in entityProps)
        {
            if (!tiledProps.TryGetValue(name, out var tiledValue))
                continue;

            object? converted = propInfo.PropertyType switch
            {
                Type t when t == typeof(string) => tiledValue.AsString(),
                Type t when t == typeof(int) => tiledValue.AsInt(),
                Type t when t == typeof(float) => tiledValue.AsFloat(),
                Type t when t == typeof(bool) => tiledValue.AsBool(),
                _ => null,
            };

            if (converted != null)
                propInfo.SetValue(entity, converted);
        }
    }

    /// <summary>
    /// Re-parses <paramref name="tmxPath"/> and applies tile-data changes in place.
    /// Returns <c>true</c> if applied; <c>false</c> if the new TMX differs structurally
    /// (map dimensions, layer set, tilesets, or object layers) — in which case the caller
    /// should fall back to <c>RestartScreen(RestartMode.HotReload)</c>.
    /// </summary>
    /// <remarks>
    /// In-place reload preserves all entity state, camera position, and live
    /// <see cref="TileShapes"/> references — collision relationships keep working
    /// without reattachment. Hand-authored mutations made directly to a generated
    /// <see cref="TileShapes"/> after <see cref="GenerateCollisionFromClass"/>
    /// (e.g. extra <c>AddPolygonTileAtCell</c> calls) are wiped: the engine rebuilds each
    /// tracked collection from the new tile data. Put augmentations in
    /// <c>CustomInitialize</c> if you need them to survive a full restart.
    /// </remarks>
    public bool TryReloadFrom(string tmxPath)
    {
        if (_graphicsDevice == null)
            throw new InvalidOperationException(
                "TryReloadFrom requires the TileMap to have been constructed with a GraphicsDevice.");

        var newTilemap = TmxLoader(tmxPath, _graphicsDevice);
        return TryReload(newTilemap);
    }

    /// <summary>
    /// Test seam for <see cref="TryReloadFrom"/>. Applies tile-data changes from
    /// <paramref name="newTilemap"/> onto this map's existing layers, returning <c>false</c>
    /// if the structures differ.
    /// </summary>
    internal bool TryReload(Tilemap newTilemap)
    {
        if (!IsStructurallyCompatible(_tilemap, newTilemap))
            return false;

        // Per-cell SetTile on the live TilemapTileLayer instances. The renderer holds the same
        // layer references so it sees the new tile data automatically (we still rebuild its
        // vertex cache below).
        for (int li = 0; li < _tilemap.Layers.Count; li++)
        {
            if (_tilemap.Layers[li] is not TilemapTileLayer oldLayer) continue;
            var newLayer = (TilemapTileLayer)newTilemap.Layers[li];

            for (int row = 0; row < oldLayer.Height; row++)
            {
                for (int col = 0; col < oldLayer.Width; col++)
                {
                    var oldTile = oldLayer.GetTile(col, row);
                    var newTile = newLayer.GetTile(col, row);
                    if (!TilesEqual(oldTile, newTile))
                        oldLayer.SetTile(col, row, newTile);
                }
            }
        }

        // Rebuild every tracked TSC against the now-updated _tilemap.
        foreach (var tracked in _trackedCollections)
        {
            tracked.Collection.Clear();
            if (tracked.LayerName != null)
                TileMapCollisions.RegenerateInto(
                    _tilemap, GetInternalLayer(tracked.LayerName), tracked.Predicate, tracked.Collection);
            else
                TileMapCollisions.RegenerateInto(
                    _tilemap, tracked.Predicate, tracked.Collection);
        }

        // Refresh the renderer's vertex cache so the visual update is visible next frame.
        // No-op in unit tests where _renderer is null.
        _renderer?.LoadTilemap(_tilemap);

        return true;
    }

    private static bool TilesEqual(TilemapTile? a, TilemapTile? b)
    {
        if (!a.HasValue && !b.HasValue) return true;
        if (a.HasValue != b.HasValue) return false;
        return a!.Value.GlobalId == b!.Value.GlobalId &&
               a.Value.FlipFlags == b.Value.FlipFlags;
    }

    private static bool IsStructurallyCompatible(Tilemap oldMap, Tilemap newMap)
    {
        if (oldMap.Width != newMap.Width) return false;
        if (oldMap.Height != newMap.Height) return false;
        if (oldMap.TileWidth != newMap.TileWidth) return false;
        if (oldMap.TileHeight != newMap.TileHeight) return false;

        if (oldMap.Layers.Count != newMap.Layers.Count) return false;
        for (int i = 0; i < oldMap.Layers.Count; i++)
        {
            var a = oldMap.Layers[i];
            var b = newMap.Layers[i];
            if (a.GetType() != b.GetType()) return false;
            if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)) return false;

            if (a is TilemapTileLayer atl && b is TilemapTileLayer btl)
            {
                if (atl.Width != btl.Width || atl.Height != btl.Height) return false;
            }
            else if (a is TilemapObjectLayer aol && b is TilemapObjectLayer bol)
            {
                // Conservative: any object-count difference forces restart. v1 doesn't
                // do per-object equality — if you move a spawn marker the screen restarts.
                if (aol.Objects.Count != bol.Objects.Count) return false;
            }
        }

        if (oldMap.Tilesets.Count != newMap.Tilesets.Count) return false;
        for (int i = 0; i < oldMap.Tilesets.Count; i++)
        {
            var a = oldMap.Tilesets[i];
            var b = newMap.Tilesets[i];
            if (a.FirstGlobalId != b.FirstGlobalId) return false;
            if (a.TileCount != b.TileCount) return false;
            if (!string.Equals(a.Name, b.Name, StringComparison.Ordinal)) return false;
        }

        return true;
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
