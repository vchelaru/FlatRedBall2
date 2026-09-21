using DotTiled;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Maps a Tiled <see cref="Tileset"/>'s per-tile animations onto an <see
/// cref="AnimationChainListSave"/> — the reverse of <see cref="AchjToTiledAnimationMapper"/>, used
/// to open a <c>.tsx</c> as a native AnimationEditor project (issue #1140) instead of pushing an
/// achx/achj onto an associated tileset.
/// </summary>
/// <remarks>
/// One animated tile with no <see cref="ParentIdPropertyName"/> property becomes one chain (an
/// "anchor"). A tile carrying a <see cref="ParentIdPropertyName"/> property that resolves to
/// another animated tile is a multi-tile-group "satellite" -- it is folded into its anchor's chain
/// as part of a wider per-frame rect instead of becoming its own chain; the satellite's own
/// <c>Animation</c> frames are not read here, only its static grid position relative to the anchor
/// (see <see cref="MultiTileToTiledAnimationMapper"/> for why that's safe to trust). A
/// <see cref="ParentIdPropertyName"/> that does *not* resolve to a true (unchained) anchor at a
/// forward (right/below) offset -- it doesn't resolve to any animated tile (typo, hand-edit
/// mistake, or the anchor was separately deleted), it resolves to a tile that is itself a
/// satellite (chained/nested ParentId), or it points at an anchor with a larger column/row than
/// the satellite itself (backward offset -- a footprint shape AnimationEditor's own UI never
/// produces) -- is treated as an anchor of its own rather than dropped -- <see
/// cref="TsxAnimationValidator"/> still flags the dangling/chained/backward reference as a
/// warning, but the tile's own animation data is never silently unrecoverable. An anchor's
/// satellites are only folded in as a group when they collectively fill every cell of the
/// rectangle their bounding box implies; a gap (AnimationEditor's own UI never writes one) means
/// every satellite in that group is treated as its own independent anchor instead, since taking
/// the bounding box at face value would silently claim the missing cell's tile on the next save.
/// A chain's name is the tile's
/// <see cref="NamePropertyName"/> property when present, otherwise a synthetic <c>"ID:{tileId}"</c>
/// label.
/// </remarks>
public static class TiledAnimationToAchjMapper
{
    /// <summary>Custom tile property AnimationEditor writes/reads for a user-assigned animation name.</summary>
    public const string NamePropertyName = "Name";

    /// <summary>Custom tile property marking a tile as a multi-tile-group satellite, valued with its anchor tile's id.</summary>
    public const string ParentIdPropertyName = "ParentId";

    /// <summary>
    /// Builds the achx-shaped model in UV (0-1) coordinates and Second-based durations -- the
    /// same invariant every achx load produces (see <c>ProjectManager.NormalizeCoordinatesToUv</c>'s
    /// doc comment: the in-memory representation is always UV). Storing raw pixel values here
    /// with <see cref="TextureCoordinateType.Pixel"/> instead was a real shipped bug (issue #1140
    /// follow-up): nothing downstream treats <c>Pixel</c> as "already converted, leave it alone,"
    /// so every consumer re-multiplied an already-in-pixels value by the texture size again (a
    /// 200ms duration rendered as "200 seconds"; a 864px coordinate rendered as 864*2048).
    /// </summary>
    /// <param name="entryTileIdsByChain">Every returned chain's own source tile id, keyed by
    /// chain object reference. A native-tsx save (<see cref="MultiTileToTiledAnimationMapper"/>)
    /// must feed this back in as its <c>knownEntryTileIds</c> so a chain whose owning tile id
    /// doesn't match its own first frame -- a perfectly ordinary hand-authored Tiled pattern --
    /// keeps writing to the same tile instead of relocating (and orphaning the original tile)
    /// every save. Keyed by reference, not name, so a rename doesn't look like delete+create.</param>
    /// <param name="satelliteTileIdsByChain">The satellite equivalent of <paramref
    /// name="entryTileIdsByChain"/>: each chain's satellites' own source tile ids, keyed by chain
    /// reference then by the satellite's (Dx, Dy) offset from the anchor's *static* grid position.
    /// A native-tsx save must feed this back in as <see
    /// cref="MultiTileToTiledAnimationMapper"/>'s <c>knownSatelliteTileIds</c> for the same reason
    /// as <paramref name="entryTileIdsByChain"/> -- a satellite's tile id is otherwise always
    /// recomputed relative to the anchor's *frame-0* position, a different base whenever the
    /// anchor's own id isn't its own frame-0 tile, which silently drifts the satellite to a new
    /// tile every save.</param>
    public static AnimationChainListSave Map(
        Tileset tileset,
        out IReadOnlyDictionary<AnimationChainSave, uint> entryTileIdsByChain,
        out IReadOnlyDictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>> satelliteTileIdsByChain)
    {
        // Every tile-position computation below is "% columns" / "/ columns" -- a corrupt/hand-
        // edited tsx with Columns <= 0 would either divide by zero (Columns == 0) or unchecked-cast
        // a negative value into a huge uint, silently misplacing every tile instead of failing
        // loudly. A real Tiled-authored tsx always has Columns >= 1.
        if (tileset.Columns <= 0)
            throw new InvalidOperationException(
                $"Can't map tile animations: tileset \"{tileset.Name}\" has Columns={tileset.Columns}, which isn't a valid tile-grid width.");

        var imageFileName = tileset.Image.HasValue ? (tileset.Image.Value.Source.HasValue ? tileset.Image.Value.Source.Value : string.Empty) : string.Empty;
        var (textureWidth, textureHeight) = GetTextureSize(tileset);
        var acls = new AnimationChainListSave();
        var entryTileIds = new Dictionary<AnimationChainSave, uint>(ReferenceEqualityComparer.Instance);
        var satelliteTileIds = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>(ReferenceEqualityComparer.Instance);

        // A tile carrying TilesetAnimationSync.SourceFilePropertyName ("achjSourceFile") was
        // written by the achx-push feature (issue #1133) -- a completely separate save pipeline
        // that never touches Name/ParentId and independently re-syncs this tile from its own
        // achx/achj, regardless of whether this same .tsx is also open as a native project.
        // Folding it into this project's own editable model (as it used to be, since any animated
        // tile qualified) would let a user rename/move/delete an animation this project doesn't
        // own, and would leave its achjSourceFile/achjAnimationName tracking properties orphaned
        // -- or fought over -- the next time either feature saves. Excluded entirely rather than
        // surfaced as a chain, unlike every other "not a real anchor" case in this method: those
        // are all broken references to a tile this project *does* own, whereas this tile belongs
        // to a different owner outright.
        var animatedTiles = tileset.Tiles
            .Where(t => t.Animation.Count > 0 && !IsAchxPushOwned(t))
            .ToList();
        var parentIdByTileId = new Dictionary<uint, uint?>();
        foreach (var tile in animatedTiles)
            if (!parentIdByTileId.TryAdd(tile.ID, GetParentId(tile)))
                throw new InvalidOperationException(
                    $"Can't map tile animations: tileset \"{tileset.Name}\" has more than one animated tile with id {tile.ID}, which isn't valid Tiled data.");
        var trueAnchorTileIds = animatedTiles.Where(t => !parentIdByTileId[t.ID].HasValue).Select(t => t.ID).ToHashSet();

        var columns = (uint)tileset.Columns;

        // A ParentId pointing "backward" -- to an anchor with a larger column or row than the
        // satellite's own -- has no corresponding multi-tile-group shape AnimationEditor's own UI
        // could ever produce (a footprint only ever grows right/down from its anchor). Treating it
        // as a real satellite would underflow the uint dx/dy subtraction below, wrapping
        // footprintColumns/footprintRows back to their 1x1 default and silently excluding the tile
        // from the anchor's mapped frame rect instead of surfacing it.
        bool IsBackwardOffset(uint anchorId, uint satelliteId) =>
            (satelliteId % columns) < (anchorId % columns) || (satelliteId / columns) < (anchorId / columns);

        // A tile whose ParentId doesn't resolve to a true (unchained) anchor -- either it doesn't
        // resolve to any animated tile at all (typo, hand-edit mistake, or the anchor's own
        // animation was separately deleted), it resolves to a tile that is itself a satellite (a
        // chained/nested ParentId, i.e. satellite-of-a-satellite), or its offset from that anchor
        // would be backward -- is treated as an anchor of its own rather than dropped/miscomputed.
        // TsxAnimationValidator already flags each of these broken-reference cases as a warning,
        // but the tile's animation data must still survive into the editable model so it isn't
        // silently unrecoverable on save.
        bool IsAnchor(Tile t) => parentIdByTileId[t.ID] is not { } parentId
            || !trueAnchorTileIds.Contains(parentId)
            || IsBackwardOffset(parentId, t.ID);

        var tentativeSatellitesByAnchor = animatedTiles
            .Where(t => !IsAnchor(t))
            .ToLookup(t => parentIdByTileId[t.ID]!.Value);

        // An anchor's satellites are only trustworthy as a group when they collectively fill
        // every cell of the rectangle their own bounding box implies -- AnimationEditor's own UI
        // only ever writes a fully-populated NxM footprint, so a gap (e.g. one satellite's
        // ParentId hand-deleted without updating the others) is only reachable by hand-editing.
        // Each existing satellite in a gappy group is individually valid on its own terms
        // (forward offset, resolves to a true anchor, own frames in lockstep) so none of the
        // per-satellite checks above or in TsxAnimationValidator catch this -- only looking at the
        // group as a whole does. Taking the bounding box at face value here would silently claim
        // the missing cell's tile into this chain's footprint on the very next save (see
        // NativeTsxAnimationSync.Apply / MultiTileToTiledAnimationMapper, which always fill every
        // cell of the computed footprint), corrupting a tile that was never part of any group.
        var incompleteAnchorIds = new HashSet<uint>();
        foreach (var group in tentativeSatellitesByAnchor)
        {
            var anchorCol = group.Key % columns;
            var anchorRow = group.Key / columns;
            var offsets = new HashSet<(uint Dx, uint Dy)>();
            var footprintColumns = 1u;
            var footprintRows = 1u;
            foreach (var satellite in group)
            {
                var dx = (satellite.ID % columns) - anchorCol;
                var dy = (satellite.ID / columns) - anchorRow;
                footprintColumns = System.Math.Max(footprintColumns, dx + 1);
                footprintRows = System.Math.Max(footprintRows, dy + 1);
                offsets.Add((dx, dy));
            }

            for (var dy = 0u; dy < footprintRows; dy++)
                for (var dx = 0u; dx < footprintColumns; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (!offsets.Contains((dx, dy)))
                        incompleteAnchorIds.Add(group.Key);
                }
        }

        bool IsEffectiveAnchor(Tile t) => IsAnchor(t)
            || (parentIdByTileId[t.ID] is { } parentId && incompleteAnchorIds.Contains(parentId));

        var satellitesByAnchor = animatedTiles
            .Where(t => !IsEffectiveAnchor(t))
            .ToLookup(t => parentIdByTileId[t.ID]!.Value);

        foreach (var anchor in animatedTiles.Where(IsEffectiveAnchor))
        {
            var chain = new AnimationChainSave { Name = ChainName(anchor) };

            var anchorCol = anchor.ID % columns;
            var anchorRow = anchor.ID / columns;

            var footprintColumns = 1u;
            var footprintRows = 1u;
            var satelliteTileIdsForChain = new Dictionary<(int Dx, int Dy), uint>();
            foreach (var satellite in satellitesByAnchor[anchor.ID])
            {
                var dx = (satellite.ID % columns) - anchorCol;
                var dy = (satellite.ID / columns) - anchorRow;
                footprintColumns = System.Math.Max(footprintColumns, dx + 1);
                footprintRows = System.Math.Max(footprintRows, dy + 1);
                satelliteTileIdsForChain[((int)dx, (int)dy)] = satellite.ID;
            }

            foreach (var frame in anchor.Animation)
            {
                var col = frame.TileID % columns;
                var row = frame.TileID / columns;

                chain.Frames.Add(new AnimationFrameSave
                {
                    TextureName = imageFileName,
                    LeftCoordinate = (col * tileset.TileWidth) / (float)textureWidth,
                    TopCoordinate = (row * tileset.TileHeight) / (float)textureHeight,
                    RightCoordinate = ((col + footprintColumns) * tileset.TileWidth) / (float)textureWidth,
                    BottomCoordinate = ((row + footprintRows) * tileset.TileHeight) / (float)textureHeight,
                    FrameLength = frame.Duration / 1000f,
                });
            }

            acls.AnimationChains.Add(chain);
            entryTileIds[chain] = anchor.ID;
            if (satelliteTileIdsForChain.Count > 0)
                satelliteTileIds[chain] = satelliteTileIdsForChain;
        }

        entryTileIdsByChain = entryTileIds;
        satelliteTileIdsByChain = satelliteTileIds;
        return acls;
    }

    /// <summary>
    /// The image's own pixel dimensions when the tsx records them (as every real Tiled-authored
    /// file does), otherwise the size implied by the tile grid itself
    /// (<c>Columns * TileWidth</c> by however many rows <c>TileCount</c> needs) -- keeps UV
    /// conversion well-defined instead of dividing by a missing/zero size.
    /// </summary>
    private static (int Width, int Height) GetTextureSize(Tileset tileset)
    {
        if (tileset.Image.HasValue)
        {
            var image = tileset.Image.Value;
            if (image.Width.HasValue && image.Height.HasValue && image.Width.Value > 0 && image.Height.Value > 0)
                return (image.Width.Value, image.Height.Value);
        }

        var rows = (tileset.TileCount + tileset.Columns - 1) / tileset.Columns;
        return (tileset.Columns * tileset.TileWidth, rows * tileset.TileHeight);
    }

    /// <summary>Whether the achx-push feature (issue #1133) -- not this native-tsx project --
    /// currently owns this tile's animation, per <see cref="TilesetAnimationSync.SourceFilePropertyName"/>.</summary>
    private static bool IsAchxPushOwned(Tile tile) =>
        tile.Properties.OfType<StringProperty>().Any(p => p.Name == TilesetAnimationSync.SourceFilePropertyName);

    private static string ChainName(Tile tile)
    {
        var name = tile.Properties.OfType<StringProperty>().FirstOrDefault(p => p.Name == NamePropertyName)?.Value;
        return string.IsNullOrEmpty(name) ? $"ID:{tile.ID}" : name;
    }

    /// <summary>A negative <c>ParentId</c> (hand-edited or corrupt file) is treated the same as a
    /// missing one rather than unchecked-cast into a huge <see cref="uint"/> -- silently wrapping
    /// -1 into 4294967295 would send lookups into nonsense territory instead of failing loudly.</summary>
    internal static uint? GetParentId(Tile tile)
    {
        var property = tile.Properties.FirstOrDefault(p => p.Name == ParentIdPropertyName);
        return property switch
        {
            IntProperty { Value: >= 0 } intProperty => (uint)intProperty.Value,
            _ => null,
        };
    }
}
