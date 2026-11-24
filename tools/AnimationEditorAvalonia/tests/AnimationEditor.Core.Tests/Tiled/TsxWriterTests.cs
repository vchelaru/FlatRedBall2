using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TsxWriterTests
{
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

    private static Tileset WriteLegacyFixture(string tempDir, out string path, string xml = LegacyStyleFixtureXml)
    {
        path = Path.Combine(tempDir, "Legacy.tsx");
        File.WriteAllText(path, xml);
        return Loader.Default().LoadTileset(path);
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
    public void Write_SharedLineWithTileoffsetAndEditedTile_FallsBackWithoutLosingTileoffset()
    {
        // <tileoffset> shares a physical line with the tile that is edited.
        const string sharedLineXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset name=\"Weird\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"4\" columns=\"2\">\n" +
            " <image source=\"Weird.png\"/>\n" +
            " <tileoffset x=\"1\" y=\"2\"/><tile id=\"5\" type=\"Rock\"/>\n" +
            " <tile id=\"8\" type=\"Chest\"/>\n" +
            "</tileset>\n";
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path, sharedLineXml);
        tileset.Tiles.Single(t => t.ID == 5).Properties.Add(new StringProperty { Name = "note", Value = "edited" });

        TsxWriter.Write(tileset, path);
        var reloaded = Loader.Default().LoadTileset(path);

        Assert.True(reloaded.TileOffset.HasValue);
        Assert.Equal(1, reloaded.TileOffset.Value.X);
        Assert.Equal(2, reloaded.TileOffset.Value.Y);
        Assert.Equal("edited", reloaded.Tiles.Single(t => t.ID == 5).GetProperty<StringProperty>("note").Value);
        Assert.Equal("Chest", reloaded.Tiles.Single(t => t.ID == 8).Type);
    }

    [Fact]
    public void Write_TileWithUnchangedObjectLayer_ReusesOriginalSliceWithoutThrowing()
    {
        // Editing one tile leaves another tile's collision layer alone.
        const string objectLayerXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset name=\"Collide\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"4\" columns=\"2\">\n" +
            " <image source=\"Collide.png\"/>\n" +
            " <tile id=\"3\">\n" +
            "  <objectgroup id=\"1\" draworder=\"index\">\n" +
            "   <object id=\"1\" x=\"1\" y=\"4\" width=\"14\" height=\"10\"/>\n" +
            "  </objectgroup>\n" +
            " </tile>\n" +
            " <tile id=\"5\" type=\"Rock\"/>\n" +
            "</tileset>\n";
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path, objectLayerXml);
        tileset.Tiles.Single(t => t.ID == 5).Properties.Add(new StringProperty { Name = "note", Value = "edited" });

        TsxWriter.Write(tileset, path);
        var written = File.ReadAllText(path);

        Assert.Contains("<objectgroup id=\"1\" draworder=\"index\">", written);
        Assert.Contains("<object id=\"1\" x=\"1\" y=\"4\" width=\"14\" height=\"10\"/>", written);
        var reloaded = Loader.Default().LoadTileset(path);
        Assert.Equal("edited", reloaded.Tiles.Single(t => t.ID == 5).GetProperty<StringProperty>("note").Value);
    }

    [Fact]
    public void Write_TwoTilesShareOneLine_FallsBackSafely()
    {
        const string sharedLineXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<tileset name=\"Cramped\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"4\" columns=\"2\">\n" +
            " <image source=\"Cramped.png\"/>\n" +
            " <tile id=\"1\" type=\"First\"/><tile id=\"2\" type=\"Second\"/>\n" +
            "</tileset>\n";
        var tileset = WriteLegacyFixture(Directory.CreateTempSubdirectory().FullName, out var path, sharedLineXml);
        tileset.Tiles.Single(t => t.ID == 1).Properties.Add(new StringProperty { Name = "note", Value = "edited" });

        TsxWriter.Write(tileset, path);
        var reloaded = Loader.Default().LoadTileset(path);

        Assert.Equal(2, reloaded.Tiles.Count);
        Assert.Equal("edited", reloaded.Tiles.Single(t => t.ID == 1).GetProperty<StringProperty>("note").Value);
        Assert.Equal("Second", reloaded.Tiles.Single(t => t.ID == 2).Type);
    }

    private const string Open = "<tileset name=\"T\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"64\" columns=\"8\">";

    /// <summary>What is inside the <c>&lt;tileset&gt;</c> after an edit.</summary>
    private static string Edit(string content, Action<Tileset> edit)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "T.tsx");
        File.WriteAllText(path, $"{Open}{content}</tileset>");
        var tileset = TsxLoader.LoadTileset(path);
        edit(tileset);

        TsxWriter.Write(tileset, path);

        var written = File.ReadAllText(path);
        Assert.StartsWith(Open, written);
        Assert.EndsWith("</tileset>", written);
        return written[Open.Length..^"</tileset>".Length];
    }

    private static Tile Find(Tileset tileset, uint id) => tileset.Tiles.Single(tile => tile.ID == id);

    private static List<Frame> Frames(params (uint TileId, int Duration)[] frames) =>
        frames.Select(frame => new Frame { TileID = frame.TileId, Duration = frame.Duration }).ToList();

    private static Tile Animated(uint id) => new() { ID = id, Width = 0, Height = 0, Animation = Frames((id, 100)) };

    private static string AnimatedText(uint id, string lead, string indent) =>
        $"{lead}<tile id=\"{id}\">{lead}{indent}<animation>{lead}{indent}{indent}<frame tileid=\"{id}\" duration=\"100\"/>{lead}{indent}</animation>{lead}</tile>";

    [Theory]
    [InlineData(1, "\n <tile id=\"5\" type=\"A\"/>")]
    [InlineData(7, "\n <tile id=\"8\" type=\"B\"/>")]
    [InlineData(20, "\n <wangsets/>")]
    public void Write_NewTile_IsInsertedById(uint id, string before)
    {
        const string content = "\n <image source=\"a.png\"/>\n <tile id=\"5\" type=\"A\"/>\n <tile id=\"8\" type=\"B\"/>\n <wangsets/>\n";

        Assert.Equal(
            content.Replace(before, AnimatedText(id, "\n ", " ") + before),
            Edit(content, tileset => tileset.Tiles.Add(Animated(id))));
    }

    [Theory]
    [InlineData("\n <image/>", "\n <wangsets/>\n", "\n ", " ")]
    [InlineData("\n <image/>", "\n", "\n ", " ")]
    [InlineData("\r\n\t<image/>", "\r\n", "\r\n\t", "\t")]
    [InlineData("<image/>", "", "", "")]
    public void Write_NewTileInAFileWithoutTiles_FollowsTheLayoutOfTheFile(string head, string tail, string lead, string indent)
    {
        Assert.Equal(head + AnimatedText(3, lead, indent) + tail, Edit(head + tail, tileset => tileset.Tiles.Add(Animated(3))));
    }

    [Fact]
    public void Write_TwoNewTiles_AreInsertedInOrder()
    {
        Assert.Equal(
            $"\n <tile id=\"1\"/>{AnimatedText(3, "\n ", " ")}{AnimatedText(4, "\n ", " ")}\n",
            Edit("\n <tile id=\"1\"/>\n", tileset => tileset.Tiles.AddRange([Animated(4), Animated(3)])));
    }

    // The frames around the change keep their own text, so an unknown attribute stays with its frame.
    [Theory]
    [InlineData("1,2,3", "1,9,2,3", "\n  <frame tileid=\"1\" duration=\"1\" x=\"\"/>\n  <frame tileid=\"9\" duration=\"9\"/>\n  <frame tileid=\"2\" duration=\"2\" x=\"\"/>\n  <frame tileid=\"3\" duration=\"3\" x=\"\"/>")]
    [InlineData("1,2", "1,2,9", "\n  <frame tileid=\"1\" duration=\"1\" x=\"\"/>\n  <frame tileid=\"2\" duration=\"2\" x=\"\"/>\n  <frame tileid=\"9\" duration=\"9\"/>")]
    [InlineData("1,2", "9,1,2", "\n  <frame tileid=\"9\" duration=\"9\"/>\n  <frame tileid=\"1\" duration=\"1\" x=\"\"/>\n  <frame tileid=\"2\" duration=\"2\" x=\"\"/>")]
    [InlineData("1,2,3", "2,3", "\n  <frame tileid=\"2\" duration=\"2\" x=\"\"/>\n  <frame tileid=\"3\" duration=\"3\" x=\"\"/>")]
    [InlineData("1,2,3", "1,2", "\n  <frame tileid=\"1\" duration=\"1\" x=\"\"/>\n  <frame tileid=\"2\" duration=\"2\" x=\"\"/>")]
    [InlineData("1,2,3", "1,9,3", "\n  <frame tileid=\"1\" duration=\"1\" x=\"\"/>\n  <frame tileid=\"9\" duration=\"9\" x=\"\"/>\n  <frame tileid=\"3\" duration=\"3\" x=\"\"/>")]
    [InlineData("1,2", "2,1", "\n  <frame tileid=\"2\" duration=\"2\" x=\"\"/>\n  <frame tileid=\"1\" duration=\"1\" x=\"\"/>")]
    [InlineData("1", "9,8", "\n  <frame tileid=\"9\" duration=\"9\" x=\"\"/>\n  <frame tileid=\"8\" duration=\"8\"/>")]
    public void Write_FramesChanged_OtherFramesKeepTheirText(string before, string after, string expectedFrames)
    {
        static uint[] Ids(string ids) => ids.Split(',').Select(uint.Parse).ToArray();
        static string Tile(string frames) => $"\n <tile id=\"5\">\n  <animation y=\"\">{frames}\n  </animation>\n </tile>\n";
        var original = string.Concat(Ids(before).Select(id => $"\n  <frame tileid=\"{id}\" duration=\"{id}\" x=\"\"/>"));

        var written = Edit(Tile(original), tileset => Find(tileset, 5).Animation = Frames([.. Ids(after).Select(id => (id, (int)id))]));

        Assert.Equal(Tile(expectedFrames), written);
    }

    [Theory]
    // Nothing but its id is left: the tile goes.
    [InlineData("<tile id=\"5\">\n  <properties>\n   <property name=\"Name\" value=\"A\"/>\n  </properties>\n  <animation>\n   <frame tileid=\"5\" duration=\"1\"/>\n  </animation>\n </tile>", "")]
    [InlineData("<tile id=\"5\"><animation><frame tileid=\"5\" duration=\"1\"/></animation></tile>", "")]
    // Anything else keeps it.
    [InlineData("<tile id=\"5\" future=\"1\">\n  <animation>\n   <frame tileid=\"5\" duration=\"1\"/>\n  </animation>\n </tile>", "\n <tile id=\"5\" future=\"1\"/>")]
    [InlineData("<tile id=\"5\">\n  <!-- a -->\n  <animation>\n   <frame tileid=\"5\" duration=\"1\"/>\n  </animation>\n </tile>", "\n <tile id=\"5\">\n  <!-- a -->\n </tile>")]
    [InlineData("<tile id=\"5\">\n  <objectgroup/>\n  <animation>\n   <frame tileid=\"5\" duration=\"1\"/>\n  </animation>\n </tile>", "\n <tile id=\"5\">\n  <objectgroup/>\n </tile>")]
    public void Write_TileNoLongerInTheModel_IsRemovedOnlyWhenNothingIsLeft(string tile, string expected)
    {
        Assert.Equal(
            $"\n <image/>{expected}\n <tile id=\"6\"/>\n",
            Edit($"\n <image/>\n {tile}\n <tile id=\"6\"/>\n", tileset => tileset.Tiles.RemoveAt(0)));
    }

    [Theory]
    [InlineData("<tile id=\"5\" type=\"A\" />", "<tile id=\"5\" type=\"A\">{0}{1}\n </tile>")]
    [InlineData("<tile id=\"5\"></tile>", "<tile id=\"5\">{0}{1}\n </tile>")]
    [InlineData("<tile id=\"5\" type='a>\"b'></tile>", "<tile id=\"5\" type='a>\"b'>{0}{1}\n </tile>")]
    [InlineData("<tile id=\"5\">\n  <objectgroup/>\n  <!-- a -->\n </tile>", "<tile id=\"5\">{0}\n  <objectgroup/>\n  <!-- a -->{1}\n </tile>")]
    public void Write_TileGetsANameAndAnAnimation_PutsThemWhereTiledDoes(string tile, string expected)
    {
        var written = Edit($"\n {tile}\n", tileset =>
        {
            Find(tileset, 5).Animation = Frames((5, 100));
            Find(tileset, 5).Properties.Add(new StringProperty { Name = "Name", Value = "<\"&'>" });
        });

        Assert.Equal("\n " + string.Format(
            expected,
            "\n  <properties>\n   <property name=\"Name\" value=\"&lt;&quot;&amp;&apos;&gt;\"/>\n  </properties>",
            "\n  <animation>\n   <frame tileid=\"5\" duration=\"100\"/>\n  </animation>") + "\n", written);
    }

    [Theory]
    [InlineData("<property value = 'old' name='Name' x='1'/>", "<property value = 'new' name='Name' x='1'/>")]
    [InlineData("<property x=\" value='a'\" name=\"Name\" value=\"old\" />", "<property x=\" value='a'\" name=\"Name\" value=\"new\" />")]
    [InlineData("<property name=\"Name\">old</property>", "<property name=\"Name\">new</property>")]
    [InlineData("<property name=\"Name\"><![CDATA[old]]></property>", "<property name=\"Name\">new</property>")]
    [InlineData("<property name=\"Name\"/>", "<property value=\"new\" name=\"Name\"/>")]
    [InlineData("<property name=\"Name\" type=\"string\" value=\"&#111;ld\"/>", "<property name=\"Name\" type=\"string\" value=\"new\"/>")]
    public void Write_PropertyValueChanged_ReplacesOnlyTheValue(string property, string expected)
    {
        static string Tile(string content) => $"\n <tile id=\"5\">\n  <properties>\n   <property name=\"B\" type=\"bool\" value=\"true\"/>\n   {content}\n  </properties>\n </tile>\n";

        Assert.Equal(Tile(expected), Edit(Tile(property), tileset => Find(tileset, 5).Properties.OfType<StringProperty>().Single().Value = "new"));
    }

    [Fact]
    public void LoadTileset_PropertyInTextAndCData_ReadsBoth()
    {
        Edit("<tile id='5'><properties><property name='Name'>a <![CDATA[< b]]>&amp;</property></properties></tile>",
            tileset => Assert.Equal("a < b&", Find(tileset, 5).Properties.OfType<StringProperty>().Single().Value));
    }

    [Fact]
    public void Write_OnePropertyReplacesAnother_KeepsTheRest()
    {
        static string Tile(string content) => $"\n <tile id=\"5\">\n  <properties>\n   <property name=\"L\" type=\"list\"/>{content}\n  </properties>\n </tile>\n";

        var written = Edit(Tile("\n   <property name=\"Name\" value=\"A\"/>\n   <!-- a -->"), tileset =>
        {
            Find(tileset, 5).Properties.RemoveAll(property => property.Name == "Name");
            Find(tileset, 5).Properties.Add(new IntProperty { Name = "ParentId", Value = 3 });
        });

        Assert.Equal(Tile("\n   <!-- a -->\n   <property name=\"ParentId\" type=\"int\" value=\"3\"/>"), written);
    }

    [Fact]
    public void Write_AttributeInSingleQuotes_StaysWellFormed()
    {
        var written = Edit(
            "<tile id='5'><properties><property name='Name' value='a'/></properties></tile>",
            tileset => Find(tileset, 5).Properties.OfType<StringProperty>().Single().Value = "it's");

        Assert.Equal("<tile id='5'><properties><property name='Name' value='it&apos;s'/></properties></tile>", written);
    }
}
