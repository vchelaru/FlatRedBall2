using AnimationEditor.Core.Tiled;
using DotTiled;
using DotTiled.Serialization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TsxWriterObjectLayerTests
{
    // One tile holding every object shape Tiled can put in a per-tile collision layer, then a
    // plain tile after it so a patched save has a neighbour to leave alone.
    private const string ShapesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Terrain\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\">\n" +
        " <image source=\"Terrain.png\" width=\"64\" height=\"64\"/>\n" +
        " <tile id=\"1\" type=\"SolidCollision\">\n" +
        "  <objectgroup draworder=\"index\" id=\"2\">\n" +
        "   <object id=\"1\" x=\"0\" y=\"8\" width=\"16\" height=\"8\"/>\n" +
        "   <object id=\"2\" name=\"hurt\" type=\"Hurtbox\" x=\"1\" y=\"2\" width=\"4\" height=\"6\" rotation=\"15\" visible=\"0\">\n" +
        "    <properties>\n" +
        "     <property name=\"damage\" type=\"int\" value=\"3\"/>\n" +
        "    </properties>\n" +
        "    <ellipse/>\n" +
        "   </object>\n" +
        "   <object id=\"3\" x=\"3\" y=\"4\">\n" +
        "    <point/>\n" +
        "   </object>\n" +
        "   <object id=\"4\" x=\"0\" y=\"0\">\n" +
        "    <polygon points=\"0,0 16,16 0,16\"/>\n" +
        "   </object>\n" +
        "   <object id=\"5\" x=\"2\" y=\"2\">\n" +
        "    <polyline points=\"0,0 4,4 8,0\"/>\n" +
        "   </object>\n" +
        "   <object id=\"6\" x=\"2\" y=\"2\" width=\"20\" height=\"10\">\n" +
        "    <text fontfamily=\"Arial\" pixelsize=\"12\" wrap=\"1\" color=\"#ff0000\" bold=\"1\" halign=\"center\" valign=\"bottom\">Hello</text>\n" +
        "   </object>\n" +
        "  </objectgroup>\n" +
        " </tile>\n" +
        " <tile id=\"2\" type=\"Rock\"/>\n" +
        "</tileset>\n";

    private static Tileset Load(string xml, out string path)
    {
        path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "Terrain.tsx");
        File.WriteAllText(path, xml);
        return Loader.Default().LoadTileset(path);
    }

    private static string WriteToText(Tileset tileset)
    {
        using var ms = new MemoryStream();
        TsxWriter.Write(tileset, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static Tileset Reload(string text)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "Out.tsx");
        File.WriteAllText(path, text);
        return Loader.Default().LoadTileset(path);
    }

    [Fact]
    public void Write_ToStream_EveryObjectShapeRoundTrips()
    {
        var tileset = Load(ShapesXml, out _);

        var text = WriteToText(tileset);
        var reloaded = Reload(text);

        var objects = reloaded.Tiles.Single(t => t.ID == 1).ObjectLayer.Value.Objects;
        Assert.IsType<RectangleObject>(objects[0]);
        var ellipse = Assert.IsType<EllipseObject>(objects[1]);
        Assert.Equal("hurt", ellipse.Name);
        Assert.Equal("Hurtbox", ellipse.Type);
        Assert.Equal(15f, ellipse.Rotation);
        Assert.False(ellipse.Visible);
        Assert.Equal(3, ellipse.GetProperty<IntProperty>("damage").Value);
        Assert.IsType<PointObject>(objects[2]);
        Assert.Equal(3, Assert.IsType<PolygonObject>(objects[3]).Points.Count);
        Assert.Equal(3, Assert.IsType<PolylineObject>(objects[4]).Points.Count);
        var label = Assert.IsType<TextObject>(objects[5]);
        Assert.Equal("Hello", label.Text);
        Assert.Equal("Arial", label.FontFamily);
        Assert.Equal(12, label.PixelSize);
        Assert.True(label.Wrap);
        Assert.True(label.Bold);
        Assert.Equal(TextHorizontalAlignment.Center, label.HorizontalAlignment);
        Assert.Equal(TextVerticalAlignment.Bottom, label.VerticalAlignment);
        // Writing what was read must reach a fixed point.
        Assert.Equal(text, WriteToText(reloaded));
    }

    [Fact]
    public void Write_ExistingFileUnedited_KeepsBytesIdentical()
    {
        var tileset = Load(ShapesXml, out var path);

        TsxWriter.Write(tileset, path);

        Assert.Equal(ShapesXml, File.ReadAllText(path));
    }

    [Fact]
    public void Write_OtherTileEdited_KeepsObjectLayerBytes()
    {
        var tileset = Load(ShapesXml, out var path);
        tileset.Tiles.Single(t => t.ID == 2).Type = "Door";

        TsxWriter.Write(tileset, path);

        Assert.Equal(ShapesXml.Replace("type=\"Rock\"/>", "type=\"Door\" />"), File.ReadAllText(path));
    }

    [Fact]
    public void Write_ObjectEditedAndNothingElse_IsSaved()
    {
        var tileset = Load(ShapesXml, out var path);
        var layer = tileset.Tiles.Single(t => t.ID == 1).ObjectLayer.Value;
        layer.Objects[0].X = 4;

        TsxWriter.Write(tileset, path);

        var reloaded = Loader.Default().LoadTileset(path);
        Assert.Equal(4f, reloaded.Tiles.Single(t => t.ID == 1).ObjectLayer.Value.Objects[0].X);
    }

    [Fact]
    public void Write_ObjectRemovedAndNothingElse_IsSaved()
    {
        var tileset = Load(ShapesXml, out var path);
        tileset.Tiles.Single(t => t.ID == 1).ObjectLayer.Value.Objects.RemoveAt(5);

        TsxWriter.Write(tileset, path);

        var reloaded = Loader.Default().LoadTileset(path);
        Assert.Equal(5, reloaded.Tiles.Single(t => t.ID == 1).ObjectLayer.Value.Objects.Count);
    }

    [Fact]
    public void Write_PolygonPointEditedAndNothingElse_IsSaved()
    {
        var tileset = Load(ShapesXml, out var path);
        var polygon = (PolygonObject)tileset.Tiles.Single(t => t.ID == 1).ObjectLayer.Value.Objects[3];
        polygon.Points[1] = new System.Numerics.Vector2(8, 8);

        TsxWriter.Write(tileset, path);

        var reloaded = Loader.Default().LoadTileset(path);
        var saved = (PolygonObject)reloaded.Tiles.Single(t => t.ID == 1).ObjectLayer.Value.Objects[3];
        Assert.Equal(new System.Numerics.Vector2(8, 8), saved.Points[1]);
    }

    [Fact]
    public void CheckOpenCompatibility_TileWithObjectLayer_CanOpen()
    {
        var tileset = Load(ShapesXml, out _);

        var canOpen = TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var reason);

        Assert.True(canOpen, reason);
    }
}
