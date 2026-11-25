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
/// cref="TiledAnimationToAchjMapper"/>. A native project owns every animated tile:
/// any previously-animated tile not represented in <paramref name="results"/> is stale and gets
/// cleared.
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

    /// <param name="userProperties">What the user owned when the file was loaded; null reads it from
    /// <paramref name="tileset"/> as it is now.</param>
    public static NativeTsxAnimationSyncResult Apply(
        Tileset tileset, IReadOnlyList<MultiTileMappingResult> results, TsxUserProperties? userProperties = null)
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

        userProperties ??= TsxUserProperties.Read(tileset);

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
        {
            var staleTile = tilesById[staleTileId];
            ClearTile(staleTile, userProperties);
            changed = true;
            if (IsTileEmpty(staleTile))
            {
                tileset.Tiles.Remove(staleTile);
                tilesById.Remove(staleTileId);
            }
        }

        foreach (var result in results)
        {
            // A result carrying a warning is a mapping FAILURE (see MultiTileToTiledAnimationMapper
            // .MapChain's Empty() helper) -- its EntryTileId/Satellites, when present, are only the
            // chain's last-known identity hints, kept so the stale-clearing loop above doesn't wipe
            // them, not real geometry to write. Applying result.AnchorFrames (always empty here)
            // would overwrite the tile's real, previously-working animation with nothing.
            if (result.Warnings.Count > 0)
                continue;

            if (result.EntryTileId is not { } entryTileId)
                continue;

            if (ApplyTile(tileset, tilesById, userProperties, result.ChainName, entryTileId, result.AnchorFrames, parentId: null))
                changed = true;

            foreach (var satellite in result.Satellites)
                if (ApplyTile(tileset, tilesById, userProperties, result.ChainName, satellite.TileId, satellite.Frames, parentId: entryTileId))
                    changed = true;
        }

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

    /// <summary>
    /// Clones what <see cref="TsxLoader"/> reads, deeply where <see cref="Apply"/> changes it, so a caller can run
    /// <see cref="Apply"/> on a working copy and adopt it once it is written (see <see cref="ProjectManager.WriteDocument"/>).
    /// </summary>
    internal static Tileset CloneForSave(Tileset tileset) => new()
    {
        Name = tileset.Name,
        TileWidth = tileset.TileWidth,
        TileHeight = tileset.TileHeight,
        Spacing = tileset.Spacing,
        Margin = tileset.Margin,
        TileCount = tileset.TileCount,
        Columns = tileset.Columns,
        Image = tileset.Image,
        Tiles = tileset.Tiles.Select(CloneTile).ToList(),
    };

    private static Tile CloneTile(Tile tile) => new()
    {
        ID = tile.ID,
        Type = tile.Type,
        Width = tile.Width,
        Height = tile.Height,
        Properties = tile.Properties.Select(p => p.Clone()).ToList(),
        Animation = tile.Animation.ToList(),
    };

    /// <param name="parentId">The anchor's tile id for a satellite, null for the chain's own tile.</param>
    private static bool ApplyTile(
        Tileset tileset, Dictionary<uint, Tile> tilesById, TsxUserProperties userProperties, string chainName, uint tileId,
        IReadOnlyList<MappedFrame> frames, uint? parentId)
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

        if (SetParentId(tile, chainName, parentId, userProperties)) changed = true;
        if (parentId is null ? SetName(tile, chainName, userProperties) : RemoveOwnName(tile, userProperties)) changed = true;

        var newAnimation = frames.Select(f => new Frame { TileID = f.TileId, Duration = f.Duration }).ToList();
        if (!AnimationEquals(tile.Animation, newAnimation))
        {
            tile.Animation = newAnimation;
            changed = true;
        }

        return changed;
    }

    /// <summary>Clears a stale tile's animation and the properties this sync wrote. Doesn't remove the tile
    /// itself -- callers that want that call <see cref="IsTileEmpty"/> afterward, since a tile carrying other,
    /// non-editor-owned properties must stay in the file.</summary>
    private static void ClearTile(Tile tile, TsxUserProperties userProperties)
    {
        RemoveOwnName(tile, userProperties);
        if (TiledAnimationToAchjMapper.GetParentId(tile) is not null && !userProperties.ParentIdTileIds.Contains(tile.ID))
            tile.Properties.Remove(FindProperty(tile, TiledAnimationToAchjMapper.ParentIdPropertyName)!);
        tile.Animation = [];
    }

    /// <summary>Whether the model has nothing left for a tile. <see cref="TsxWriter"/> keeps its element in the file
    /// when that still holds something the model does not.</summary>
    private static bool IsTileEmpty(Tile tile) =>
        string.IsNullOrEmpty(tile.Type) && tile.Properties.Count == 0 && tile.Animation.Count == 0;

    private static bool AnimationEquals(List<Frame> a, List<Frame> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (a[i].TileID != b[i].TileID || a[i].Duration != b[i].Duration)
                return false;
        return true;
    }

    private static IProperty? FindProperty(Tile tile, string name) => tile.Properties.FirstOrDefault(p => p.Name == name);

    /// <summary>Stores a chain's name on its own tile; the name the tile has without one is not stored.</summary>
    /// <exception cref="InvalidOperationException">The user's own <c>Name</c> is in the way.</exception>
    private static bool SetName(Tile tile, string chainName, TsxUserProperties userProperties)
    {
        var existing = TiledAnimationToAchjMapper.FindNameProperty(tile);
        if ((existing?.Value ?? "") == chainName)
            return false;

        if (userProperties.Names.TryGetValue(tile.ID, out var userName) && !TsxUserProperties.IsNoName(tile, userName))
            throw OwnedByUser(tile, TiledAnimationToAchjMapper.NamePropertyName, chainName, $"Rename the animation to \"{userName}\", use");

        var defaultName = TiledAnimationToAchjMapper.DefaultChainName(tile);
        if (chainName == defaultName)
            return RemoveOwnName(tile, userProperties);

        if (existing is not null)
            existing.Value = chainName;
        // A second property of the name would be invalid in Tiled.
        else if (FindProperty(tile, TiledAnimationToAchjMapper.NamePropertyName) is not null)
            throw OwnedByUser(tile, TiledAnimationToAchjMapper.NamePropertyName, chainName, $"Rename the animation to \"{defaultName}\", use");
        else
            tile.Properties.Add(new StringProperty { Name = TiledAnimationToAchjMapper.NamePropertyName, Value = chainName });
        return true;
    }

    /// <summary>Removes a tile's <c>Name</c>, or puts back the one the user had.</summary>
    private static bool RemoveOwnName(Tile tile, TsxUserProperties userProperties)
    {
        if (TiledAnimationToAchjMapper.FindNameProperty(tile) is not { } existing)
            return false;
        if (!userProperties.Names.TryGetValue(tile.ID, out var userName))
            return tile.Properties.Remove(existing);
        if (existing.Value == userName)
            return false;
        existing.Value = userName;
        return true;
    }

    /// <summary>Makes a tile a satellite of <paramref name="parentId"/>, or of none.</summary>
    /// <exception cref="InvalidOperationException">The user's own <c>ParentId</c> is in the way.</exception>
    private static bool SetParentId(Tile tile, string chainName, uint? parentId, TsxUserProperties userProperties)
    {
        var existing = FindProperty(tile, TiledAnimationToAchjMapper.ParentIdPropertyName);
        var readParentId = TiledAnimationToAchjMapper.GetParentId(tile);
        if (readParentId == parentId)
            return false;

        if (existing is not null && (readParentId is null || userProperties.ParentIdTileIds.Contains(tile.ID)))
            throw OwnedByUser(tile, TiledAnimationToAchjMapper.ParentIdPropertyName, chainName, "Use");

        if (parentId is not { } newParentId)
            tile.Properties.Remove(existing!);
        else if (existing is IntProperty property)
            property.Value = (int)newParentId;
        else
            tile.Properties.Add(new IntProperty { Name = TiledAnimationToAchjMapper.ParentIdPropertyName, Value = (int)newParentId });
        return true;
    }

    private static InvalidOperationException OwnedByUser(Tile tile, string propertyName, string chainName, string advice) =>
        new($"Can't save: Tiled tile {tile.ID} has its own \"{propertyName}\" property, which the animation " +
            $"\"{chainName}\" would overwrite. {advice} another tile, or undo.");
}
