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
/// scoped so multiple achx files can share one tileset), a native project owns every tile it maps:
/// any previously-animated tile not represented in <paramref name="results"/> is stale and gets
/// cleared -- except a tile the achx-push feature (issue #1133) owns (tracked via <see
/// cref="TilesetAnimationSync.SourceFilePropertyName"/>), which is never absorbed into this
/// project's model in the first place (see <see cref="TiledAnimationToAchjMapper"/>'s matching
/// load-side exclusion) and so is left untouched here rather than misread as stale.
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

        ValidateNoAchxPushOwnedTileClaimed(tilesById, results);

        // A tile owned by the achx-push feature (issue #1133 -- tracked via
        // TilesetAnimationSync.SourceFilePropertyName) is never absorbed into this project's own
        // model (see TiledAnimationToAchjMapper's matching load-side exclusion), so it never
        // appears in `results`. Without this exclusion, the "any previously-animated tile absent
        // from results is stale, full stop" rule below would wipe another feature's animation on
        // this project's very next save, regardless of whether the user edited anything related.
        var previouslyAnimatedTileIds = tileset.Tiles
            .Where(t => !IsAchxPushOwned(t) && t.Animation.Count > 0)
            .Select(t => t.ID)
            .ToHashSet();

        // A Name is this sync's only where the editor reads it as a chain's name (issue #2014).
        TiledAnimationToAchjMapper.Map(tileset, out var ownTileIdsByChain, out _, out _);
        var chainTileIds = ownTileIdsByChain.Values.ToHashSet();

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
            ClearTile(staleTile, chainTileIds);
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

            if (ApplyTile(tileset, tilesById, chainTileIds, result.ChainName, entryTileId, result.AnchorFrames, parentId: null))
                changed = true;

            foreach (var satellite in result.Satellites)
                if (ApplyTile(tileset, tilesById, chainTileIds, result.ChainName, satellite.TileId, satellite.Frames, parentId: entryTileId))
                    changed = true;
        }

        tileset.Tiles.Sort((a, b) => a.ID.CompareTo(b.ID));
        return new NativeTsxAnimationSyncResult(changed);
    }

    /// <summary>Whether the achx-push feature (issue #1133) -- a save pipeline entirely
    /// independent of this native-tsx project -- currently owns this tile's animation, per
    /// <see cref="TilesetAnimationSync.SourceFilePropertyName"/>.</summary>
    private static bool IsAchxPushOwned(Tile tile) =>
        tile.Properties.OfType<StringProperty>().Any(p => p.Name == TilesetAnimationSync.SourceFilePropertyName);

    /// <summary>
    /// A native-tsx chain's own geometry computing the same tile id as a tile the achx-push
    /// feature already owns is the same "two independent claimants, can't silently pick a winner"
    /// situation <see cref="ValidateNoTileIdCollisions"/> already fails loudly for between two
    /// native-tsx chains -- extended here to the achx-push feature's own tiles, since <see
    /// cref="TiledAnimationToAchjMapper"/> never absorbs them into this project's model in the
    /// first place, so this is the only remaining place that could silently overwrite one.
    /// </summary>
    /// <exception cref="InvalidOperationException">A result claims a tile achx-push already owns.</exception>
    private static void ValidateNoAchxPushOwnedTileClaimed(Dictionary<uint, Tile> tilesById, IReadOnlyList<MultiTileMappingResult> results)
    {
        void CheckOwnership(uint tileId, string chainName)
        {
            if (!tilesById.TryGetValue(tileId, out var tile) || !IsAchxPushOwned(tile))
                return;

            var owningSource = tile.Properties.OfType<StringProperty>()
                .First(p => p.Name == TilesetAnimationSync.SourceFilePropertyName).Value;
            throw new InvalidOperationException(
                $"Can't save: Tiled tile {tileId} is already owned by achx-push source \"{owningSource}\" " +
                $"and can't also be claimed by native-tsx chain \"{chainName}\". Move \"{chainName}\" to a " +
                "different tile, or remove the achx-push association for this tile.");
        }

        foreach (var result in results)
        {
            if (result.EntryTileId is { } entryTileId)
                CheckOwnership(entryTileId, result.ChainName);
            foreach (var satellite in result.Satellites)
                CheckOwnership(satellite.TileId, result.ChainName);
        }
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
    /// Deep-clones exactly the parts of <paramref name="tileset"/> that <see cref="Apply"/> ever
    /// mutates: the <see cref="Tileset.Tiles"/> list itself (membership -- tiles get added), and
    /// per tile, its <see cref="Tile.Properties"/> list and the <see cref="IProperty"/> instances
    /// within it (mutated in place via add/remove/<c>.Value =</c>). <see cref="Tile.Animation"/> is
    /// never mutated in place by <see cref="Apply"/> -- only wholesale-reassigned -- so copying the
    /// list without cloning each <see cref="Frame"/> is enough. Every other field (tileset name/
    /// size/image/wangsets/transformations/properties; per-tile type/probability/x/y/width/height/
    /// image/object layer) is never touched by <see cref="Apply"/> and is shared by reference with
    /// the original.
    /// </summary>
    /// <remarks>
    /// Lets a caller run <see cref="Apply"/> against a working copy and only adopt it once writing
    /// the result has actually succeeded, so a write failure can't leave the caller's own tileset
    /// reflecting computed-but-never-persisted state -- see <see cref="ProjectManager.SaveTsxProject"/>.
    /// If <see cref="Apply"/> is ever extended to mutate a field this method doesn't copy, that
    /// field must be added here too.
    /// </remarks>
    internal static Tileset CloneForSave(Tileset tileset) => new()
    {
        Version = tileset.Version,
        TiledVersion = tileset.TiledVersion,
        FirstGID = tileset.FirstGID,
        Source = tileset.Source,
        Name = tileset.Name,
        Class = tileset.Class,
        TileWidth = tileset.TileWidth,
        TileHeight = tileset.TileHeight,
        Spacing = tileset.Spacing,
        Margin = tileset.Margin,
        TileCount = tileset.TileCount,
        Columns = tileset.Columns,
        ObjectAlignment = tileset.ObjectAlignment,
        RenderSize = tileset.RenderSize,
        FillMode = tileset.FillMode,
        Image = tileset.Image,
        TileOffset = tileset.TileOffset,
        Grid = tileset.Grid,
        Properties = tileset.Properties,
        Wangsets = tileset.Wangsets,
        Transformations = tileset.Transformations,
        Tiles = tileset.Tiles.Select(CloneTile).ToList(),
    };

    private static Tile CloneTile(Tile tile) => new()
    {
        ID = tile.ID,
        Type = tile.Type,
        Probability = tile.Probability,
        X = tile.X,
        Y = tile.Y,
        Width = tile.Width,
        Height = tile.Height,
        Properties = tile.Properties.Select(p => p.Clone()).ToList(),
        Image = tile.Image,
        ObjectLayer = tile.ObjectLayer,
        Animation = tile.Animation.ToList(),
    };

    /// <param name="parentId">The anchor's tile id for a satellite, null for the chain's own tile.</param>
    private static bool ApplyTile(
        Tileset tileset, Dictionary<uint, Tile> tilesById, HashSet<uint> chainTileIds, string chainName, uint tileId,
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

        // Before the animation is replaced: whose a property is depends on the tile as it was read.
        if (SetParentId(tile, chainName, parentId)) changed = true;
        if (parentId is null ? SetName(tile, chainName, chainTileIds) : RemoveOwnName(tile, chainTileIds)) changed = true;

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
    private static void ClearTile(Tile tile, HashSet<uint> chainTileIds)
    {
        RemoveOwnName(tile, chainTileIds);
        SetParentId(tile, chainName: "", parentId: null);
        tile.Animation = [];
    }

    /// <summary>Whether a tile has nothing left worth keeping a &lt;tile&gt; element for -- no
    /// animation, no properties, and none of Tiled's other per-tile data (type/probability/x/y/
    /// width/height/image/object layer). A tile that only ever existed to carry an animation this
    /// sync owns becomes exactly this once <see cref="ClearTile"/> strips it, and leaving it in
    /// <see cref="Tileset.Tiles"/> as a bare <c>&lt;tile id="N"/&gt;</c> stub would accumulate one
    /// such stub per animation ever removed.</summary>
    private static bool IsTileEmpty(Tile tile) =>
        string.IsNullOrEmpty(tile.Type) &&
        tile.Probability == 0f &&
        tile.X == 0 &&
        tile.Y == 0 &&
        tile.Width == 0 &&
        tile.Height == 0 &&
        !tile.Image.HasValue &&
        !tile.ObjectLayer.HasValue &&
        tile.Properties.Count == 0 &&
        tile.Animation.Count == 0;

    private static bool AnimationEquals(List<Frame> a, List<Frame> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (a[i].TileID != b[i].TileID || a[i].Duration != b[i].Duration)
                return false;
        return true;
    }

    private static IProperty? FindProperty(Tile tile, string name) => tile.Properties.FirstOrDefault(p => p.Name == name);

    /// <summary>Stores a chain's name on its own tile; the synthetic <c>"ID:N"</c> name is not stored.</summary>
    /// <exception cref="InvalidOperationException">The user's own <c>Name</c> is in the way.</exception>
    private static bool SetName(Tile tile, string chainName, HashSet<uint> chainTileIds)
    {
        var existing = FindProperty(tile, TiledAnimationToAchjMapper.NamePropertyName);
        var readName = existing is StringProperty { Value: var value } ? value : "";
        if (readName == chainName)
            return false;

        var isStored = chainName != TiledAnimationToAchjMapper.SyntheticChainName(tile.ID);
        var isOwnedByUser = readName != "" ? !chainTileIds.Contains(tile.ID) : existing is not (null or StringProperty) && isStored;
        if (isOwnedByUser)
            throw OwnedByUser(tile, TiledAnimationToAchjMapper.NamePropertyName, chainName);

        if (!isStored)
            return RemoveOwnName(tile, chainTileIds);
        if (existing is StringProperty name)
            name.Value = chainName;
        else
            tile.Properties.Add(new StringProperty { Name = TiledAnimationToAchjMapper.NamePropertyName, Value = chainName });
        return true;
    }

    /// <summary>Removes a tile's <c>Name</c> if the editor read it as a chain's name.</summary>
    private static bool RemoveOwnName(Tile tile, HashSet<uint> chainTileIds) =>
        chainTileIds.Contains(tile.ID)
        && FindProperty(tile, TiledAnimationToAchjMapper.NamePropertyName) is StringProperty { Value: not "" } name
        && tile.Properties.Remove(name);

    /// <summary>Makes a tile a satellite of <paramref name="parentId"/>, or of none. An existing value is this
    /// sync's only if the tile is animated and the editor reads the value as a tile id.</summary>
    /// <exception cref="InvalidOperationException">The user's own <c>ParentId</c> is in the way.</exception>
    private static bool SetParentId(Tile tile, string chainName, uint? parentId)
    {
        var existing = FindProperty(tile, TiledAnimationToAchjMapper.ParentIdPropertyName);
        var readParentId = TiledAnimationToAchjMapper.GetParentId(tile);
        if (readParentId == parentId)
            return false;

        if (existing is not null && (readParentId is null || tile.Animation.Count == 0))
            throw OwnedByUser(tile, TiledAnimationToAchjMapper.ParentIdPropertyName, chainName);

        if (parentId is not { } newParentId)
            tile.Properties.Remove(existing!);
        else if (existing is IntProperty property)
            property.Value = (int)newParentId;
        else
            tile.Properties.Add(new IntProperty { Name = TiledAnimationToAchjMapper.ParentIdPropertyName, Value = (int)newParentId });
        return true;
    }

    private static InvalidOperationException OwnedByUser(Tile tile, string propertyName, string chainName) =>
        new($"Can't save: Tiled tile {tile.ID} has its own \"{propertyName}\" property, which the animation " +
            $"\"{chainName}\" would overwrite. Remove or rename that property in Tiled, or use another tile" +
            (propertyName == TiledAnimationToAchjMapper.NamePropertyName ? " or the same name for the animation." : "."));
}
