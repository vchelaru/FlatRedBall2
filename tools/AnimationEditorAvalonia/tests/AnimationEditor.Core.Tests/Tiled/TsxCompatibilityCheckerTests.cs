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

    // Fresh-eyes pass #18: TiledAnimationToAchjMapper places tile N at col*tilewidth, and the
    // native-tsx save path never passes Margin/Spacing to the mapper either, so a tileset with
    // either opened fine but showed every frame rect shifted off its real pixels (tile 1 of a
    // 16px, margin 1, spacing 1 sheet starts at x=18, not 16). The achx-push path already refuses
    // such a tileset per chain; opening natively must refuse up front for the same reason.
    [Theory]
    [InlineData("margin=\"1\"", "margin")]
    [InlineData("spacing=\"1\"", "spacing")]
    public void CheckOpenCompatibility_TilesetWithMarginOrSpacing_ReturnsBlockingReason(string attribute, string expectedWord)
    {
        var xml = PlainFixtureXml.Replace("tilecount=\"16\"", $"{attribute} tilecount=\"16\"");
        var tileset = Loader.Default().LoadTileset(WriteFixture(xml, "Heroes.tsx"));

        var canOpen = TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason);

        Assert.False(canOpen);
        Assert.Contains(expectedWord, blockingReason);
    }

    // Fresh-eyes pass #19: an image-collection tileset (no shared <image>, one <image> per tile,
    // columns="0") is a legitimate Tiled file, not a corrupt one -- but nothing in this editor's
    // grid-based tile math applies to it, and the only error it hit was TiledAnimationToAchjMapper's
    // "Columns=0 isn't a valid tile-grid width", which misdescribes the file.
    private const string ImageCollectionFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Props" tilewidth="32" tileheight="48" tilecount="2" columns="0">
         <grid orientation="orthogonal" width="1" height="1"/>
         <tile id="0">
          <image source="barrel.png" width="32" height="48"/>
         </tile>
         <tile id="1">
          <image source="crate.png" width="32" height="32"/>
         </tile>
        </tileset>
        """;

    [Fact]
    public void CheckOpenCompatibility_ImageCollectionTileset_ReturnsBlockingReason()
    {
        var tileset = Loader.Default().LoadTileset(WriteFixture(ImageCollectionFixtureXml, "Props.tsx"));

        var canOpen = TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason);

        Assert.False(canOpen);
        Assert.Contains("image collection", blockingReason);
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
