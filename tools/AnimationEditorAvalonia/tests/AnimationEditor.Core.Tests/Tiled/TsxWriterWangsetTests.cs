using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TsxWriterWangsetTests
{
    // Modeled on the CollisionSet wangset in StandardTileset.tsx: "mixed" type, a wangcolor with
    // no class, 8-entry wangids, and the wangsets block AFTER the last <tile>.
    private const string WangsetXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Terrain\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\">\n" +
        " <image source=\"Terrain.png\" width=\"64\" height=\"64\"/>\n" +
        " <tile id=\"1\" type=\"Rock\"/>\n" +
        " <tile id=\"2\" type=\"Chest\"/>\n" +
        " <wangsets>\n" +
        "  <wangset name=\"CollisionSet\" type=\"mixed\" tile=\"-1\">\n" +
        "   <wangcolor name=\"SolidCollision\" color=\"#ff0000\" tile=\"-1\" probability=\"1\"/>\n" +
        "   <wangtile tileid=\"0\" wangid=\"1,1,1,1,1,1,1,1\"/>\n" +
        "   <wangtile tileid=\"73\" wangid=\"1,1,0,0,0,1,1,1\"/>\n" +
        "  </wangset>\n" +
        "  <wangset name=\"Ground\" class=\"Terrain\" type=\"corner\" tile=\"3\">\n" +
        "   <properties>\n" +
        "    <property name=\"layer\" type=\"int\" value=\"2\"/>\n" +
        "   </properties>\n" +
        "   <wangcolor name=\"Grass\" class=\"Soft\" color=\"#00ff00\" tile=\"0\" probability=\"0.5\">\n" +
        "    <properties>\n" +
        "     <property name=\"speed\" type=\"float\" value=\"1.5\"/>\n" +
        "    </properties>\n" +
        "   </wangcolor>\n" +
        "   <wangtile tileid=\"4\" wangid=\"0,1,0,1,0,1,0,1\"/>\n" +
        "  </wangset>\n" +
        " </wangsets>\n" +
        "</tileset>\n";

    private static Tileset Load(string xml, out string path)
    {
        path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "Terrain.tsx");
        File.WriteAllText(path, xml);
        return Loader.Default().LoadTileset(path);
    }

    private static void AssertWangsetsEqual(Tileset expected, Tileset actual)
    {
        Assert.Equal(expected.Wangsets.Count, actual.Wangsets.Count);
        foreach (var (e, a) in expected.Wangsets.Zip(actual.Wangsets))
        {
            Assert.Equal(e.Name, a.Name);
            Assert.Equal(e.Class, a.Class);
            Assert.Equal(e.Tile, a.Tile);
            Assert.Equal(e.Properties.Count, a.Properties.Count);
            Assert.Equal(e.WangColors.Count, a.WangColors.Count);
            foreach (var (ec, ac) in e.WangColors.Zip(a.WangColors))
            {
                Assert.Equal(ec.Name, ac.Name);
                Assert.Equal(ec.Class, ac.Class);
                Assert.Equal(ec.Color.ToString(), ac.Color.ToString());
                Assert.Equal(ec.Tile, ac.Tile);
                Assert.Equal(ec.Probability, ac.Probability);
                Assert.Equal(ec.Properties.Count, ac.Properties.Count);
            }
            Assert.Equal(e.WangTiles.Count, a.WangTiles.Count);
            foreach (var (et, at) in e.WangTiles.Zip(a.WangTiles))
            {
                Assert.Equal(et.TileID, at.TileID);
                Assert.Equal(et.WangID, at.WangID);
            }
        }
    }

    [Fact]
    public void Write_ToStream_WangsetsRoundTrip()
    {
        var tileset = Load(WangsetXml, out _);
        using var ms = new MemoryStream();

        TsxWriter.Write(tileset, ms);

        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "Out.tsx");
        File.WriteAllBytes(path, ms.ToArray());
        AssertWangsetsEqual(tileset, Loader.Default().LoadTileset(path));
    }

    [Fact]
    public void Write_ExistingFileNoChange_KeepsBytesIdentical()
    {
        var tileset = Load(WangsetXml, out var path);

        TsxWriter.Write(tileset, path);

        Assert.Equal(WangsetXml, File.ReadAllText(path));
    }

    [Fact]
    public void Write_LastTileEdited_KeepsWangsetsVerbatim()
    {
        var tileset = Load(WangsetXml, out var path);
        tileset.Tiles.Single(t => t.ID == 2).Type = "Door";

        TsxWriter.Write(tileset, path);

        Assert.Equal(WangsetXml.Replace("type=\"Chest\"/>", "type=\"Door\" />"), File.ReadAllText(path));
    }

    [Fact]
    public void Write_WangtileAdded_IsSavedAndKeepsWangsetType()
    {
        var tileset = Load(WangsetXml, out var path);
        tileset.Wangsets[0].WangTiles.Add(new WangTile { TileID = 9, WangID = [0, 0, 0, 1, 0, 0, 0, 0] });

        TsxWriter.Write(tileset, path);

        var raw = File.ReadAllText(path);
        Assert.Contains("type=\"mixed\"", raw);
        Assert.Contains("type=\"corner\"", raw);
        var reloaded = Loader.Default().LoadTileset(path);
        Assert.Contains(reloaded.Wangsets[0].WangTiles, w => w.TileID == 9 && w.WangID.SequenceEqual(new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 }));
        AssertWangsetsEqual(tileset, reloaded);
    }

    [Fact]
    public void Write_WangsetRemoved_IsSaved()
    {
        var tileset = Load(WangsetXml, out var path);
        tileset.Wangsets.RemoveAt(1);

        TsxWriter.Write(tileset, path);

        var reloaded = Loader.Default().LoadTileset(path);
        Assert.Equal(["CollisionSet"], reloaded.Wangsets.Select(w => w.Name));
    }

    [Fact]
    public void CheckOpenCompatibility_TilesetWithWangsets_CanOpen()
    {
        var tileset = Load(WangsetXml, out _);

        var canOpen = TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason);

        Assert.True(canOpen, blockingReason);
    }
}
