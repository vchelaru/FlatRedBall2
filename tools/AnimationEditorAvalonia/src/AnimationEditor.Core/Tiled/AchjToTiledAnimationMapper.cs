using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>A single mapped Tiled tile-animation frame: which tile id to show, for how long (ms).</summary>
public readonly record struct MappedFrame(uint TileId, int Duration);

/// <summary>How many frames of a chain were skipped, broken out by reason. See the "skip
/// categories" remarks on <see cref="AchjToTiledAnimationMapper.Map"/>.</summary>
public sealed record SkipCounts
{
    public int TextureMismatch { get; init; }
    public int UvMissingPixelSize { get; init; }
    public int SizeMismatch { get; init; }
    public int NotGridAligned { get; init; }
    public int FlipDropped { get; init; }
    public int NegativeCoordinate { get; init; }
    public int ColumnOutOfRange { get; init; }
    public int RowOutOfRange { get; init; }
}

/// <summary>Result of mapping one <see cref="AnimationChainSave"/> onto a tileset's tile grid.</summary>
public sealed record ChainMappingResult
{
    public required string ChainName { get; init; }
    public required IReadOnlyList<MappedFrame> Frames { get; init; }
    /// <summary>The tile id that should carry <see cref="Frames"/> as its Tiled animation, or
    /// <see langword="null"/> when every frame in the chain was skipped.</summary>
    public uint? EntryTileId { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required SkipCounts SkipCounts { get; init; }
}

/// <summary>The subset of a Tiled tileset's geometry needed to map achx/achj frame rects onto
/// tile ids. Deliberately a plain data record (not <c>DotTiled.Tileset</c> directly) so mapping
/// logic has no DotTiled dependency and stays testable without constructing one.</summary>
public sealed record TilesetAnimationInfo
{
    public required int TileWidth { get; init; }
    public required int TileHeight { get; init; }
    public required int ColumnCount { get; init; }
    /// <summary>Total number of tiles the tileset declares (Tiled's <c>tilecount</c> attribute).
    /// A partial last row is normal -- this is not necessarily <c>ColumnCount</c> times a whole
    /// number of rows -- so a computed tile id is only ever validated against this directly,
    /// never against a derived row count.</summary>
    public required int TileCount { get; init; }
    public int Margin { get; init; }
    public int TileSpacing { get; init; }
    public required string ImageFileName { get; init; }
    /// <summary>Required only when the source uses <see cref="TextureCoordinateType.UV"/> frames,
    /// to convert normalized (0-1) coordinates to pixels.</summary>
    public int? TextureWidth { get; init; }
    public int? TextureHeight { get; init; }
}

/// <summary>
/// Maps a FlatRedBall2 AnimationEditor <see cref="AnimationChainListSave"/> (.achx/.achj) onto
/// Tiled tile-animation frames (<c>{ tileId, duration }[]</c> per tile), for tileset <see
/// cref="TilesetAnimationInfo"/>. FRB2/achx-specific glue -- ported from the (now-removed) pull-based
/// Tiled scripting extension's <c>mapAchjToTiledAnimations</c>, adapted to consume the
/// already-parsed <see cref="AnimationChainListSave"/> model directly instead of re-parsing achx
/// XML/achj JSON text. Replaces that extension entirely (issue #1133): this runs as a push from
/// <c>AppCommands.SaveCurrentAnimationChainList</c> instead of a pull triggered from inside Tiled.
/// </summary>
/// <remarks>
/// A frame is skipped (excluded from the mapped result) for one of these reasons: it references a
/// different texture than the tileset's image (<see cref="SkipCounts.TextureMismatch"/>); its
/// rect doesn't match the tileset's tile size (<see cref="SkipCounts.SizeMismatch"/>); its rect
/// origin isn't aligned to the tile grid (<see cref="SkipCounts.NotGridAligned"/>); its rect origin
/// resolves to a negative column/row -- an exact negative multiple of the tile size passes the
/// grid-alignment check but would otherwise unchecked-cast to a huge bogus tile id (<see
/// cref="SkipCounts.NegativeCoordinate"/>); its rect origin resolves to a column at or past the
/// tileset's own column count, which would otherwise compute a tileId that lands on a real tile in
/// the next row instead of failing (<see cref="SkipCounts.ColumnOutOfRange"/>); its resolved tile id
/// is at or past the tileset's own <see cref="TilesetAnimationInfo.TileCount"/> -- a row past the
/// tileset's last (possibly partial) row doesn't wrap into an existing tile the way column overflow
/// does, so without this check the sync step would fabricate a brand-new out-of-range
/// <c>&lt;tile&gt;</c> element instead of failing (<see cref="SkipCounts.RowOutOfRange"/>); or it uses
/// <see cref="TextureCoordinateType.UV"/>
/// coordinates but the tileset's pixel size wasn't supplied (<see
/// cref="SkipCounts.UvMissingPixelSize"/>). A flipped frame is not skipped -- Tiled tile animation
/// frames can't flip per-frame, so the flip is dropped and tallied separately (<see
/// cref="SkipCounts.FlipDropped"/>). A tileset with non-zero margin or spacing can't be mapped at
/// all (tile-id arithmetic assumes none), so every chain comes back empty with one warning instead
/// of being scanned frame by frame.
/// </remarks>
public static class AchjToTiledAnimationMapper
{
    private const float Epsilon = 0.001f;

    /// <param name="achj">The already-parsed animation chain list (in-memory model is always UV
    /// coordinates for <c>ProjectManager.AnimationChainListSave</c>, but this also accepts Pixel).</param>
    /// <param name="tilesetInfo">The target tileset's grid geometry and image.</param>
    /// <param name="tallySkips">When <see langword="true"/>, skip/flip reasons are tallied into
    /// <see cref="ChainMappingResult.SkipCounts"/> instead of appended to <see
    /// cref="ChainMappingResult.Warnings"/> one at a time -- for a project-wide sync where most
    /// frames scanned were never meant for this tileset at all, itemizing every one would drown
    /// out warnings that are actually actionable.</param>
    public static IReadOnlyList<ChainMappingResult> Map(
        AnimationChainListSave achj, TilesetAnimationInfo tilesetInfo, bool tallySkips = false)
    {
        var results = new List<ChainMappingResult>();

        foreach (var chain in achj.AnimationChains)
        {
            if (tilesetInfo.Margin != 0 || tilesetInfo.TileSpacing != 0)
            {
                results.Add(new ChainMappingResult
                {
                    ChainName = chain.Name,
                    Frames = [],
                    EntryTileId = null,
                    Warnings = [$"chain \"{chain.Name}\": tileset has non-zero margin or spacing, which this importer can't account for when computing tile ids - skipped."],
                    SkipCounts = new SkipCounts(),
                });
                continue;
            }

            var warnings = new List<string>();
            var skipCounts = new SkipCountsBuilder();
            var frames = new List<MappedFrame>();

            for (var i = 0; i < chain.Frames.Count; i++)
            {
                var mapped = MapFrame(chain.Frames[i], achj.CoordinateType, achj.TimeMeasurementUnit,
                    tilesetInfo, warnings, skipCounts, i, chain.Name, tallySkips);
                if (mapped.HasValue)
                    frames.Add(mapped.Value);
            }

            results.Add(new ChainMappingResult
            {
                ChainName = chain.Name,
                Frames = frames,
                EntryTileId = frames.Count > 0 ? frames[0].TileId : null,
                Warnings = warnings,
                SkipCounts = skipCounts.Build(),
            });
        }

        return results;
    }

    private static MappedFrame? MapFrame(
        AnimationFrameSave frame, TextureCoordinateType coordinateType, TimeMeasurementUnit timeUnit,
        TilesetAnimationInfo tilesetInfo, List<string> warnings, SkipCountsBuilder skipCounts,
        int frameIndex, string chainName, bool tallySkips)
    {
        var label = $"chain \"{chainName}\" frame {frameIndex}";

        MappedFrame? Skip(Action<SkipCountsBuilder> tally, string message)
        {
            tally(skipCounts);
            if (!tallySkips) warnings.Add(message);
            return null;
        }

        var textureBaseName = new FilePath(frame.TextureName).NoPath;
        var tilesetBaseName = new FilePath(tilesetInfo.ImageFileName).NoPath;
        if (!string.Equals(textureBaseName, tilesetBaseName, StringComparison.OrdinalIgnoreCase))
            return Skip(s => s.TextureMismatch++,
                $"{label}: references a different texture (\"{frame.TextureName}\") than the open tileset (\"{tilesetInfo.ImageFileName}\") - skipped.");

        if (coordinateType == TextureCoordinateType.UV && (tilesetInfo.TextureWidth is null or <= 0 || tilesetInfo.TextureHeight is null or <= 0))
            return Skip(s => s.UvMissingPixelSize++,
                $"{label}: \"UV\" coordinateType needs the tileset image's pixel dimensions, which weren't available - skipped.");

        var (left, top, width, height) = FrameRectPixels(frame, coordinateType, tilesetInfo);

        if (Math.Abs(width - tilesetInfo.TileWidth) > Epsilon || Math.Abs(height - tilesetInfo.TileHeight) > Epsilon)
            return Skip(s => s.SizeMismatch++,
                $"{label}: frame rect {width}x{height} doesn't match tile size {tilesetInfo.TileWidth}x{tilesetInfo.TileHeight} - skipped.");

        if (Math.Abs(left % tilesetInfo.TileWidth) > Epsilon || Math.Abs(top % tilesetInfo.TileHeight) > Epsilon)
            return Skip(s => s.NotGridAligned++,
                $"{label}: frame rect origin ({left}, {top}) is not aligned to the tile grid - skipped.");

        if (frame.FlipHorizontal || frame.FlipVertical || frame.FlipDiagonal)
        {
            skipCounts.FlipDropped++;
            if (!tallySkips)
                warnings.Add($"{label}: uses a flip flag; Tiled tile animation frames can't flip per-frame, so the flip is dropped.");
        }

        var column = (int)Math.Round(left / tilesetInfo.TileWidth);
        var row = (int)Math.Round(top / tilesetInfo.TileHeight);

        // A left/top that's an exact negative multiple of the tile size (e.g. -16 with a 16px
        // tile) passes the grid-alignment check above (remainder is 0 -- "%" keeps the dividend's
        // sign for negative operands) yet resolves to a negative column/row. Casting that straight
        // to uint would wrap to a huge bogus tile id instead of failing gracefully.
        if (column < 0 || row < 0)
            return Skip(s => s.NegativeCoordinate++,
                $"{label}: frame rect origin ({left}, {top}) resolves to a negative column/row, which isn't a valid tile position - skipped.");

        // A column at or past the tileset's own column count would still compute a "valid"-
        // looking tileId (row * ColumnCount + column) -- just one that lands on the first tile(s)
        // of the *next* row instead of failing, silently misplacing this frame's animation onto
        // an unrelated tile.
        if (column >= tilesetInfo.ColumnCount)
            return Skip(s => s.ColumnOutOfRange++,
                $"{label}: frame rect origin ({left}, {top}) resolves to column {column}, which is past the tileset's {tilesetInfo.ColumnCount} column(s) - skipped.");

        var tileId = (uint)((row * tilesetInfo.ColumnCount) + column);

        // A row past the tileset's own tile count doesn't wrap into an existing tile the way
        // column overflow does -- it computes a tileId that simply doesn't correspond to any real
        // cell. Checking the final tileId against TileCount (rather than deriving a row bound from
        // ColumnCount) is also correct for a tileset whose last row is partial.
        if (tileId >= tilesetInfo.TileCount)
            return Skip(s => s.RowOutOfRange++,
                $"{label}: frame rect origin ({left}, {top}) resolves to tile id {tileId}, which is past the tileset's {tilesetInfo.TileCount} tile(s) - skipped.");

        return new MappedFrame(tileId, FrameDurationMs(frame.FrameLength, timeUnit));
    }

    /// <summary>Widened to <c>internal</c> so <see cref="MultiTileToTiledAnimationMapper"/> can reuse
    /// the same pixel-rect math instead of duplicating the UV-to-pixel conversion.</summary>
    internal static (float Left, float Top, float Width, float Height) FrameRectPixels(
        AnimationFrameSave frame, TextureCoordinateType coordinateType, TilesetAnimationInfo tilesetInfo)
    {
        if (coordinateType == TextureCoordinateType.UV)
        {
            var textureWidth = tilesetInfo.TextureWidth!.Value;
            var textureHeight = tilesetInfo.TextureHeight!.Value;
            return (
                frame.LeftCoordinate * textureWidth,
                frame.TopCoordinate * textureHeight,
                (frame.RightCoordinate - frame.LeftCoordinate) * textureWidth,
                (frame.BottomCoordinate - frame.TopCoordinate) * textureHeight);
        }

        return (
            frame.LeftCoordinate,
            frame.TopCoordinate,
            frame.RightCoordinate - frame.LeftCoordinate,
            frame.BottomCoordinate - frame.TopCoordinate);
    }

    /// <summary>Converts a frame's display length to milliseconds -- "Second" and "Undefined" both
    /// mean seconds (matching how the runtime treats "Undefined"), "Millisecond" passes through.
    /// Widened to <c>internal</c> so <see cref="MultiTileToTiledAnimationMapper"/> can reuse it.</summary>
    internal static int FrameDurationMs(float frameLength, TimeMeasurementUnit timeUnit) =>
        timeUnit == TimeMeasurementUnit.Millisecond
            ? (int)Math.Round(frameLength)
            : (int)Math.Round(frameLength * 1000f);

    private sealed class SkipCountsBuilder
    {
        public int TextureMismatch;
        public int UvMissingPixelSize;
        public int SizeMismatch;
        public int NotGridAligned;
        public int FlipDropped;
        public int NegativeCoordinate;
        public int ColumnOutOfRange;
        public int RowOutOfRange;

        public SkipCounts Build() => new()
        {
            TextureMismatch = TextureMismatch,
            UvMissingPixelSize = UvMissingPixelSize,
            SizeMismatch = SizeMismatch,
            NotGridAligned = NotGridAligned,
            FlipDropped = FlipDropped,
            NegativeCoordinate = NegativeCoordinate,
            ColumnOutOfRange = ColumnOutOfRange,
            RowOutOfRange = RowOutOfRange,
        };
    }
}
