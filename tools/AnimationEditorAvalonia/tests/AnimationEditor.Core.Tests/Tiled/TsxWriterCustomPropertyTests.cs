using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TsxWriterCustomPropertyTests
{
    // Tiled stores a class property as type="class" propertytype=... with nested <properties>,
    // a string-backed enum as a string property with propertytype, and an int-backed enum as an
    // int property with propertytype. The type definitions live in the Tiled project, not the tsx.
    private const string Xml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Terrain\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\">\n" +
        " <properties>\n" +
        "  <property name=\"stats\" type=\"class\" propertytype=\"Stats\">\n" +
        "   <properties>\n" +
        "    <property name=\"hp\" type=\"int\" value=\"5\"/>\n" +
        "    <property name=\"inner\" type=\"class\" propertytype=\"Inner\">\n" +
        "     <properties>\n" +
        "      <property name=\"flag\" type=\"bool\" value=\"true\"/>\n" +
        "     </properties>\n" +
        "    </property>\n" +
        "   </properties>\n" +
        "  </property>\n" +
        "  <property name=\"biome\" propertytype=\"Biome\" value=\"Forest\"/>\n" +
        "  <property name=\"level\" type=\"int\" propertytype=\"Level\" value=\"2\"/>\n" +
        " </properties>\n" +
        " <image source=\"Terrain.png\" width=\"64\" height=\"64\"/>\n" +
        " <tile id=\"1\" type=\"Rock\">\n" +
        "  <properties>\n" +
        "   <property name=\"stats\" type=\"class\" propertytype=\"Stats\">\n" +
        "    <properties>\n" +
        "     <property name=\"hp\" type=\"int\" value=\"9\"/>\n" +
        "    </properties>\n" +
        "   </property>\n" +
        "   <property name=\"biome\" propertytype=\"Biome\" value=\"Desert,Forest\"/>\n" +
        "   <property name=\"level\" type=\"int\" propertytype=\"Level\" value=\"1\"/>\n" +
        "  </properties>\n" +
        " </tile>\n" +
        " <tile id=\"2\" type=\"Chest\"/>\n" +
        "</tileset>\n";

    private static Tileset Load(string xml, out string path)
    {
        path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "Terrain.tsx");
        File.WriteAllText(path, xml);
        return TsxLoader.LoadTileset(path);
    }

    [Fact]
    public void CheckOpenCompatibility_ClassAndEnumProperties_CanOpen()
    {
        var tileset = Load(Xml, out _);

        var canOpen = TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason);

        Assert.True(canOpen, blockingReason);
    }

    [Fact]
    public void Write_ExistingFileNoChange_KeepsBytesIdentical()
    {
        var tileset = Load(Xml, out var path);

        TsxWriter.Write(tileset, path);

        Assert.Equal(Xml, File.ReadAllText(path));
    }

    [Fact]
    public void Write_ToStream_KeepsClassAndEnumProperties()
    {
        var tileset = Load(Xml, out _);
        using var ms = new MemoryStream();

        TsxWriter.Write(tileset, ms);

        var written = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        foreach (var fragment in new[]
        {
            "type=\"class\" propertytype=\"Stats\"", "type=\"class\" propertytype=\"Inner\"",
            "name=\"hp\" type=\"int\" value=\"5\"", "name=\"hp\" type=\"int\" value=\"9\"",
            "name=\"flag\" type=\"bool\" value=\"true\"",
            "name=\"biome\" propertytype=\"Biome\" value=\"Forest\"",
            "name=\"biome\" propertytype=\"Biome\" value=\"Desert,Forest\"",
            "name=\"level\" type=\"int\" propertytype=\"Level\" value=\"2\"",
            "name=\"level\" type=\"int\" propertytype=\"Level\" value=\"1\"",
        })
            Assert.Contains(fragment, written);
    }

    [Fact]
    public void Write_TileEdited_KeepsTilesetAndTileCustomProperties()
    {
        var tileset = Load(Xml, out var path);
        tileset.Tiles.Single(t => t.ID == 1).Type = "Boulder";
        tileset.Tiles.Single(t => t.ID == 2).Type = "Door";

        TsxWriter.Write(tileset, path);

        var raw = File.ReadAllText(path);
        Assert.Contains("name=\"level\" type=\"int\" propertytype=\"Level\" value=\"2\"", raw);
        Assert.Contains("name=\"level\" type=\"int\" propertytype=\"Level\" value=\"1\"", raw);
        Assert.Contains("name=\"biome\" propertytype=\"Biome\" value=\"Desert,Forest\"", raw);
        Assert.Contains("type=\"Boulder\"", raw);
    }

    [Fact]
    public void Write_ClassMemberEdited_IsSaved()
    {
        var tileset = Load(Xml, out var path);
        var stats = (ClassProperty)tileset.Tiles.Single(t => t.ID == 1).Properties.Single(p => p.Name == "stats");
        ((IntProperty)stats.Value.Single(p => p.Name == "hp")).Value = 12;

        TsxWriter.Write(tileset, path);

        Assert.Contains("name=\"hp\" type=\"int\" value=\"12\"", File.ReadAllText(path));
    }
}
