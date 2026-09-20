using AnimationEditor.Core;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerTsxProjectTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles, one animated tile with no Name property.
    private const string PlainFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private const string WangsetFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Terrain" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Terrain.png" width="64" height="64"/>
         <wangsets>
          <wangset name="Ground" type="corner" tile="-1">
           <wangcolor name="Grass" color="#00ff00" tile="0" probability="1"/>
           <wangtile tileid="0" wangid="0,1,0,1,0,1,0,1"/>
          </wangset>
         </wangsets>
        </tileset>
        """;

    private string WriteFixture(string xml, string fileName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    [Fact]
    public void LoadTsxProject_PlainTileset_PopulatesAnimationChainListSaveAndTileSize()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");

        pm.LoadTsxProject(new FilePath(path));

        Assert.True(pm.IsNativeTsxProject);
        Assert.Equal("ID:0", pm.AnimationChainListSave!.AnimationChains.Single().Name);
        Assert.Equal((16, 16), pm.TsxTileSize);
    }

    [Fact]
    public void LoadTsxProject_UnsupportedConstruct_ThrowsAndLeavesProjectUnchanged()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(WangsetFixtureXml, "Terrain.tsx");

        var ex = Assert.Throws<NotSupportedException>(() => pm.LoadTsxProject(new FilePath(path)));

        Assert.Contains("wangsets", ex.Message);
        Assert.False(pm.IsNativeTsxProject);
        Assert.Null(pm.AnimationChainListSave);
    }

    [Fact]
    public void SaveTsxProject_AfterLoad_WritesAnimationBackToTsxFile()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        pm.SaveTsxProject();

        var reloaded = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        var tile = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200)], tile.Animation.Select(f => (f.TileID, f.Duration)));
    }

    // A chain the user creates in-session (never loaded from disk) has no prior
    // _tsxEntryTileIdsByChain entry, so its first save must compute an entry tile id from its
    // first frame -- but every save after that must reuse the id it committed on that first save,
    // not recompute, even if the chain's own frame order later changes what frame[0] would map to.
    [Fact]
    public void SaveTsxProject_BrandNewChain_EntryTileIdStaysStableAcrossRepeatedSaves()
    {
        var pm = new ProjectManager();
        var path = WriteFixture(PlainFixtureXml, "Heroes.tsx");
        pm.LoadTsxProject(new FilePath(path));

        // Tiles 4 and 5 (row 1) carry no animation in the fixture, so this is a genuinely new chain.
        var newChain = new AnimationChainSave { Name = "NewChain" };
        newChain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png",
            LeftCoordinate = 0f, RightCoordinate = 0.25f,
            TopCoordinate = 0.25f, BottomCoordinate = 0.5f,
            FrameLength = 0.1f,
        });
        newChain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png",
            LeftCoordinate = 0.25f, RightCoordinate = 0.5f,
            TopCoordinate = 0.25f, BottomCoordinate = 0.5f,
            FrameLength = 0.1f,
        });
        pm.AnimationChainListSave!.AnimationChains.Add(newChain);

        pm.SaveTsxProject();

        var afterFirstSave = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal((uint)4, EntryTileIdNamed(afterFirstSave, "NewChain"));

        // Reverse frame order: frame[0] now maps to tile 5, not tile 4. Without identity tracking
        // this save would relocate the animation from tile 4 to tile 5.
        newChain.Frames.Reverse();
        pm.SaveTsxProject();

        var afterSecondSave = DotTiled.Serialization.Loader.Default().LoadTileset(path);
        Assert.Equal((uint)4, EntryTileIdNamed(afterSecondSave, "NewChain"));
    }

    private static uint EntryTileIdNamed(DotTiled.Tileset tileset, string chainName) =>
        tileset.Tiles
            .Single(t => t.Properties.OfType<DotTiled.StringProperty>().Any(p => p.Name == "Name" && p.Value == chainName))
            .ID;
}
