using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class AchjToTiledAnimationMapperTests
{
    // 4 columns, 16x32 tiles -- matches the fixture the old JS extension's test suite used
    // before this class replaced it (issue #1133).
    private static readonly TilesetAnimationInfo TilesetInfo = new()
    {
        TileWidth = 16,
        TileHeight = 32,
        ColumnCount = 4,
        TileCount = 16,
        ImageFileName = "AnimatedSpritesheet.png",
    };

    private static AnimationChainListSave AchjWithChain(
        string chainName, params AnimationFrameSave[] frames)
    {
        var save = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.AddRange(frames);
        save.AnimationChains.Add(chain);
        return save;
    }

    private static AnimationFrameSave PixelFrame(
        float left, float top, float right, float bottom, float frameLength = 0.1f,
        string textureName = "AnimatedSpritesheet.png",
        bool flipHorizontal = false, bool flipVertical = false, bool flipDiagonal = false) => new()
    {
        TextureName = textureName,
        FrameLength = frameLength,
        LeftCoordinate = left,
        TopCoordinate = top,
        RightCoordinate = right,
        BottomCoordinate = bottom,
        FlipHorizontal = flipHorizontal,
        FlipVertical = flipVertical,
        FlipDiagonal = flipDiagonal,
    };

    [Fact]
    public void Map_ColumnBeyondTilesetWidth_SkipsFrameAndWarnsInsteadOfWrappingIntoNextRow()
    {
        // Left=64 is column 4 -- one past the last valid column index (0-3) in a 4-column
        // tileset. Column 4 passes both the grid-alignment and negative-column checks, but
        // tileId = row*ColumnCount + column would land on tile 4 -- a real tile, just the first
        // one of the *next* row, not "one past the last column of this row".
        var achj = AchjWithChain("Corrupt", PixelFrame(64, 0, 80, 32));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("column", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_RowBeyondTilesetTileCount_SkipsFrameAndWarnsInsteadOfFabricatingOutOfRangeTile()
    {
        // Row 4 (top=128, tile height 32), column 0 -- column 0 is in range, but tileId = 4*4+0 =
        // 16, which is at/past this tileset's declared TileCount (16, tile ids 0-15). Unlike
        // column overflow, this doesn't wrap into an existing tile -- it's a tile id with no real
        // cell at all, which the sync step would otherwise fabricate a phantom <tile> for.
        var achj = AchjWithChain("Corrupt", PixelFrame(0, 128, 16, 160));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("tile id 16", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_DifferentTexture_SkipsFrameAndWarns()
    {
        var achj = AchjWithChain("OtherTexture", PixelFrame(0, 0, 16, 32, textureName: "OtherSheet.png"));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("different texture", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_FirstFrameSkippedButLaterFrameValid_EntryTileIdIsFirstSurvivingFrameNotOriginalFrameZero()
    {
        // EntryTileId is documented (see ChainMappingResult.EntryTileId) as "the first non-skipped
        // frame's tile id," not "frame 0's tile id" -- and that's intentional here, not a gap:
        // achx-push has no identity-preservation concept for entry tile ids the way native-tsx's
        // knownEntryTileIds hint does (TilesetAnimationSync's own
        // Apply_RenamedChainMovesToDifferentTile_ClearsOldTileAndPopulatesNewTile test already
        // establishes that an achx-push chain simply follows wherever its geometry currently
        // points, with the sync layer's source-scoped stale-clearing self-healing the old tile
        // either way). This chain's frame 0 references a different texture (skipped); frame 1 is
        // the first frame that actually survives, at tile 5.
        var achj = AchjWithChain("Walk",
            PixelFrame(0, 0, 16, 32, textureName: "OtherSheet.png"),
            PixelFrame(16, 32, 32, 64));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Equal((uint)5, results[0].EntryTileId);
        Assert.Equal([(uint)5], results[0].Frames.Select(f => f.TileId));
    }

    [Fact]
    public void Map_FlippedFrame_KeepsFrameButWarnsFlipDropped()
    {
        var achj = AchjWithChain("Flipped", PixelFrame(0, 0, 16, 32, flipHorizontal: true));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Single(results[0].Frames);
        Assert.Contains(results[0].Warnings, w => w.Contains("flip") && w.Contains("dropped"));
    }

    [Fact]
    public void Map_GridAlignedPixelFrames_MapsToTileIdsAndConvertsSecondsToMilliseconds()
    {
        var achj = AchjWithChain("Walk",
            PixelFrame(0, 0, 16, 32, frameLength: 0.1f),
            PixelFrame(16, 0, 32, 32, frameLength: 0.1f));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Equal("Walk", results[0].ChainName);
        Assert.Empty(results[0].Warnings);
        Assert.Equal([new MappedFrame(0, 100), new MappedFrame(1, 100)], results[0].Frames);
        Assert.Equal((uint)0, results[0].EntryTileId);
    }

    [Fact]
    public void Map_MarginAndSpacing_LocatesTileFromItsRealPixels()
    {
        // 16x32 tiles, margin 2, spacing 1: column 1 starts at x = 2 + 17 = 19, row 1 at y = 2 + 33 = 35.
        var achj = AchjWithChain("Walk", PixelFrame(19, 35, 35, 67));
        var spacedTilesetInfo = TilesetInfo with { Margin = 2, TileSpacing = 1 };

        var results = AchjToTiledAnimationMapper.Map(achj, spacedTilesetInfo);

        Assert.Equal((uint)5, Assert.Single(results[0].Frames).TileId);
        Assert.Empty(results[0].Warnings);
    }

    [Fact]
    public void Map_MarginAndSpacing_FrameOnUnspacedPixels_SkipsAsNotGridAligned()
    {
        // Left=16 is where column 1 would be WITHOUT the margin/spacing; on this sheet it is 19.
        var achj = AchjWithChain("Walk", PixelFrame(16, 0, 32, 32));
        var spacedTilesetInfo = TilesetInfo with { Margin = 2, TileSpacing = 1 };

        var results = AchjToTiledAnimationMapper.Map(achj, spacedTilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("not aligned", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_NoChainProducesAnyFrame_EntryTileIdIsNull()
    {
        var achj = AchjWithChain("Empty", PixelFrame(0, 0, 20, 32));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Null(results[0].EntryTileId);
    }

    [Fact]
    public void Map_NegativeAlignedCoordinate_SkipsFrameAndWarnsInsteadOfUncheckedCastToHugeId()
    {
        // Left=-16 is an exact multiple of tile width 16, so it passes the grid-alignment check
        // (remainder is 0 -- C#'s "%" keeps the dividend's sign) yet resolves to column -1.
        // Casting that straight to uint would wrap to 4294967295, same bug shape as the
        // negative-ParentId fix in TiledAnimationToAchjMapper.
        var achj = AchjWithChain("Corrupt", PixelFrame(-16, 0, 0, 32));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("negative", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_NotGridAligned_SkipsFrameAndWarns()
    {
        var achj = AchjWithChain("Unaligned", PixelFrame(4, 0, 20, 32));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("not aligned to the tile grid", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_RowAndColumn_ComputesTileIdFromColumnCount()
    {
        // row 1 (y=32/32), column 2 (x=32/16), columnCount=4 -> tileId = 1*4 + 2 = 6
        var achj = AchjWithChain("SecondRow", PixelFrame(32, 32, 48, 64, frameLength: 0.2f));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Equal([new MappedFrame(6, 200)], results[0].Frames);
    }

    [Fact]
    public void Map_SizeMismatch_SkipsFrameAndWarns()
    {
        var achj = AchjWithChain("WrongSize", PixelFrame(0, 0, 20, 32));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("doesn't match tile size", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_TallySkipsTrue_RowOutOfRangeTalliedInSkipCounts()
    {
        var achj = AchjWithChain("Corrupt", PixelFrame(0, 128, 16, 160));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo, tallySkips: true);

        Assert.Empty(results[0].Warnings);
        Assert.Equal(1, results[0].SkipCounts.RowOutOfRange);
    }

    [Fact]
    public void Map_TallySkipsTrue_TalliesInsteadOfItemizingWarnings()
    {
        var achj = AchjWithChain("Mixed",
            PixelFrame(0, 0, 16, 32, textureName: "OtherSheet.png"),   // texture mismatch
            PixelFrame(0, 0, 20, 32),                                   // size mismatch
            PixelFrame(4, 0, 20, 32),                                   // not grid aligned
            PixelFrame(0, 0, 16, 32));                                  // matches fine

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo, tallySkips: true);

        Assert.Empty(results[0].Warnings);
        Assert.Single(results[0].Frames);
        Assert.Equal(1, results[0].SkipCounts.TextureMismatch);
        Assert.Equal(1, results[0].SkipCounts.SizeMismatch);
        Assert.Equal(1, results[0].SkipCounts.NotGridAligned);
        Assert.Equal(0, results[0].SkipCounts.FlipDropped);
    }

    [Fact]
    public void Map_UvCoordinateTypeMissingTexturePixelSize_SkipsFrameAndWarns()
    {
        var achj = AchjWithChain("Walk", PixelFrame(0f, 0f, 0.25f, 1f));
        achj.CoordinateType = TextureCoordinateType.UV;

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("pixel dimensions", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_UvCoordinateTypeWithTexturePixelSize_ConvertsToTileId()
    {
        // 64x32 texture, 16x32 tiles -> column 0 is [0, 0.25) in U
        var achj = AchjWithChain("Walk", PixelFrame(0f, 0f, 0.25f, 1f));
        achj.CoordinateType = TextureCoordinateType.UV;
        var tilesetInfo = TilesetInfo with { TextureWidth = 64, TextureHeight = 32 };

        var results = AchjToTiledAnimationMapper.Map(achj, tilesetInfo);

        Assert.Empty(results[0].Warnings);
        Assert.Equal([new MappedFrame(0, 100)], results[0].Frames);
    }
}
