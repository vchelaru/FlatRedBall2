using DotTiled;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>Outcome of <see cref="TilesetAnimationSync.Apply"/>: how many chains were applied to
/// a tile, plus every mapping warning collected along the way.</summary>
public sealed record TilesetAnimationSyncResult(int AppliedCount, IReadOnlyList<string> Warnings);

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

        foreach (var staleTileId in previouslyTrackedTileIds.Except(newTileIds))
            ClearTile(tileset.Tiles.Single(t => t.ID == staleTileId));

        var appliedCount = 0;
        var warnings = new List<string>();
        foreach (var result in results)
        {
            warnings.AddRange(result.Warnings);
            if (result.EntryTileId is not { } tileId)
                continue;

            var tile = tileset.Tiles.FirstOrDefault(t => t.ID == tileId);
            if (tile == null)
            {
                tile = new Tile { ID = tileId, Width = 0, Height = 0 };
                tileset.Tiles.Add(tile);
            }

            tile.Animation = result.Frames.Select(f => new Frame { TileID = f.TileId, Duration = f.Duration }).ToList();
            SetStringProperty(tile, AnimationNamePropertyName, result.ChainName);
            SetStringProperty(tile, SourceFilePropertyName, sourceLabel);
            appliedCount++;
        }

        tileset.Tiles.Sort((a, b) => a.ID.CompareTo(b.ID));
        return new TilesetAnimationSyncResult(appliedCount, warnings);
    }

    private static void ClearTile(Tile tile)
    {
        tile.Animation = [];
        tile.Properties.RemoveAll(p => p.Name is AnimationNamePropertyName or SourceFilePropertyName);
    }

    private static string? GetStringProperty(Tile tile, string name) =>
        tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == name)?.Value;

    private static void SetStringProperty(Tile tile, string name, string value)
    {
        var existing = tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == name);
        if (existing != null)
            existing.Value = value;
        else
            tile.Properties.Add(new StringProperty { Name = name, Value = value });
    }
}
