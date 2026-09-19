using DotTiled;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>Outcome of <see cref="TilesetAnimationSync.Apply"/>: how many chains were applied to
/// a tile, every mapping warning collected along the way, and whether the tileset was actually
/// mutated (a tile's animation/properties changed, or a stale tile was cleared) -- callers use
/// <see cref="Changed"/> to skip rewriting the .tsx file when a re-sync found nothing to change.</summary>
public sealed record TilesetAnimationSyncResult(int AppliedCount, IReadOnlyList<string> Warnings, bool Changed);

/// <summary>
/// Applies <see cref="AchjToTiledAnimationMapper.Map"/> results onto a <see cref="Tileset"/>'s
/// tiles, source-scoped so re-syncing from the same achx/achj never disturbs tiles owned by a
/// different source, and so a chain that's since been removed or renamed away from its tile gets
/// that tile's stale animation and tracking properties cleared rather than left behind -- the gap
/// the old pull-based Tiled scripting extension had, and the reason it was replaced by this push
/// model entirely (issue #1133).
/// </summary>
public static class TilesetAnimationSync
{
    /// <summary>Tile property recording which achx/achj chain populated a tile's animation.</summary>
    public const string AnimationNamePropertyName = "achjAnimationName";

    /// <summary>Tile property recording which achx/achj file (relative to the .tsx's own
    /// directory) populated a tile's animation -- scopes stale-clearing to tiles this exact
    /// source previously wrote, so two different achx files targeting the same tileset don't
    /// clobber each other's tiles.</summary>
    public const string SourceFilePropertyName = "achjSourceFile";

    public static TilesetAnimationSyncResult Apply(
        Tileset tileset, IReadOnlyList<ChainMappingResult> results, string sourceLabel)
    {
        var previouslyTrackedTileIds = tileset.Tiles
            .Where(t => GetStringProperty(t, SourceFilePropertyName) == sourceLabel)
            .Select(t => t.ID)
            .ToHashSet();

        var newTileIds = results
            .Where(r => r.EntryTileId.HasValue)
            .Select(r => r.EntryTileId!.Value)
            .ToHashSet();

        var changed = false;

        foreach (var staleTileId in previouslyTrackedTileIds.Except(newTileIds))
            if (ClearTile(tileset.Tiles.Single(t => t.ID == staleTileId)))
                changed = true;

        var appliedCount = 0;
        var warnings = new List<string>();
        foreach (var result in results)
        {
            warnings.AddRange(result.Warnings);
            if (result.EntryTileId is not { } tileId)
                continue;

            var tile = tileset.Tiles.FirstOrDefault(t => t.ID == tileId);
            var isNewTile = tile == null;
            if (tile == null)
            {
                tile = new Tile { ID = tileId, Width = 0, Height = 0 };
                tileset.Tiles.Add(tile);
            }

            var newAnimation = result.Frames.Select(f => new Frame { TileID = f.TileId, Duration = f.Duration }).ToList();
            if (isNewTile || !AnimationEquals(tile.Animation, newAnimation))
            {
                tile.Animation = newAnimation;
                changed = true;
            }
            if (SetStringProperty(tile, AnimationNamePropertyName, result.ChainName)) changed = true;
            if (SetStringProperty(tile, SourceFilePropertyName, sourceLabel)) changed = true;
            appliedCount++;
        }

        tileset.Tiles.Sort((a, b) => a.ID.CompareTo(b.ID));
        return new TilesetAnimationSyncResult(appliedCount, warnings, changed);
    }

    /// <summary>Clears a stale tile's animation/tracking properties. Returns whether it actually
    /// had anything to clear -- a tile only ever enters this path because it's currently tracked
    /// (i.e. it has <see cref="SourceFilePropertyName"/> set), so in practice this is always
    /// <c>true</c>, but the check keeps the method honest rather than assuming that invariant.</summary>
    private static bool ClearTile(Tile tile)
    {
        var hadAnimation = tile.Animation.Count > 0;
        var hadTrackingProperties = tile.Properties.Any(p => p.Name is AnimationNamePropertyName or SourceFilePropertyName);
        tile.Animation = [];
        tile.Properties.RemoveAll(p => p.Name is AnimationNamePropertyName or SourceFilePropertyName);
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

    private static string? GetStringProperty(Tile tile, string name) =>
        tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == name)?.Value;

    /// <summary>Sets a tile's string property, returning whether the value actually changed.</summary>
    private static bool SetStringProperty(Tile tile, string name, string value)
    {
        var existing = tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == name);
        if (existing != null)
        {
            if (existing.Value == value) return false;
            existing.Value = value;
            return true;
        }
        tile.Properties.Add(new StringProperty { Name = name, Value = value });
        return true;
    }
}
