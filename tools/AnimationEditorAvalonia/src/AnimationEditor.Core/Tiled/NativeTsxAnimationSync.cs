using DotTiled;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>Outcome of <see cref="NativeTsxAnimationSync.Apply"/> -- whether the tileset was
/// actually mutated, so a caller can skip rewriting the <c>.tsx</c> on a no-op save.</summary>
public sealed record NativeTsxAnimationSyncResult(bool Changed);

/// <summary>
/// Applies <see cref="MultiTileToTiledAnimationMapper.Map"/> results onto a <see cref="Tileset"/>
/// for a native <c>.tsx</c> project (issue #1140) -- the save-side counterpart of <see
/// cref="TiledAnimationToAchjMapper"/>. Unlike <see cref="TilesetAnimationSync"/> (achj-push, source-
/// scoped so multiple achx files can share one tileset), a native project owns the whole file: any
/// previously-animated tile not represented in <paramref name="results"/> is stale and gets cleared,
/// full stop.
/// </summary>
public static class NativeTsxAnimationSync
{
    /// <summary>
    /// Every tile id a save wants to write to must be claimed by exactly one chain (as its anchor
    /// or one of its satellites) -- a Tiled tile can only carry one &lt;animation&gt;. Two chains
    /// whose geometry happens to compute the same tile id, or a group whose own anchor and
    /// satellite alias each other, would otherwise silently overwrite one another in whatever
    /// order <paramref name="results"/> happens to iterate. Matches this codebase's "fail loudly
    /// instead of corrupting" precedent (issue #1145) rather than picking a winner.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two chains claim the same tile id.</exception>
    private static void ValidateNoTileIdCollisions(IReadOnlyList<MultiTileMappingResult> results)
    {
        var claimedBy = new Dictionary<uint, string>();

        void Claim(uint tileId, string chainName)
        {
            if (claimedBy.TryGetValue(tileId, out var existingChain))
                throw new InvalidOperationException(
                    $"Can't save: Tiled tile {tileId} would be claimed by both \"{existingChain}\" and " +
                    $"\"{chainName}\". Two animations can't share one tile -- move one of them in the " +
                    "spritesheet or rename it so they no longer compute the same tile id.");
            claimedBy[tileId] = chainName;
        }

        foreach (var result in results)
        {
            if (result.EntryTileId is { } entryTileId)
                Claim(entryTileId, result.ChainName);
            foreach (var satellite in result.Satellites)
                Claim(satellite.TileId, result.ChainName);
        }
    }

    public static NativeTsxAnimationSyncResult Apply(Tileset tileset, IReadOnlyList<MultiTileMappingResult> results)
    {
        ValidateNoTileIdCollisions(results);

        // Built once so every lookup below is O(1) instead of an O(n) scan of tileset.Tiles per
        // stale tile cleared and per anchor/satellite applied -- matters on a tileset with
        // thousands of tiles. Kept in sync by ApplyTile whenever it adds a brand-new tile;
        // ValidateNoTileIdCollisions above already guarantees every id touched in this call is
        // unique, so no lookup ever needs to see a tile created earlier in the same call.
        var tilesById = BuildTilesById(tileset);

        var previouslyAnimatedTileIds = tileset.Tiles
            .Where(t => t.Animation.Count > 0)
            .Select(t => t.ID)
            .ToHashSet();

        var newTileIds = new HashSet<uint>();
        foreach (var result in results)
        {
            if (result.EntryTileId is { } entryTileId)
                newTileIds.Add(entryTileId);
            foreach (var satellite in result.Satellites)
                newTileIds.Add(satellite.TileId);
        }

        var changed = false;

        foreach (var staleTileId in previouslyAnimatedTileIds.Except(newTileIds))
            if (ClearTile(tilesById[staleTileId]))
                changed = true;

        foreach (var result in results)
        {
            if (result.EntryTileId is not { } entryTileId)
                continue;

            if (ApplyTile(tileset, tilesById, entryTileId, result.AnchorFrames, explicitName: SyntheticName(entryTileId) == result.ChainName ? null : result.ChainName))
                changed = true;

            foreach (var satellite in result.Satellites)
                if (ApplyTile(tileset, tilesById, satellite.TileId, satellite.Frames, explicitName: null, parentId: entryTileId))
                    changed = true;
        }

        tileset.Tiles.Sort((a, b) => a.ID.CompareTo(b.ID));
        return new NativeTsxAnimationSyncResult(changed);
    }

    /// <summary>Builds a tile-id-keyed dictionary of every tile, throwing a clear error instead of
    /// <see cref="Dictionary{TKey,TValue}"/>'s own opaque "item with the same key" exception when a
    /// corrupt/hand-edited tsx has two &lt;tile&gt; elements sharing one id.</summary>
    private static Dictionary<uint, Tile> BuildTilesById(Tileset tileset)
    {
        var tilesById = new Dictionary<uint, Tile>();
        foreach (var tile in tileset.Tiles)
            if (!tilesById.TryAdd(tile.ID, tile))
                throw new InvalidOperationException(
                    $"Can't sync: tileset \"{tileset.Name}\" has more than one tile with id {tile.ID}, which isn't valid Tiled data.");
        return tilesById;
    }

    private static string SyntheticName(uint tileId) => $"ID:{tileId}";

    private static bool ApplyTile(
        Tileset tileset, Dictionary<uint, Tile> tilesById, uint tileId, IReadOnlyList<MappedFrame> frames,
        string? explicitName, uint? parentId = null)
    {
        var isNewTile = false;
        if (!tilesById.TryGetValue(tileId, out var tile))
        {
            isNewTile = true;
            tile = new Tile { ID = tileId, Width = 0, Height = 0 };
            tileset.Tiles.Add(tile);
            tilesById[tileId] = tile;
        }

        var changed = isNewTile;

        var newAnimation = frames.Select(f => new Frame { TileID = f.TileId, Duration = f.Duration }).ToList();
        if (!AnimationEquals(tile.Animation, newAnimation))
        {
            tile.Animation = newAnimation;
            changed = true;
        }

        if (SetOrRemoveStringProperty(tile, TiledAnimationToAchjMapper.NamePropertyName, explicitName)) changed = true;
        if (SetOrRemoveIntProperty(tile, TiledAnimationToAchjMapper.ParentIdPropertyName, parentId.HasValue ? (int)parentId.Value : null)) changed = true;

        return changed;
    }

    /// <summary>Clears a stale tile's animation and any AnimationEditor-owned tracking properties.
    /// Returns whether it actually had anything to clear.</summary>
    private static bool ClearTile(Tile tile)
    {
        var hadAnimation = tile.Animation.Count > 0;
        var hadTrackingProperties = tile.Properties.Any(p =>
            p.Name is TiledAnimationToAchjMapper.NamePropertyName or TiledAnimationToAchjMapper.ParentIdPropertyName);

        tile.Animation = [];
        tile.Properties.RemoveAll(p =>
            p.Name is TiledAnimationToAchjMapper.NamePropertyName or TiledAnimationToAchjMapper.ParentIdPropertyName);

        return hadAnimation || hadTrackingProperties;
    }

    private static bool AnimationEquals(List<Frame> a, List<Frame> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (a[i].TileID != b[i].TileID || a[i].Duration != b[i].Duration)
                return false;
        return true;
    }

    /// <summary>Finds a property by name regardless of its concrete type, and removes it if it
    /// isn't a <typeparamref name="T"/> -- a hand-authored file can carry e.g. an int-typed "Name"
    /// property. Matching only <c>OfType&lt;T&gt;()</c> would miss it entirely and add a *second*,
    /// correctly-typed property with the same name alongside it: two <c>&lt;property name="Name"&gt;</c>
    /// entries, which real Tiled never produces and won't round-trip cleanly.</summary>
    private static T? FindByNameRemovingWrongType<T>(Tile tile, string name) where T : class, IProperty
    {
        var existingWrongType = tile.Properties.FirstOrDefault(p => p.Name == name && p is not T);
        if (existingWrongType != null)
            tile.Properties.Remove(existingWrongType);
        return tile.Properties.OfType<T>().FirstOrDefault(p => p.Name == name);
    }

    private static bool SetOrRemoveStringProperty(Tile tile, string name, string? value)
    {
        var hadWrongType = tile.Properties.Any(p => p.Name == name && p is not StringProperty);
        var existing = FindByNameRemovingWrongType<StringProperty>(tile, name);
        if (value is null)
        {
            if (existing is null) return hadWrongType;
            tile.Properties.Remove(existing);
            return true;
        }

        if (existing != null)
        {
            if (existing.Value == value) return hadWrongType;
            existing.Value = value;
            return true;
        }

        tile.Properties.Add(new StringProperty { Name = name, Value = value });
        return true;
    }

    private static bool SetOrRemoveIntProperty(Tile tile, string name, int? value)
    {
        var hadWrongType = tile.Properties.Any(p => p.Name == name && p is not IntProperty);
        var existing = FindByNameRemovingWrongType<IntProperty>(tile, name);
        if (value is null)
        {
            if (existing is null) return hadWrongType;
            tile.Properties.Remove(existing);
            return true;
        }

        if (existing != null)
        {
            if (existing.Value == value.Value) return hadWrongType;
            existing.Value = value.Value;
            return true;
        }

        tile.Properties.Add(new IntProperty { Name = name, Value = value.Value });
        return true;
    }
}
