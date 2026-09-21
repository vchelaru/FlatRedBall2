using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class MultiTileToTiledAnimationMapperTests
{
    // 4 columns, 16x16 tiles.
    private static readonly TilesetAnimationInfo TilesetInfo = new()
    {
        TileWidth = 16,
        TileHeight = 16,
        ColumnCount = 4,
        TileCount = 16,
        ImageFileName = "Heroes.png",
    };

    private static AnimationChainListSave AchjWithChain(string chainName, params AnimationFrameSave[] frames)
    {
        var save = new AnimationChainListSave
        {
            CoordinateType = TextureCoordinateType.Pixel,
            TimeMeasurementUnit = TimeMeasurementUnit.Millisecond,
        };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.AddRange(frames);
        save.AnimationChains.Add(chain);
        return save;
    }

    private static AnimationFrameSave PixelFrame(float left, float top, float right, float bottom, float frameLength = 100f) => new()
    {
        TextureName = "Heroes.png",
        LeftCoordinate = left,
        TopCoordinate = top,
        RightCoordinate = right,
        BottomCoordinate = bottom,
        FrameLength = frameLength,
    };

    [Fact]
    public void Map_SingleTileFrame_ProducesAnchorOnlyNoSatellites()
    {
        var achj = AchjWithChain("Idle", PixelFrame(0, 0, 16, 16));

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo);

        var result = Assert.Single(results);
        Assert.Empty(result.Satellites);
        Assert.Equal((uint)0, result.EntryTileId);
    }

    [Fact]
    public void Map_TwoTileWideFrame_ProducesAnchorAndOneSatelliteWithMatchingSequence()
    {
        // Frame spans columns 0-1 (32px wide) at row 0, then columns 1-2 at row 0.
        var achj = AchjWithChain("Walk",
            PixelFrame(0, 0, 32, 16),
            PixelFrame(16, 0, 48, 16));

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo);

        var result = Assert.Single(results);
        Assert.Equal([new MappedFrame(0, 100), new MappedFrame(1, 100)], result.AnchorFrames);
        Assert.Equal((uint)0, result.EntryTileId);

        var satellite = Assert.Single(result.Satellites);
        // Satellite's own identity tile is frame 0's right-hand cell: column 1, row 0 -> tile id 1.
        Assert.Equal((uint)1, satellite.TileId);
        Assert.Equal([new MappedFrame(1, 100), new MappedFrame(2, 100)], satellite.Frames);
    }

    [Fact]
    public void Map_MarginAndSpacing_TwoTileWideFrameIncludingTheGap_ProducesAnchorAndSatellite()
    {
        // Margin 2, spacing 1: column 0 at x=2..18, column 1 at x=19..35. A 2x1 frame is one
        // contiguous rect from 2 to 35 (33px) that includes the 1px gap at x=18.
        var achj = AchjWithChain("Walk", PixelFrame(2, 2, 35, 18));
        var spacedTilesetInfo = TilesetInfo with { Margin = 2, TileSpacing = 1 };

        var results = MultiTileToTiledAnimationMapper.Map(achj, spacedTilesetInfo);

        var result = Assert.Single(results);
        Assert.Empty(result.Warnings);
        Assert.Equal((uint)0, result.EntryTileId);
        Assert.Equal((uint)1, Assert.Single(result.Satellites).TileId);
    }

    [Fact]
    public void Map_MarginAndSpacing_TwoTileWideFrameWithoutTheGap_SkipsAsNotWholeTiles()
    {
        // 32px wide = two 16px cells butted together, which no spaced footprint ever is (2 cells = 33px).
        var achj = AchjWithChain("Walk", PixelFrame(2, 2, 34, 18));
        var spacedTilesetInfo = TilesetInfo with { Margin = 2, TileSpacing = 1 };

        var results = MultiTileToTiledAnimationMapper.Map(achj, spacedTilesetInfo);

        Assert.Contains("whole number of tiles", Assert.Single(results).Warnings[0]);
    }

    [Fact]
    public void Map_NegativeAlignedFrameOrigin_SkipsChainAndWarnsInsteadOfUncheckedCastToHugeTileId()
    {
        // Left=-16 is an exact multiple of tile width 16 (remainder 0), so it isn't caught by the
        // grid-alignment check, but resolves to column -1 -- an unchecked cast to uint would wrap
        // to 4294967295 for the anchor tile id.
        var achj = AchjWithChain("Corrupt", PixelFrame(-16, 0, 0, 16));

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].AnchorFrames);
        Assert.Contains("negative", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_FrameFootprintExtendsPastTilesetRightEdge_SkipsChainAndWarnsInsteadOfWrappingIntoNextRow()
    {
        // Columns=4, footprint is 2 tiles wide, anchored at column 3 (the last column) -- the
        // footprint's right-hand cell would need column 4, which doesn't exist in this row.
        // originColumn(3) + dx(1) = 4 == ColumnCount computes a tileId that lands on a real tile
        // (the first tile of the next row) instead of failing, silently misplacing the satellite.
        var achj = AchjWithChain("BadEdge", PixelFrame(48, 0, 80, 16));

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].AnchorFrames);
        Assert.Contains("column", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_FootprintBottomRowBeyondTilesetTileCount_SkipsChainAndWarnsInsteadOfFabricatingOutOfRangeTile()
    {
        // 1-wide x 2-tall footprint anchored at row 3, column 0 (TileCount=16 -> valid rows are
        // 0-3, ids 0-15). The origin cell (row 3, id 12) is in range, but the footprint's bottom
        // cell is row 4 -> id 4*4+0 = 16, at/past TileCount. Column bound alone can't catch this --
        // it only checks originColumn+footprintColumns against ColumnCount, never the row axis.
        var achj = AchjWithChain("BadBottomEdge", PixelFrame(0, 48, 16, 80));

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].AnchorFrames);
        Assert.Contains("tile(s)", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_FrameSizeNotWholeMultipleOfTile_SkipsChainAndWarns()
    {
        var achj = AchjWithChain("Bad", PixelFrame(0, 0, 20, 16));

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].AnchorFrames);
        Assert.Contains("whole number of tiles", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_MappingFailsButHasKnownEntryHint_StillReportsHintedEntryTileIdSoItIsNotOrphaned()
    {
        // NativeTsxAnimationSync.Apply only protects a tile id from stale-clearing when it shows
        // up in this save's results -- a failed chain (e.g. a resize that lands on a size that
        // isn't a whole number of tiles) must keep reporting its last-known entry tile id from the
        // save's identity hint, or the previously-working animation on that tile gets deleted as a
        // side effect of the failed edit.
        var achj = AchjWithChain("Bad", PixelFrame(0, 0, 20, 16));
        var chain = achj.AnimationChains[0];
        var hints = new Dictionary<AnimationChainSave, uint> { [chain] = 5 };

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo, hints);

        var result = Assert.Single(results);
        Assert.Empty(result.AnchorFrames);
        Assert.NotEmpty(result.Warnings);
        Assert.Equal((uint)5, result.EntryTileId);
    }

    [Fact]
    public void Map_MappingFailsButHasKnownSatelliteHints_StillReportsHintedSatelliteTileIds()
    {
        var achj = AchjWithChain("Bad", PixelFrame(0, 0, 20, 16));
        var chain = achj.AnimationChains[0];
        var entryHints = new Dictionary<AnimationChainSave, uint> { [chain] = 0 };
        var satelliteHints = new Dictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>
        {
            [chain] = new Dictionary<(int, int), uint> { [(1, 0)] = 1 },
        };

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo, entryHints, satelliteHints);

        var result = Assert.Single(results);
        var satellite = Assert.Single(result.Satellites);
        Assert.Equal((uint)1, satellite.TileId);
    }

    [Fact]
    public void Map_ChainGenuinelyEmpty_NoWarningAndNoHintedEntryTileId()
    {
        // Distinguishes "mapping failed, protect the old tile" (above) from "the chain
        // intentionally has no frames anymore, the old tile really is stale now."
        var achj = AchjWithChain("Empty");
        var chain = achj.AnimationChains[0];
        var hints = new Dictionary<AnimationChainSave, uint> { [chain] = 5 };

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo, hints);

        var result = Assert.Single(results);
        Assert.Empty(result.Warnings);
        Assert.Null(result.EntryTileId);
    }

    // ── Fresh satellites sit next to the ENTRY tile, not next to the frames ──────────────────
    // TiledAnimationToAchjMapper reads a satellite's offset from the anchor tile's static grid
    // position, so a hand-authored owner tile unrelated to its frames needs its new satellites
    // placed beside the owner, or they come back as unattached anchors on the next load.

    [Fact]
    public void Map_EntryHintUnrelatedToFrames_FreshSatelliteSitsNextToEntryTile()
    {
        // Frame is cols 0-1 at row 0 (tiles 0, 1); the owner tile is 8 (row 2, col 0).
        var achj = AchjWithChain("Walk", PixelFrame(0, 0, 32, 16));
        var hints = new Dictionary<AnimationChainSave, uint> { [achj.AnimationChains[0]] = 8 };

        var result = Assert.Single(MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo, hints));

        var satellite = Assert.Single(result.Satellites);
        Assert.Equal((uint)9, satellite.TileId);
        Assert.Equal([new MappedFrame(1, 100)], satellite.Frames);
    }

    [Fact]
    public void Map_EntryHintInLastColumn_TwoWideFrame_SkipsChainAndWarnsInsteadOfWrappingIntoNextRow()
    {
        // Frame is cols 0-1 at row 0; the owner tile is 3 (row 0, last col), so its (1,0)
        // satellite has no column to land in.
        var achj = AchjWithChain("Walk", PixelFrame(0, 0, 32, 16));
        var hints = new Dictionary<AnimationChainSave, uint> { [achj.AnimationChains[0]] = 3 };

        var result = Assert.Single(MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo, hints));

        Assert.Empty(result.AnchorFrames);
        Assert.Contains("owner tile 3", Assert.Single(result.Warnings));
    }

    [Fact]
    public void Map_EntryHintInLastRow_TwoTallFrame_SkipsChainAndWarnsInsteadOfFabricatingOutOfRangeTile()
    {
        // Frame is rows 0-1 at col 0; the owner tile is 12 (last row), so its (0,1) satellite
        // would be tile 16, past the 16-tile tileset.
        var achj = AchjWithChain("Walk", PixelFrame(0, 0, 16, 32));
        var hints = new Dictionary<AnimationChainSave, uint> { [achj.AnimationChains[0]] = 12 };

        var result = Assert.Single(MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo, hints));

        Assert.Empty(result.AnchorFrames);
        Assert.Contains("owner tile 12", Assert.Single(result.Warnings));
    }

    // ── EntryTileIdIsFreshlyComputed (tile-ownership-transfer signal) ────────────────────────
    // ProjectManager uses this to tell "the hint was actually used as-is" (possibly a
    // hand-authored owner tile that's intentionally unrelated to frame 0's own geometry, must
    // never be silently discarded) apart from "no hint existed, so this is exactly frame 0's own
    // computed top-left tile" (safe to treat as ours to relocate later if geometry moves).

    [Fact]
    public void Map_NoEntryHint_EntryTileIdIsFreshlyComputedIsTrue()
    {
        var achj = AchjWithChain("Idle", PixelFrame(0, 0, 16, 16));

        var result = Assert.Single(MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo));

        Assert.True(result.EntryTileIdIsFreshlyComputed);
    }

    [Fact]
    public void Map_EntryHintProvided_EntryTileIdIsFreshlyComputedIsFalse()
    {
        var achj = AchjWithChain("Idle", PixelFrame(0, 0, 16, 16));
        var chain = achj.AnimationChains[0];
        var hints = new Dictionary<AnimationChainSave, uint> { [chain] = 7 };

        var result = Assert.Single(MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo, hints));

        Assert.Equal((uint)7, result.EntryTileId);
        Assert.False(result.EntryTileIdIsFreshlyComputed);
    }

    [Fact]
    public void Map_MappingFailsButHasKnownEntryHint_EntryTileIdIsFreshlyComputedIsFalse()
    {
        var achj = AchjWithChain("Bad", PixelFrame(0, 0, 20, 16));
        var chain = achj.AnimationChains[0];
        var hints = new Dictionary<AnimationChainSave, uint> { [chain] = 5 };

        var result = Assert.Single(MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo, hints));

        Assert.False(result.EntryTileIdIsFreshlyComputed);
    }
}
