using AnimationEditor.Core.Tiled;
using DotTiled;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TiledAnimationToAchjMapperTests
{
    // 4 columns, 16x16 tiles.
    private static Tileset EmptyTileset() => new()
    {
        Name = "Heroes",
        TileWidth = 16,
        TileHeight = 16,
        TileCount = 256,
        Columns = 4,
        Image = new Image { Source = "Heroes.png" },
    };

    [Fact]
    public void Map_MultiTileSatelliteWithParentId_FoldsIntoOneWideFrameChain()
    {
        var tileset = EmptyTileset();
        var anchor = new Tile { ID = 0, Width = 0, Height = 0 };
        anchor.Animation.Add(new Frame { TileID = 0, Duration = 100 });
        anchor.Animation.Add(new Frame { TileID = 1, Duration = 100 });
        tileset.Tiles.Add(anchor);

        // Satellite sits one column to the right of the anchor (tile 1 = column 1, row 0).
        var satellite = new Tile { ID = 1, Width = 0, Height = 0 };
        satellite.Animation.Add(new Frame { TileID = 1, Duration = 100 });
        satellite.Animation.Add(new Frame { TileID = 2, Duration = 100 });
        satellite.Properties.Add(new IntProperty { Name = "ParentId", Value = 0 });
        tileset.Tiles.Add(satellite);

        var acls = TiledAnimationToAchjMapper.Map(tileset);

        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal(2, chain.Frames.Count);

        // Frame 0: anchor tile 0 -> column 0, row 0; footprint is 2 tiles wide -> right edge at 2*16=32.
        Assert.Equal(0, chain.Frames[0].LeftCoordinate);
        Assert.Equal(32, chain.Frames[0].RightCoordinate);
        Assert.Equal(0, chain.Frames[0].TopCoordinate);
        Assert.Equal(16, chain.Frames[0].BottomCoordinate);

        // Frame 1: anchor tile 1 -> column 1, row 0 -> left edge at 16, right edge at (1+2)*16=48.
        Assert.Equal(16, chain.Frames[1].LeftCoordinate);
        Assert.Equal(48, chain.Frames[1].RightCoordinate);
    }

    [Fact]
    public void Map_TileWithNameProperty_UsesNameAsChainName()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 2, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 2, Duration = 100 });
        tile.Properties.Add(new StringProperty { Name = "Name", Value = "Torch" });
        tileset.Tiles.Add(tile);

        var acls = TiledAnimationToAchjMapper.Map(tileset);

        Assert.Equal("Torch", acls.AnimationChains.Single().Name);
    }

    [Fact]
    public void Map_TileWithoutNameProperty_UsesIdLabel()
    {
        var tileset = EmptyTileset();
        var tile = new Tile { ID = 5, Width = 0, Height = 0 };
        tile.Animation.Add(new Frame { TileID = 5, Duration = 200 });
        tileset.Tiles.Add(tile);

        var acls = TiledAnimationToAchjMapper.Map(tileset);

        var chain = Assert.Single(acls.AnimationChains);
        Assert.Equal("ID:5", chain.Name);
        // Tile 5, 4 columns -> column 1, row 1 -> left=16, top=16.
        Assert.Equal(16, chain.Frames[0].LeftCoordinate);
        Assert.Equal(16, chain.Frames[0].TopCoordinate);
        Assert.Equal(200, chain.Frames[0].FrameLength);
    }
}
