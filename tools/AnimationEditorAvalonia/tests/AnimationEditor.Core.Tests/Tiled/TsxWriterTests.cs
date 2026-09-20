using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TsxWriterTests
{
    // Modeled on real-world shape: a shared tileset image, several animated tiles, a plain
    // typed tile with no animation, and a tile carrying custom properties.
    private const string FixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="256" columns="16">
         <image source="Heroes.png" width="256" height="256"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
           <frame tileid="2" duration="200"/>
          </animation>
         </tile>
         <tile id="5">
          <animation>
           <frame tileid="5" duration="150"/>
           <frame tileid="6" duration="150"/>
          </animation>
         </tile>
         <tile id="12" type="Chest"/>
         <tile id="20" type="Torch">
          <properties>
           <property name="lightRadius" type="int" value="4"/>
           <property name="flickers" type="bool" value="true"/>
           <property name="label" value="Wall Torch"/>
          </properties>
          <animation>
           <frame tileid="20" duration="100"/>
           <frame tileid="21" duration="100"/>
          </animation>
         </tile>
        </tileset>
        """;

    // Predates Tiled 1.12's own conventions (space before "/>", format="png", lowercase
    // "utf-8") -- the exact shape that made a single-tile edit reformat a whole real-world
    // tileset (#1146). Three tiles (first/middle/last) so position-dependent bugs (the writer
    // has already produced two: a wrong trailing gap for a brand-new last tile, and a double
    // leading space when the first tile gets edited) have somewhere to hide if reintroduced.
    // These fixtures write to the SAME path they read from, since that's the in-place-save
    // pattern every real caller (ProjectManager.SaveTsxProject, TiledTilesetSyncRunner.SyncAll) uses.
    private const string LegacyStyleFixtureXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
        " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
        " <tile id=\"5\">\n" +
        "  <animation>\n" +
        "   <frame tileid=\"5\" duration=\"200\"/>\n" +
        "   <frame tileid=\"6\" duration=\"200\"/>\n" +
        "  </animation>\n" +
        " </tile>\n" +
        " <tile id=\"8\" type=\"Rock\"/>\n" +
        " <tile id=\"12\" type=\"Chest\"/>\n" +
        "</tileset>\n";

    private const string LegacyStyleFixtureXmlCrlf =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n" +
        "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\r\n" +
        " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\r\n" +
        " <tile id=\"5\" type=\"Rock\"/>\r\n" +
        " <tile id=\"12\" type=\"Chest\"/>\r\n" +
        "</tileset>\r\n";

    // No tiles at all -- the exact shape a brand-new project starts from before any animation
    // has been synced in (#1146's regression: adding the first-ever tile got a stray leading
    // space because nothing preceded it to borrow indentation from).
    private const string LegacyStyleFixtureXmlNoTiles =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<tileset name=\"Bare\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"4\" columns=\"2\">\n" +
        " <image source=\"Bare.png\"/>\n" +
        "</tileset>\n";

    private static string WriteFixture(string tempDir)
    {
        var path = Path.Combine(tempDir, "Heroes.tsx");
        File.WriteAllText(path, FixtureXml);
        return path;
    }

    private static Tileset WriteLegacyFixture(string tempDir, out string path, string xml = LegacyStyleFixtureXml)
    {
        path = Path.Combine(tempDir, "Legacy.tsx");
        File.WriteAllText(path, xml);
        return Loader.Default().LoadTileset(path);
    }

    [Fact]
    public void Write_AllTilesRemoved_ProducesTilesetWithNoTiles()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.Clear();

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_AnimationFramesReordered_TreatsAsChangeNotDuplicate()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        var tile5 = tileset.Tiles.Single(t => t.ID == 5);
        var reversed = tile5.Animation.AsEnumerable().Reverse().ToList();
        tile5.Animation.Clear();
        tile5.Animation.AddRange(reversed);

        TsxWriter.Write(tileset, path);
        var written = File.ReadAllText(path);

        Assert.Equal(1, written.Split("<tile id=\"5\"").Length - 1);
        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"5\">\n" +
            "  <animation>\n" +
            "   <frame tileid=\"6\" duration=\"200\" />\n" +
            "   <frame tileid=\"5\" duration=\"200\" />\n" +
            "  </animation>\n" +
            " </tile>\n" +
            " <tile id=\"8\" type=\"Rock\"/>\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            "</tileset>\n",
            written);
    }

    [Fact]
    public void Write_CrlfFileNoContentChange_PreservesOriginalFormatting()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path, LegacyStyleFixtureXmlCrlf);

        TsxWriter.Write(tileset, path);

        Assert.Equal(LegacyStyleFixtureXmlCrlf, File.ReadAllText(path));
    }

    [Fact]
    public void Write_ExistingFileNoContentChange_PreservesOriginalFormatting()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);

        TsxWriter.Write(tileset, path);

        Assert.Equal(LegacyStyleFixtureXml, File.ReadAllText(path));
    }

    [Fact]
    public void Write_FirstTileEdited_OnlyRewritesThatTileExactly()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.Single(t => t.ID == 5).Animation.Clear();

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"5\" />\n" +
            " <tile id=\"8\" type=\"Rock\"/>\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_LastTileEdited_OnlyRewritesThatTileExactly()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.Single(t => t.ID == 12).Properties.Add(new StringProperty { Name = "locked", Value = "true" });

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"5\">\n" +
            "  <animation>\n" +
            "   <frame tileid=\"5\" duration=\"200\"/>\n" +
            "   <frame tileid=\"6\" duration=\"200\"/>\n" +
            "  </animation>\n" +
            " </tile>\n" +
            " <tile id=\"8\" type=\"Rock\"/>\n" +
            " <tile id=\"12\" type=\"Chest\">\n" +
            "  <properties>\n" +
            "   <property name=\"locked\" value=\"true\" />\n" +
            "  </properties>\n" +
            " </tile>\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_MiddleTileEdited_OnlyRewritesThatTileExactly()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.Single(t => t.ID == 8).Properties.Add(new StringProperty { Name = "note", Value = "boulder" });

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"5\">\n" +
            "  <animation>\n" +
            "   <frame tileid=\"5\" duration=\"200\"/>\n" +
            "   <frame tileid=\"6\" duration=\"200\"/>\n" +
            "  </animation>\n" +
            " </tile>\n" +
            " <tile id=\"8\" type=\"Rock\">\n" +
            "  <properties>\n" +
            "   <property name=\"note\" value=\"boulder\" />\n" +
            "  </properties>\n" +
            " </tile>\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_MultipleSimultaneousChanges_OnlyRewritesChangedTiles()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.Single(t => t.ID == 5).Animation.Clear();
        tileset.Tiles.RemoveAll(t => t.ID == 8);
        tileset.Tiles.Add(new Tile { ID = 99, Type = "Extra", Width = 0, Height = 0 });

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"5\" />\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            " <tile id=\"99\" type=\"Extra\" />\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_NewTileAddedAtEnd_OnlyAddsThatTileExactly()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.Add(new Tile { ID = 20, Type = "Extra", Width = 0, Height = 0 });

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"5\">\n" +
            "  <animation>\n" +
            "   <frame tileid=\"5\" duration=\"200\"/>\n" +
            "   <frame tileid=\"6\" duration=\"200\"/>\n" +
            "  </animation>\n" +
            " </tile>\n" +
            " <tile id=\"8\" type=\"Rock\"/>\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            " <tile id=\"20\" type=\"Extra\" />\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_NewTileInsertedAtStart_OnlyAddsThatTileExactly()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.Insert(0, new Tile { ID = 1, Type = "First", Width = 0, Height = 0 });

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"1\" type=\"First\" />\n" +
            " <tile id=\"5\">\n" +
            "  <animation>\n" +
            "   <frame tileid=\"5\" duration=\"200\"/>\n" +
            "   <frame tileid=\"6\" duration=\"200\"/>\n" +
            "  </animation>\n" +
            " </tile>\n" +
            " <tile id=\"8\" type=\"Rock\"/>\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_NewTileInsertedInMiddle_OnlyAddsThatTileExactly()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.Insert(1, new Tile { ID = 7, Type = "Mid", Width = 0, Height = 0 });

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"5\">\n" +
            "  <animation>\n" +
            "   <frame tileid=\"5\" duration=\"200\"/>\n" +
            "   <frame tileid=\"6\" duration=\"200\"/>\n" +
            "  </animation>\n" +
            " </tile>\n" +
            " <tile id=\"7\" type=\"Mid\" />\n" +
            " <tile id=\"8\" type=\"Rock\"/>\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_TileOrderChanged_RelocatesWithoutDuplicating()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        (tileset.Tiles[0], tileset.Tiles[1]) = (tileset.Tiles[1], tileset.Tiles[0]);

        TsxWriter.Write(tileset, path);
        var written = File.ReadAllText(path);

        Assert.Equal(1, written.Split("<tile id=\"5\"").Length - 1);
        Assert.Equal(1, written.Split("<tile id=\"8\"").Length - 1);
        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"8\" type=\"Rock\"/>\n" +
            " <tile id=\"5\">\n" +
            "  <animation>\n" +
            "   <frame tileid=\"5\" duration=\"200\"/>\n" +
            "   <frame tileid=\"6\" duration=\"200\"/>\n" +
            "  </animation>\n" +
            " </tile>\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            "</tileset>\n",
            written);
    }

    [Fact]
    public void Write_TileRemoved_OmitsThatTileExactly()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Tiles.RemoveAll(t => t.ID == 8);

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Legacy\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">\n" +
            " <image source=\"Legacy.png\" width=\"128\" height=\"128\"/>\n" +
            " <tile id=\"5\">\n" +
            "  <animation>\n" +
            "   <frame tileid=\"5\" duration=\"200\"/>\n" +
            "   <frame tileid=\"6\" duration=\"200\"/>\n" +
            "  </animation>\n" +
            " </tile>\n" +
            " <tile id=\"12\" type=\"Chest\"/>\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_TopLevelAttributeChanged_FallsBackButStaysCorrect()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path);
        tileset.Properties.Add(new StringProperty { Name = "schemaVersion", Value = "2" });

        TsxWriter.Write(tileset, path);
        var reloaded = Loader.Default().LoadTileset(path);

        Assert.Equal("2", reloaded.GetProperty<StringProperty>("schemaVersion").Value);
        var tile5 = reloaded.Tiles.Single(t => t.ID == 5);
        Assert.Equal([((uint)5, 200), ((uint)6, 200)], tile5.Animation.Select(f => (f.TileID, f.Duration)));
    }

    [Fact]
    public void Write_ZeroTilesOriginally_TilesAdded_RendersCorrectly()
    {
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path, LegacyStyleFixtureXmlNoTiles);
        tileset.Tiles.Add(new Tile { ID = 0, Type = "First", Width = 0, Height = 0 });

        TsxWriter.Write(tileset, path);

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<tileset name=\"Bare\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"4\" columns=\"2\">\n" +
            " <image source=\"Bare.png\"/>\n" +
            " <tile id=\"0\" type=\"First\" />\n" +
            "</tileset>\n",
            File.ReadAllText(path));
    }

    [Fact]
    public void Write_RoundTrip_PreservesAnimationFrames()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = WriteFixture(tempDir);
        var original = Loader.Default().LoadTileset(fixturePath);
        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");

        TsxWriter.Write(original, outputPath);
        var reloaded = Loader.Default().LoadTileset(outputPath);

        var tile0 = reloaded.Tiles.Single(t => t.ID == 0);
        Assert.Equal([((uint)0, 200), ((uint)1, 200), ((uint)2, 200)],
            tile0.Animation.Select(f => (f.TileID, f.Duration)));

        var tile5 = reloaded.Tiles.Single(t => t.ID == 5);
        Assert.Equal([((uint)5, 150), ((uint)6, 150)],
            tile5.Animation.Select(f => (f.TileID, f.Duration)));
    }

    [Fact]
    public void Write_RoundTrip_PreservesTileTypeAndProperties()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = WriteFixture(tempDir);
        var original = Loader.Default().LoadTileset(fixturePath);
        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");

        TsxWriter.Write(original, outputPath);
        var reloaded = Loader.Default().LoadTileset(outputPath);

        var chest = reloaded.Tiles.Single(t => t.ID == 12);
        Assert.Equal("Chest", chest.Type);
        Assert.Empty(chest.Animation);

        var torch = reloaded.Tiles.Single(t => t.ID == 20);
        Assert.Equal("Torch", torch.Type);
        Assert.Equal(4, torch.GetProperty<IntProperty>("lightRadius").Value);
        Assert.True(torch.GetProperty<BoolProperty>("flickers").Value);
        Assert.Equal("Wall Torch", torch.GetProperty<StringProperty>("label").Value);
        Assert.Equal([((uint)20, 100), ((uint)21, 100)],
            torch.Animation.Select(f => (f.TileID, f.Duration)));
    }

    [Fact]
    public void Write_RoundTrip_PreservesTilesetAttributes()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = WriteFixture(tempDir);
        var original = Loader.Default().LoadTileset(fixturePath);
        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");

        TsxWriter.Write(original, outputPath);
        var reloaded = Loader.Default().LoadTileset(outputPath);

        Assert.Equal("Heroes", reloaded.Name);
        Assert.Equal(16, reloaded.TileWidth);
        Assert.Equal(16, reloaded.TileHeight);
        Assert.Equal(256, reloaded.TileCount);
        Assert.Equal(16, reloaded.Columns);
        Assert.True(reloaded.Image.HasValue);
        Assert.Equal("Heroes.png", reloaded.Image.Value.Source.Value);
        Assert.Equal(256, reloaded.Image.Value.Width.Value);
        Assert.Equal(256, reloaded.Image.Value.Height.Value);
    }

    [Fact]
    public void Write_WellFormedXml_KeepsRequiredTiledAttributes()
    {
        var tempDir = Directory.CreateTempSubdirectory().FullName;
        var fixturePath = WriteFixture(tempDir);
        var original = Loader.Default().LoadTileset(fixturePath);
        var outputPath = Path.Combine(tempDir, "Heroes.written.tsx");

        TsxWriter.Write(original, outputPath);
        var document = System.Xml.Linq.XDocument.Load(outputPath);
        var tilesetElement = document.Root!;

        Assert.Equal("tileset", tilesetElement.Name.LocalName);
        Assert.Equal("Heroes", tilesetElement.Attribute("name")!.Value);
        Assert.Equal("16", tilesetElement.Attribute("tilewidth")!.Value);
        Assert.Equal("16", tilesetElement.Attribute("tileheight")!.Value);
        Assert.Equal("256", tilesetElement.Attribute("tilecount")!.Value);
        Assert.Equal("16", tilesetElement.Attribute("columns")!.Value);
        Assert.Equal(4, tilesetElement.Elements("tile").Count());
    }
}
