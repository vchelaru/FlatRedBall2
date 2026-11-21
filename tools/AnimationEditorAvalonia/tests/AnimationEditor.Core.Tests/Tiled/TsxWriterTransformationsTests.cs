using AnimationEditor.Core.Tiled;
using DotTiled;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class TsxWriterTransformationsTests
{
    private static string Xml(string transformationsLine) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<tileset version=\"1.10\" tiledversion=\"1.12.2\" name=\"Terrain\" tilewidth=\"16\" tileheight=\"16\" tilecount=\"16\" columns=\"4\">\n" +
        " <image source=\"Terrain.png\" width=\"64\" height=\"64\"/>\n" +
        (transformationsLine.Length == 0 ? "" : " " + transformationsLine + "\n") +
        " <tile id=\"1\" type=\"Rock\"/>\n" +
        "</tileset>\n";

    private static string WriteFixture(string xml)
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory().FullName, "Terrain.tsx");
        File.WriteAllText(path, xml);
        return path;
    }

    public static TheoryData<string, bool, bool, bool, bool> Cases => new()
    {
        { "<transformations hflip=\"1\" vflip=\"0\" rotate=\"0\" preferuntransformed=\"0\"/>", true, false, false, false },
        { "<transformations hflip=\"0\" vflip=\"1\" rotate=\"0\" preferuntransformed=\"0\"/>", false, true, false, false },
        { "<transformations hflip=\"0\" vflip=\"0\" rotate=\"1\" preferuntransformed=\"0\"/>", false, false, true, false },
        { "<transformations hflip=\"0\" vflip=\"0\" rotate=\"0\" preferuntransformed=\"1\"/>", false, false, false, true },
        { "<transformations hflip=\"1\" vflip=\"1\" rotate=\"1\" preferuntransformed=\"1\"/>", true, true, true, true },
        { "<transformations hflip=\"0\" vflip=\"0\" rotate=\"0\" preferuntransformed=\"0\"/>", false, false, false, false },
        { "<transformations hflip=\"1\" rotate=\"1\"/>", true, false, true, false },
        { "<transformations/>", false, false, false, false },
    };

    private static void AssertFlags(Tileset tileset, bool h, bool v, bool r, bool p)
    {
        Assert.True(tileset.Transformations.HasValue);
        var t = tileset.Transformations!.Value;
        Assert.Equal((h, v, r, p), (t.HFlip, t.VFlip, t.Rotate, t.PreferUntransformed));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Write_ToNewFile_KeepsTransformations(string line, bool h, bool v, bool r, bool p)
    {
        var tileset = TsxLoader.LoadTileset(WriteFixture(Xml(line)));
        var outPath = Path.Combine(Directory.CreateTempSubdirectory().FullName, "Out.tsx");

        TsxWriter.Write(tileset, outPath);

        AssertFlags(TsxLoader.LoadTileset(outPath), h, v, r, p);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Write_ExistingFileAfterTileEdit_KeepsTransformations(string line, bool h, bool v, bool r, bool p)
    {
        var path = WriteFixture(Xml(line));
        var tileset = TsxLoader.LoadTileset(path);
        tileset.Tiles[0].Type = "Door";

        TsxWriter.Write(tileset, path);

        AssertFlags(TsxLoader.LoadTileset(path), h, v, r, p);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Write_ExistingFileNoChange_KeepsBytesIdentical(string line, bool _h, bool _v, bool _r, bool _p)
    {
        _ = (_h, _v, _r, _p);
        var xml = Xml(line);
        var path = WriteFixture(xml);

        TsxWriter.Write(TsxLoader.LoadTileset(path), path);

        Assert.Equal(xml, File.ReadAllText(path));
    }

    [Fact]
    public void Write_AbsentElement_StaysAbsent()
    {
        var path = WriteFixture(Xml(""));
        var tileset = TsxLoader.LoadTileset(path);
        Assert.False(tileset.Transformations.HasValue);

        TsxWriter.Write(tileset, path);

        Assert.DoesNotContain("transformations", File.ReadAllText(path));
    }

    [Fact]
    public void Write_TransformationsChanged_IsSaved()
    {
        var path = WriteFixture(Xml("<transformations hflip=\"1\" vflip=\"0\" rotate=\"0\" preferuntransformed=\"0\"/>"));
        var tileset = TsxLoader.LoadTileset(path);
        tileset.Transformations = new Transformations { HFlip = true, VFlip = true, Rotate = false, PreferUntransformed = false };

        TsxWriter.Write(tileset, path);

        AssertFlags(TsxLoader.LoadTileset(path), true, true, false, false);
    }

    [Fact]
    public void CheckOpenCompatibility_TilesetWithTransformations_CanOpen()
    {
        var tileset = TsxLoader.LoadTileset(WriteFixture(Xml("<transformations hflip=\"1\" vflip=\"0\" rotate=\"0\" preferuntransformed=\"0\"/>")));

        var canOpen = TsxCompatibilityChecker.CheckOpenCompatibility(tileset, out var blockingReason);

        Assert.True(canOpen, blockingReason);
    }
}
