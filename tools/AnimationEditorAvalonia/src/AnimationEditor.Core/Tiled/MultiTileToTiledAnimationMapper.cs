using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>One satellite tile of a multi-tile animation group: its own (fixed) tile id, the
/// per-frame tile id sequence it should carry as its own Tiled animation, and its position within
/// the footprint relative to the anchor (used to key <c>knownSatelliteTileIds</c> hints across
/// saves -- see <see cref="MultiTileToTiledAnimationMapper.Map"/>).</summary>
public sealed record TiledSatelliteMapping(uint TileId, IReadOnlyList<MappedFrame> Frames, (int Dx, int Dy) Offset);

/// <summary>Result of mapping one <see cref="AnimationChainSave"/> that may span more than one
/// tile cell per frame onto a tileset's tile grid.</summary>
public sealed record MultiTileMappingResult
{
    /// <summary>The chain this result came from, by reference -- lets a caller (<see
    /// cref="AnimationEditor.Core.ProjectManager"/>) record "this chain owns this tile id" keyed
    /// on identity instead of on <see cref="ChainName"/>, which changes on a rename.</summary>
    public required AnimationChainSave SourceChain { get; init; }
    public required string ChainName { get; init; }
    /// <summary>The entry/anchor tile's own per-frame tile id sequence (top-left cell of the footprint).</summary>
    public required IReadOnlyList<MappedFrame> AnchorFrames { get; init; }
    public uint? EntryTileId { get; init; }
    /// <summary>Empty for a single-cell (1x1) chain -- see <see cref="AchjToTiledAnimationMapper"/>
    /// remarks for why that case needs no group at all.</summary>
    public required IReadOnlyList<TiledSatelliteMapping> Satellites { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>
/// Maps an achx/achj <see cref="AnimationChainListSave"/> onto Tiled tile animations the same way
/// <see cref="AchjToTiledAnimationMapper"/> does, but also accepts frames whose rect spans an NxM
/// whole-tile footprint (not just a single cell). Used by the native <c>.tsx</c> project save path
/// (issue #1140) -- deliberately kept separate from <see cref="AchjToTiledAnimationMapper"/>, which
/// stays single-cell-only and unchanged for the existing achj-push feature.
/// </summary>
/// <remarks>
/// A multi-cell frame is represented in Tiled as a group of individually-animated tiles: the
/// footprint's top-left cell (the "anchor") carries the group's identity, and every other cell (a
/// "satellite") gets its own independent tile animation cycling through that same relative offset
/// across every frame. A satellite's own tile id is always the offset cell's id *in frame 0* --
/// i.e. exactly what that physical tile looks like at rest in the spritesheet -- so no separate
/// offset needs to be stored; <see cref="TiledAnimationToAchjMapper"/> derives it back from the
/// satellite's static grid position relative to the anchor's.
/// </remarks>
public static class MultiTileToTiledAnimationMapper
{
    private const float Epsilon = 0.001f;

    /// <param name="knownEntryTileIds">Optional identity hint from a prior <see
    /// cref="TiledAnimationToAchjMapper.Map"/> load (or a prior save -- see <see
    /// cref="AnimationEditor.Core.ProjectManager"/>'s own tracking), keyed by chain object
    /// reference. When a chain has an entry here, that tile id wins over the freshly-computed
    /// first-frame id -- without this, every save relocates any chain whose owning tile isn't its
    /// own first frame (an ordinary hand-authored Tiled pattern), orphaning the original tile.</param>
    /// <param name="knownSatelliteTileIds">The satellite equivalent of <paramref
    /// name="knownEntryTileIds"/>: each chain's satellites' own on-disk tile ids, keyed by chain
    /// reference then by the satellite's (Dx, Dy) offset within the footprint. A satellite's tile
    /// id is otherwise always recomputed as the anchor's *frame-0* position plus its offset -- a
    /// different base than the anchor's own (possibly hint-preserved) tile id whenever the anchor's
    /// id isn't its own frame-0 tile, which silently drifts the satellite to a new tile every save
    /// without this hint.</param>
    public static IReadOnlyList<MultiTileMappingResult> Map(
        AnimationChainListSave achj, TilesetAnimationInfo tilesetInfo,
        IReadOnlyDictionary<AnimationChainSave, uint>? knownEntryTileIds = null,
        IReadOnlyDictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>? knownSatelliteTileIds = null)
    {
        return achj.AnimationChains
            .Select(chain => MapChain(
                chain, achj.CoordinateType, achj.TimeMeasurementUnit, tilesetInfo, knownEntryTileIds,
                knownSatelliteTileIds != null && knownSatelliteTileIds.TryGetValue(chain, out var hints) ? hints : null))
            .ToList();
    }

    private static MultiTileMappingResult MapChain(
        AnimationChainSave chain, TextureCoordinateType coordinateType, TimeMeasurementUnit timeUnit,
        TilesetAnimationInfo tilesetInfo, IReadOnlyDictionary<AnimationChainSave, uint>? knownEntryTileIds,
        IReadOnlyDictionary<(int Dx, int Dy), uint>? knownSatelliteTileIds)
    {
        MultiTileMappingResult Empty(string? warning = null) => new()
        {
            SourceChain = chain,
            ChainName = chain.Name,
            AnchorFrames = [],
            EntryTileId = null,
            Satellites = [],
            Warnings = warning is null ? [] : [warning],
        };

        if (chain.Frames.Count == 0)
            return Empty();

        if (tilesetInfo.Margin != 0 || tilesetInfo.TileSpacing != 0)
            return Empty($"chain \"{chain.Name}\": tileset has non-zero margin or spacing, which this importer can't account for when computing tile ids - skipped.");

        var firstRect = AchjToTiledAnimationMapper.FrameRectPixels(chain.Frames[0], coordinateType, tilesetInfo);
        var footprintColumns = (int)Math.Round(firstRect.Width / tilesetInfo.TileWidth);
        var footprintRows = (int)Math.Round(firstRect.Height / tilesetInfo.TileHeight);

        if (footprintColumns < 1 || footprintRows < 1
            || Math.Abs(firstRect.Width - (footprintColumns * tilesetInfo.TileWidth)) > Epsilon
            || Math.Abs(firstRect.Height - (footprintRows * tilesetInfo.TileHeight)) > Epsilon)
            return Empty($"chain \"{chain.Name}\": frame size {firstRect.Width}x{firstRect.Height} isn't a whole number of tiles - skipped.");

        var perOffset = new Dictionary<(int Dx, int Dy), List<MappedFrame>>();
        for (var dy = 0; dy < footprintRows; dy++)
            for (var dx = 0; dx < footprintColumns; dx++)
                perOffset[(dx, dy)] = [];

        var tilesetBaseName = new FilePath(tilesetInfo.ImageFileName).NoPath;

        foreach (var frame in chain.Frames)
        {
            var textureBaseName = new FilePath(frame.TextureName).NoPath;
            if (!string.Equals(textureBaseName, tilesetBaseName, StringComparison.OrdinalIgnoreCase))
                return Empty($"chain \"{chain.Name}\": references a different texture (\"{frame.TextureName}\") than the open tileset (\"{tilesetInfo.ImageFileName}\") - skipped.");

            var rect = AchjToTiledAnimationMapper.FrameRectPixels(frame, coordinateType, tilesetInfo);

            if (Math.Abs(rect.Width - (footprintColumns * tilesetInfo.TileWidth)) > Epsilon
                || Math.Abs(rect.Height - (footprintRows * tilesetInfo.TileHeight)) > Epsilon)
                return Empty($"chain \"{chain.Name}\": frame size {rect.Width}x{rect.Height} doesn't match the chain's {footprintColumns}x{footprintRows}-tile footprint - skipped.");

            if (Math.Abs(rect.Left % tilesetInfo.TileWidth) > Epsilon || Math.Abs(rect.Top % tilesetInfo.TileHeight) > Epsilon)
                return Empty($"chain \"{chain.Name}\": frame rect origin ({rect.Left}, {rect.Top}) is not aligned to the tile grid - skipped.");

            var duration = AchjToTiledAnimationMapper.FrameDurationMs(frame.FrameLength, timeUnit);
            var originColumn = (int)Math.Round(rect.Left / tilesetInfo.TileWidth);
            var originRow = (int)Math.Round(rect.Top / tilesetInfo.TileHeight);

            // An exact negative multiple of the tile size (e.g. -16 with a 16px tile) passes the
            // grid-alignment check above (remainder is 0) yet resolves to a negative
            // column/row -- an unchecked cast to uint below would wrap to a huge bogus tile id.
            if (originColumn < 0 || originRow < 0)
                return Empty($"chain \"{chain.Name}\": frame rect origin ({rect.Left}, {rect.Top}) resolves to a negative column/row, which isn't a valid tile position - skipped.");

            // A footprint whose right-hand cell would need a column at or past the tileset's own
            // column count still computes a "valid"-looking tileId for that cell -- it just lands
            // on a real tile in the *next* row instead of failing, silently misplacing a satellite
            // onto an unrelated tile every save.
            if (originColumn + footprintColumns > tilesetInfo.ColumnCount)
                return Empty($"chain \"{chain.Name}\": frame rect origin ({rect.Left}, {rect.Top}) with a {footprintColumns}x{footprintRows}-tile footprint would extend past the tileset's {tilesetInfo.ColumnCount} column(s) - skipped.");

            for (var dy = 0; dy < footprintRows; dy++)
                for (var dx = 0; dx < footprintColumns; dx++)
                {
                    var tileId = (uint)(((originRow + dy) * tilesetInfo.ColumnCount) + (originColumn + dx));
                    perOffset[(dx, dy)].Add(new MappedFrame(tileId, duration));
                }
        }

        var anchorFrames = perOffset[(0, 0)];
        var satellites = perOffset
            .Where(kv => kv.Key != (0, 0))
            .Select(kv =>
            {
                var tileId = knownSatelliteTileIds != null && knownSatelliteTileIds.TryGetValue(kv.Key, out var known)
                    ? known
                    : kv.Value[0].TileId;
                return new TiledSatelliteMapping(tileId, kv.Value, kv.Key);
            })
            .ToList();

        uint? entryTileId = null;
        if (anchorFrames.Count > 0)
            entryTileId = knownEntryTileIds != null && knownEntryTileIds.TryGetValue(chain, out var known)
                ? known
                : anchorFrames[0].TileId;

        return new MultiTileMappingResult
        {
            SourceChain = chain,
            ChainName = chain.Name,
            AnchorFrames = anchorFrames,
            EntryTileId = entryTileId,
            Satellites = satellites,
            Warnings = [],
        };
    }
}
