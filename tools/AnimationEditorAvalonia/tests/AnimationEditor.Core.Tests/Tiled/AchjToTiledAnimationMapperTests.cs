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
    public void Map_DifferentTexture_SkipsFrameAndWarns()
    {
        var achj = AchjWithChain("OtherTexture", PixelFrame(0, 0, 16, 32, textureName: "OtherSheet.png"));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Contains("different texture", results[0].Warnings[0]);
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
    public void Map_MarginOrSpacing_SkipsWholeChainAndWarns()
    {
        var achj = AchjWithChain("Walk", PixelFrame(0, 0, 16, 32));
        var marginedTilesetInfo = TilesetInfo with { Margin = 2 };

        var results = AchjToTiledAnimationMapper.Map(achj, marginedTilesetInfo);

        Assert.Empty(results[0].Frames);
        Assert.Null(results[0].EntryTileId);
        Assert.Contains("margin or spacing", results[0].Warnings[0]);
    }

    [Fact]
    public void Map_NoChainProducesAnyFrame_EntryTileIdIsNull()
    {
        var achj = AchjWithChain("Empty", PixelFrame(0, 0, 20, 32));

        var results = AchjToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Null(results[0].EntryTileId);
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
