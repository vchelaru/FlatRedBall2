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
        // Two chains in the *same* achx computing the same entry tile id is unambiguous authoring
        // error (a Tiled tile can only carry one <animation>) -- fail loudly rather than letting
        // whichever chain iterates last silently win.
        var claimedBy = new Dictionary<uint, string>();
        foreach (var result in results)
        {
            if (result.EntryTileId is not { } claimedTileId) continue;
            if (claimedBy.TryGetValue(claimedTileId, out var existingChain))
                throw new System.InvalidOperationException(
                    $"Can't sync \"{sourceLabel}\": Tiled tile {claimedTileId} would be claimed by both " +
                    $"\"{existingChain}\" and \"{result.ChainName}\". Two chains can't map to the same tile.");
            claimedBy[claimedTileId] = result.ChainName;
        }

        var previouslyTrackedTileIds = tileset.Tiles
            .Where(t => GetStringProperty(t, SourceFilePropertyName) == sourceLabel)
            .Select(t => t.ID)
            .ToHashSet();

        var newTileIds = results
            .Where(r => r.EntryTileId.HasValue)
            .Select(r => r.EntryTileId!.Value)
            .ToHashSet();

        // Built once so every lookup below is O(1) instead of an O(n) scan of tileset.Tiles per
        // stale tile cleared and per chain applied -- matters on a tileset with thousands of
        // tiles. Kept in sync whenever a brand-new tile is added below; the claimedBy check above
        // already guarantees every entry tile id in this call's results is unique, so no lookup
        // ever needs to see a tile created earlier in the same call.
        var tilesById = tileset.Tiles.ToDictionary(t => t.ID);

        var changed = false;

        foreach (var staleTileId in previouslyTrackedTileIds.Except(newTileIds))
            if (ClearTile(tilesById[staleTileId]))
                changed = true;

        var appliedCount = 0;
        var warnings = new List<string>();
        foreach (var result in results)
        {
            warnings.AddRange(result.Warnings);
            if (result.EntryTileId is not { } tileId)
                continue;

            var isNewTile = !tilesById.TryGetValue(tileId, out var tile);

            // A tile another achx source already owns (tracked by a *different* achjSourceFile)
            // is off-limits -- the class doc's "never disturbs tiles owned by a different source"
            // promise only held for the stale-clear step above; the overwrite step here had no
            // such check and would silently clobber it. Skip and warn instead of writing. The same
            // applies to a tile with an existing animation but *no* achjSourceFile at all -- a
            // human hand-authored it directly in Tiled (or some older tool wrote it), and this
            // sync has no way to know it's safe to claim, since its entry-tile-id is only ever a
            // coincidence of the achx chain's sprite-sheet geometry.
            if (!isNewTile)
            {
                var owningSource = GetStringProperty(tile!, SourceFilePropertyName);
                if (owningSource != null && owningSource != sourceLabel)
                {
                    warnings.Add($"chain \"{result.ChainName}\": tile {tileId} is already owned by \"{owningSource}\" - skipped.");
                    continue;
                }
                if (owningSource == null && tile!.Animation.Count > 0)
                {
                    warnings.Add($"chain \"{result.ChainName}\": tile {tileId} already has a hand-authored animation with no achjSourceFile - skipped.");
                    continue;
                }
            }

            if (tile == null)
            {
                tile = new Tile { ID = tileId, Width = 0, Height = 0 };
                tileset.Tiles.Add(tile);
                tilesById[tileId] = tile;
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

    /// <summary>Sets a tile's string property, returning whether the value actually changed. Also
    /// removes any existing property with the same name that isn't a <see cref="StringProperty"/>
    /// (e.g. a hand-authored int-typed "Name") -- matching only by name+type would leave it in
    /// place and add a second, differently-typed property with the same name, which real Tiled
    /// never produces and won't round-trip cleanly.</summary>
    private static bool SetStringProperty(Tile tile, string name, string value)
    {
        var wrongType = tile.Properties.FirstOrDefault(p => p.Name == name && p is not StringProperty);
        if (wrongType != null)
            tile.Properties.Remove(wrongType);

        var existing = tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == name);
        if (existing != null)
        {
            if (existing.Value == value) return wrongType != null;
            existing.Value = value;
            return true;
        }
        tile.Properties.Add(new StringProperty { Name = name, Value = value });
        return true;
    }
}
