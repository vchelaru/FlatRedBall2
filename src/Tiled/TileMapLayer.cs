using System;
using System.Collections.Generic;
using FlatRedBall2.Rendering;
using MonoGame.Extended.Tilemaps;

namespace FlatRedBall2.Tiled;

/// <summary>
/// A single tile layer within a <see cref="TileMap"/>. Provides per-layer control over
/// Z-order, visibility, rendering layer, and per-cell tile mutation without exposing
/// MonoGame.Extended types.
/// </summary>
/// <remarks>
/// Position is owned by the parent <see cref="TileMap"/> — setting <see cref="TileMap.X"/>
/// or <see cref="TileMap.Y"/> propagates to all layers automatically.
/// </remarks>
public class TileMapLayer
{
    internal TileMapLayerRenderable Renderable { get; }
    private readonly TilemapTileLayer? _tileLayer;
    private readonly TilemapTilesetCollection? _tilesets;

    internal TileMapLayer(string name, TileMapLayerRenderable renderable,
        TilemapTileLayer tileLayer, TilemapTilesetCollection tilesets)
    {
        Name = name;
        Renderable = renderable;
        _tileLayer = tileLayer;
        _tilesets = tilesets;
    }

    /// <summary>
    /// Internal constructor for unit testing through a hand-built <see cref="Tilemap"/> —
    /// no renderable, but tile mutation works against the real <see cref="TilemapTileLayer"/>.
    /// </summary>
    internal TileMapLayer(string name, TilemapTileLayer tileLayer, TilemapTilesetCollection tilesets)
    {
        Name = name;
        Renderable = null!;
        _tileLayer = tileLayer;
        _tilesets = tilesets;
        _isTestMode = true;
    }

    /// <summary>
    /// Internal constructor for legacy unit testing — creates a TileMapLayer with neither a
    /// renderable nor an underlying tile layer. Tile-mutation APIs throw if called.
    /// </summary>
    internal TileMapLayer(string name)
    {
        Name = name;
        Renderable = null!;
        _testZ = 0f;
        _isTestMode = true;
    }

    private float _testZ;
    private readonly bool _isTestMode;

    /// <summary>The layer name as defined in the TMX file.</summary>
    public string Name { get; }

    /// <summary>
    /// Z-order for rendering. Set automatically by <see cref="TileMap"/> on construction:
    /// layers are spaced 1 apart in TMX order, with "GameplayLayer" at Z = 0 if it exists.
    /// Override this to interleave sprites or other renderables between tile layers.
    /// </summary>
    public float Z
    {
        get => _isTestMode ? _testZ : Renderable.Z;
        set
        {
            if (_isTestMode)
                _testZ = value;
            else
                Renderable.Z = value;
        }
    }

    /// <summary>Whether this layer is drawn. Defaults to the TMX layer's visibility.</summary>
    public bool IsVisible
    {
        get => Renderable.IsVisible;
        set => Renderable.IsVisible = value;
    }

    /// <summary>
    /// The FlatRedBall2 rendering layer. Set by <see cref="Screen.Add(TileMapLayer, Layer?)"/>
    /// or manually for fine-grained render-pass control.
    /// </summary>
    public Layer? Layer
    {
        get => Renderable.Layer;
        set => Renderable.Layer = value;
    }

    /// <summary>
    /// Replaces the tile at (<paramref name="col"/>, <paramref name="row"/>) with the tile
    /// having the given <paramref name="globalTileId"/>. Use the (col, row, className) overload
    /// for a more discoverable lookup that doesn't require memorizing tile IDs.
    /// </summary>
    /// <remarks>
    /// Tiled rows are Y-down (row 0 is the top row), matching the rest of the
    /// <see cref="TileMap"/> API. The mutation only affects the in-memory tile layer; the
    /// on-disk TMX is never modified. The change is picked up by the renderer next frame.
    /// </remarks>
    public void SetTile(int col, int row, int globalTileId)
    {
        EnsureMutable();
        _tileLayer!.SetTile(col, row, new TilemapTile(globalId: globalTileId));
    }

    /// <summary>
    /// Replaces the tile at (<paramref name="col"/>, <paramref name="row"/>) with a tile whose
    /// tileset class matches <paramref name="className"/> (case-insensitive). More discoverable
    /// than the integer-id overload — games can paint by class name configured in the Tiled
    /// tileset editor.
    /// </summary>
    /// <param name="col">Column of the cell to paint. Tiled rows are Y-down.</param>
    /// <param name="row">Row of the cell to paint. Tiled rows are Y-down.</param>
    /// <param name="className">
    /// The <see cref="TilemapTileData.Class"/> to look up across all tilesets in the parent map.
    /// </param>
    /// <param name="pickRandom">
    /// If <c>false</c> (default), throws <see cref="InvalidOperationException"/> if more than
    /// one tile matches the class — silent ambiguity is a footgun (works in dev with one
    /// "Dirt" tile, randomizes later when an artist adds a variant). If <c>true</c>, picks a
    /// uniformly random match using <see cref="FlatRedBallService.Default"/>'s shared random
    /// — use this for variety painting (Stardew-style "dug-up dirt" with multiple visuals).
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// No tile matches <paramref name="className"/>, or multiple match and
    /// <paramref name="pickRandom"/> is <c>false</c>.
    /// </exception>
    public void SetTile(int col, int row, string className, bool pickRandom = false)
    {
        EnsureMutable();
        var matches = FindGlobalIdsForClass(className);
        if (matches.Count == 0)
            throw new InvalidOperationException(
                $"No tile in any tileset has class '{className}'.");

        int chosenGlobalId;
        if (matches.Count == 1)
        {
            chosenGlobalId = matches[0];
        }
        else if (pickRandom)
        {
            chosenGlobalId = matches[FlatRedBallService.Default.Random.Next(matches.Count)];
        }
        else
        {
            throw new InvalidOperationException(
                $"Multiple tiles ({matches.Count}) match class '{className}'. " +
                "Pass pickRandom: true to choose a random match, or use the integer-id overload " +
                "to specify a particular tile.");
        }

        _tileLayer!.SetTile(col, row, new TilemapTile(globalId: chosenGlobalId));
    }

    /// <summary>
    /// Clears the tile at (<paramref name="col"/>, <paramref name="row"/>) so the cell renders
    /// empty. Tiled rows are Y-down (row 0 is the top row).
    /// </summary>
    public void RemoveTile(int col, int row)
    {
        EnsureMutable();
        _tileLayer!.SetTile(col, row, null);
    }

    private void EnsureMutable()
    {
        if (_tileLayer == null || _tilesets == null)
            throw new InvalidOperationException(
                "This TileMapLayer was constructed without a backing TilemapTileLayer " +
                "(legacy test seam). Tile mutation requires a TileMap built from a TMX " +
                "file or from the Tilemap-based test constructor.");
    }

    private List<int> FindGlobalIdsForClass(string className)
    {
        var matches = new List<int>();
        foreach (var tileset in _tilesets!)
        {
            int firstGid = tileset.FirstGlobalId;
            int last = firstGid + tileset.TileCount;
            for (int gid = firstGid; gid < last; gid++)
            {
                var data = new TilemapTile(globalId: gid).GetTileData(_tilesets);
                if (data == null) continue;
                if (string.Equals(data.Class, className, StringComparison.OrdinalIgnoreCase))
                    matches.Add(gid);
            }
        }
        return matches;
    }
}
