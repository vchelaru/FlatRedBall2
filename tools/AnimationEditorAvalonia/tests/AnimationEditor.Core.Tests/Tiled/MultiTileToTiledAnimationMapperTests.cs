using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
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
    public void Map_FrameSizeNotWholeMultipleOfTile_SkipsChainAndWarns()
    {
        var achj = AchjWithChain("Bad", PixelFrame(0, 0, 20, 16));

        var results = MultiTileToTiledAnimationMapper.Map(achj, TilesetInfo);

        Assert.Empty(results[0].AnchorFrames);
        Assert.Contains("whole number of tiles", results[0].Warnings[0]);
    }
}
