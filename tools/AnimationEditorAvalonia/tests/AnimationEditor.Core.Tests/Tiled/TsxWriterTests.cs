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

    private static string WriteFixture(string tempDir)
    {
        var path = Path.Combine(tempDir, "Heroes.tsx");
        File.WriteAllText(path, FixtureXml);
        return path;
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
