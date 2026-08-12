using FlatRedBall2.Tiled;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

// Per the TMX spec a tileset's <image source> is relative to the .tsx, but the parser resolves every
// path against one base directory -- the map's. These cover the rewrite that reconciles the two.
//
// Sharing one tileset between levels in different folders is the normal way Tiled projects are laid
// out, so this is the common case rather than an edge one.
public class TilesetImagePathTests
{
    [Fact]
    public void Rewrite_ATilesetAboveTheMap_PointsTheImageBackDown()
    {
        const string tileset = """<tileset name="Icons"><image source="StandardTilesetIcons.png" width="256" height="256"/></tileset>""";

        var rewritten = TileMap.RewriteTilesetImageSources(
            tileset, tilesetDirectory: "Content", mapDirectory: "Content/Screens/Level1");

        rewritten.ShouldContain(@"source=""../../StandardTilesetIcons.png""");
    }

    [Fact]
    public void Rewrite_ATilesetBesideTheMap_LeavesThePathAlone()
    {
        const string tileset = """<tileset><image source="Tiles.png"/></tileset>""";

        var rewritten = TileMap.RewriteTilesetImageSources(
            tileset, tilesetDirectory: "Content/Screens/Level1", mapDirectory: "Content/Screens/Level1");

        rewritten.ShouldContain(@"source=""Tiles.png""");
    }

    [Fact]
    public void Rewrite_ATilesetInASiblingFolder_WalksUpAndBackDown()
    {
        const string tileset = """<tileset><image source="art/Tiles.png"/></tileset>""";

        var rewritten = TileMap.RewriteTilesetImageSources(
            tileset, tilesetDirectory: "Content/Shared", mapDirectory: "Content/Screens/Level1");

        rewritten.ShouldContain(@"source=""../../Shared/art/Tiles.png""");
    }

    [Fact]
    public void Rewrite_ATilesetImagePathThatAlreadyClimbs_CollapsesTheDotDots()
    {
        const string tileset = """<tileset><image source="../Art/Tiles.png"/></tileset>""";

        var rewritten = TileMap.RewriteTilesetImageSources(
            tileset, tilesetDirectory: "Content/Tilesets", mapDirectory: "Content/Screens/Level1");

        rewritten.ShouldContain(@"source=""../../Art/Tiles.png""");
    }

    // The rewrite must stay relative. An absolute path reaches the resolver unprefixed and bypasses
    // TitleContainer, which is the only way content loads on a backend with no filesystem.
    [Fact]
    public void Rewrite_NeverProducesAnAbsolutePath()
    {
        const string tileset = """<tileset><image source="Icons.png"/></tileset>""";

        var rewritten = TileMap.RewriteTilesetImageSources(
            tileset, tilesetDirectory: "Content", mapDirectory: "Content/Screens/Level1");

        rewritten.ShouldNotContain(":");
        rewritten.ShouldNotContain(@"source=""/");
    }

    [Fact]
    public void Rewrite_AnAlreadyRootedPath_IsLeftAlone()
    {
        const string tileset = """<tileset><image source="/absolute/Tiles.png"/></tileset>""";

        var rewritten = TileMap.RewriteTilesetImageSources(
            tileset, tilesetDirectory: "Content", mapDirectory: "Content/Screens");

        rewritten.ShouldContain(@"source=""/absolute/Tiles.png""");
    }

    [Fact]
    public void Rewrite_MultipleImages_RewritesEveryOne()
    {
        const string tileset = """
            <tileset>
              <image source="One.png"/>
              <tile id="1"><image source="Two.png"/></tile>
            </tileset>
            """;

        var rewritten = TileMap.RewriteTilesetImageSources(
            tileset, tilesetDirectory: "Content", mapDirectory: "Content/Screens/Level1");

        rewritten.ShouldContain(@"source=""../../One.png""");
        rewritten.ShouldContain(@"source=""../../Two.png""");
    }

    [Fact]
    public void Rewrite_PreservesEverythingElseInTheDocument()
    {
        const string tileset = """<tileset firstgid="1" name="Icons" tilewidth="16"><image source="I.png" width="256"/></tileset>""";

        var rewritten = TileMap.RewriteTilesetImageSources(
            tileset, tilesetDirectory: "Content", mapDirectory: "Content/Screens");

        rewritten.ShouldContain(@"name=""Icons""");
        rewritten.ShouldContain(@"tilewidth=""16""");
        rewritten.ShouldContain(@"width=""256""");
    }
}
