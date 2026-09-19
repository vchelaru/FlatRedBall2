using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TiledTilesetSyncRunnerTests
{
    // 4 columns, 16x16 tiles, no image pixel size needed since the achx below uses Pixel
    // coordinates (ProjectManager.AnimationChainListSave is always UV in memory, but the mapper
    // itself supports Pixel too -- see AchjToTiledAnimationMapperTests for the UV path).
    private static string WriteFixtureTileset(string tempDir, string fileName = "Heroes.tsx")
    {
        var tileset = new Tileset
        {
            Name = "Heroes",
            TileWidth = 16,
            TileHeight = 16,
            TileCount = 64,
            Columns = 4,
            Image = new Image { Source = "Heroes.png" },
        };
        var path = Path.Combine(tempDir, fileName);
        TsxWriter.Write(tileset, path);
        return path;
    }

    private static AnimationChainListSave AchjWithWalkChain()
    {
        var save = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0, TopCoordinate = 0, RightCoordinate = 16, BottomCoordinate = 16,
        });
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 16, TopCoordinate = 0, RightCoordinate = 32, BottomCoordinate = 16,
        });
        save.AnimationChains.Add(chain);
        return save;
    }

    [Fact]
    public void SyncAll_MatchingChain_WritesTileAnimationBackToTsxFile()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var achj = AchjWithWalkChain();

        var outcomes = TiledTilesetSyncRunner.SyncAll(achj, achxPath, [tsxPath]);

        Assert.True(outcomes[0].Success);
        Assert.Equal(1, outcomes[0].AppliedCount);

        var reloaded = Loader.Default().LoadTileset(tsxPath);
        var tile0 = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 100), ((uint)1, 100)], tile0.Animation.Select(f => (f.TileID, f.Duration)));
        Assert.Equal("Walk", tile0.GetProperty<StringProperty>("achjAnimationName").Value);
    }

    [Fact]
    public void SyncAll_ChainRemovedSinceLastSync_ClearsStaleTileOnReSync()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var tsxPath = WriteFixtureTileset(tempDir);
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        TiledTilesetSyncRunner.SyncAll(AchjWithWalkChain(), achxPath, [tsxPath]);

        var emptyAchj = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        TiledTilesetSyncRunner.SyncAll(emptyAchj, achxPath, [tsxPath]);

        var reloaded = Loader.Default().LoadTileset(tsxPath);
        var tile0 = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Empty(tile0.Animation);
    }

    [Fact]
    public void SyncAll_UnreadableTsxPath_ReportsFailureWithoutThrowing()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var achxPath = Path.Combine(tempDir, "Hero.achx");
        var missingTsxPath = Path.Combine(tempDir, "DoesNotExist.tsx");

        var outcomes = TiledTilesetSyncRunner.SyncAll(AchjWithWalkChain(), achxPath, [missingTsxPath]);

        Assert.False(outcomes[0].Success);
        Assert.NotNull(outcomes[0].Error);
    }
}
