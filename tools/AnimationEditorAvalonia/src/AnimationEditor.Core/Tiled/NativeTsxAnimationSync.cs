using DotTiled;
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
    public static NativeTsxAnimationSyncResult Apply(Tileset tileset, IReadOnlyList<MultiTileMappingResult> results)
    {
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
            if (ClearTile(tileset.Tiles.Single(t => t.ID == staleTileId)))
                changed = true;

        foreach (var result in results)
        {
            if (result.EntryTileId is not { } entryTileId)
                continue;

            if (ApplyTile(tileset, entryTileId, result.AnchorFrames, explicitName: SyntheticName(entryTileId) == result.ChainName ? null : result.ChainName))
                changed = true;

            foreach (var satellite in result.Satellites)
                if (ApplyTile(tileset, satellite.TileId, satellite.Frames, explicitName: null, parentId: entryTileId))
                    changed = true;
        }

        tileset.Tiles.Sort((a, b) => a.ID.CompareTo(b.ID));
        return new NativeTsxAnimationSyncResult(changed);
    }

    private static string SyntheticName(uint tileId) => $"ID:{tileId}";

    private static bool ApplyTile(
        Tileset tileset, uint tileId, IReadOnlyList<MappedFrame> frames, string? explicitName, uint? parentId = null)
    {
        var tile = tileset.Tiles.FirstOrDefault(t => t.ID == tileId);
        var isNewTile = tile == null;
        if (tile == null)
        {
            tile = new Tile { ID = tileId, Width = 0, Height = 0 };
            tileset.Tiles.Add(tile);
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

    private static bool SetOrRemoveStringProperty(Tile tile, string name, string? value)
    {
        var existing = tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == name);
        if (value is null)
        {
            if (existing is null) return false;
            tile.Properties.Remove(existing);
            return true;
        }

        if (existing != null)
        {
            if (existing.Value == value) return false;
            existing.Value = value;
            return true;
        }

        tile.Properties.Add(new StringProperty { Name = name, Value = value });
        return true;
    }

    private static bool SetOrRemoveIntProperty(Tile tile, string name, int? value)
    {
        var existing = tile.Properties.OfType<IntProperty>().FirstOrDefault(p => p.Name == name);
        if (value is null)
        {
            if (existing is null) return false;
            tile.Properties.Remove(existing);
            return true;
        }

        if (existing != null)
        {
            if (existing.Value == value.Value) return false;
            existing.Value = value.Value;
            return true;
        }

        tile.Properties.Add(new IntProperty { Name = name, Value = value.Value });
        return true;
    }
}
