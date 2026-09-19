using AnimationEditor.Core.Tiled;
using DotTiled.Serialization;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TsxCompatibilityCheckerTests
{
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

    // Real Tiled tsx wangset schema -- one of the constructs TsxWriter throws on (see its
    // remarks); used here to prove the compatibility checker catches it before AE ever tries
    // to save this tileset natively.
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

    private static string WriteFixture(string xml, string fileName)
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(tempDir, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    [Fact]
    public void CheckOpenCompatibility_PlainTileset_ReturnsNoBlockingReason()
    {
        var tileset = Loader.Default().LoadTileset(WriteFixture(PlainFixtureXml, "Heroes.tsx"));

        var canOpen = TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason);

        Assert.True(canOpen);
        Assert.Null(blockingReason);
    }

    [Fact]
    public void CheckOpenCompatibility_TilesetWithWangsets_ReturnsBlockingReason()
    {
        var tileset = Loader.Default().LoadTileset(WriteFixture(WangsetFixtureXml, "Terrain.tsx"));

        var canOpen = TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason);

        Assert.False(canOpen);
        Assert.Contains("wangsets", blockingReason);
    }
}
