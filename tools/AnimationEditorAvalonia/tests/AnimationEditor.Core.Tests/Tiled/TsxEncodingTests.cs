using AnimationEditor.Core.Paths;
using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>#2015: the editor writes UTF-8 only, so a .tsx in any other encoding is refused before it can be rewritten.</summary>
public sealed class TsxEncodingTests : IDisposable
{
    private const string OnlyUtf8 = "AnimationEditor only supports UTF-8";

    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private static string Tsx(string declaration) => declaration + """
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0" type="Größe">
          <animation>
           <frame tileid="0" duration="200"/>
          </animation>
         </tile>
        </tileset>

        """.ReplaceLineEndings("\n");

    private static string Declare(string encoding) => $"<?xml version=\"1.0\" encoding=\"{encoding}\"?>\n";

    private static byte[] Encode(string text, Encoding encoding, bool bom) =>
        [.. bom ? encoding.GetPreamble() : [], .. encoding.GetBytes(text)];

    private string Write(byte[] bytes, string name = "Heroes.tsx")
    {
        var path = Path.Combine(_dir.Path, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>The file's bytes and the encoding the refusal has to name.</summary>
    private static (byte[] Bytes, string Named) RefusedFile(string name) => name switch
    {
        "UTF-16 LE BOM" => (Encode(Tsx(Declare("UTF-16")), Encoding.Unicode, bom: true), "UTF-16"),
        "UTF-16 BE BOM" => (Encode(Tsx(Declare("UTF-16")), Encoding.BigEndianUnicode, bom: true), "UTF-16"),
        "UTF-16 LE" => (Encode(Tsx(Declare("UTF-16")), Encoding.Unicode, bom: false), "UTF-16"),
        "UTF-16 BE" => (Encode(Tsx(Declare("UTF-16")), Encoding.BigEndianUnicode, bom: false), "UTF-16"),
        "UTF-16 LE BOM, no declaration" => (Encode(Tsx(""), Encoding.Unicode, bom: true), "UTF-16"),
        "UTF-32 LE BOM" => (Encode(Tsx(Declare("UTF-32")), Encoding.UTF32, bom: true), "UTF-32"),
        "UTF-32 BE" => (Encode(Tsx(Declare("UTF-32")), new UTF32Encoding(bigEndian: true, byteOrderMark: false), bom: false), "UTF-32"),
        "ISO-8859-1" => (Encode(Tsx(Declare("ISO-8859-1")), Encoding.Latin1, bom: false), "ISO-8859-1"),
        // Windows-1252 has the same bytes as Latin-1 for these characters.
        "Windows-1252" => (Encode(Tsx(Declare("Windows-1252")), Encoding.Latin1, bom: false), "Windows-1252"),
        // ASCII bytes today, but the next non-ASCII name would be written as UTF-8 under this declaration.
        "ISO-8859-1, ASCII only" => (Encode(Tsx(Declare("ISO-8859-1")).Replace("Größe", "Size"), Encoding.Latin1, bom: false), "ISO-8859-1"),
        "ISO-8859-1, single quotes and spaces" => (Encode(Tsx("<?xml version='1.0'\n encoding = 'ISO-8859-1'?>\n"), Encoding.Latin1, bom: false), "ISO-8859-1"),
        "no declaration, invalid UTF-8" => (Encode(Tsx(""), Encoding.Latin1, bom: false), "not valid UTF-8"),
        "no declared encoding, invalid UTF-8" => (Encode(Tsx("<?xml version=\"1.0\"?>\n"), Encoding.Latin1, bom: false), "not valid UTF-8"),
        "declared UTF-8, invalid UTF-8" => (Encode(Tsx(Declare("UTF-8")), Encoding.Latin1, bom: false), "not valid UTF-8"),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    public static TheoryData<string> RefusedNames =>
    [
        "UTF-16 LE BOM", "UTF-16 BE BOM", "UTF-16 LE", "UTF-16 BE", "UTF-16 LE BOM, no declaration", "UTF-32 LE BOM", "UTF-32 BE",
        "ISO-8859-1", "Windows-1252", "ISO-8859-1, ASCII only", "ISO-8859-1, single quotes and spaces",
        "no declaration, invalid UTF-8", "no declared encoding, invalid UTF-8", "declared UTF-8, invalid UTF-8",
    ];

    [Theory]
    [MemberData(nameof(RefusedNames))]
    public void LoadTsxProject_NotUtf8_RefusesNamingTheEncodingAndLeavesTheFile(string name)
    {
        var (bytes, named) = RefusedFile(name);
        var path = Write(bytes);
        var pm = new ProjectManager();

        var ex = Should.Throw<NotSupportedException>(() => pm.LoadTsxProject(new FilePath(path)));

        ex.Message.ShouldStartWith("Can't open \"");
        ex.Message.ShouldContain(named);
        ex.Message.ShouldContain(OnlyUtf8);
        pm.IsNativeTsxProject.ShouldBeFalse();
        File.ReadAllBytes(path).ShouldBe(bytes);
    }

    [Theory]
    [MemberData(nameof(RefusedNames))]
    public void SyncAll_NotUtf8Tileset_FailsNamingTheEncodingAndLeavesTheFile(string name)
    {
        var (bytes, named) = RefusedFile(name);
        var path = Write(bytes);

        var outcome = TiledTilesetSyncRunner.SyncAll(WalkOnTile1(), Path.Combine(_dir.Path, "Hero.achx"), [path]).Single();

        outcome.Success.ShouldBeFalse();
        outcome.Error!.Message.ShouldContain(named);
        outcome.Error!.Message.ShouldContain(OnlyUtf8);
        File.ReadAllBytes(path).ShouldBe(bytes);
    }

    [Theory]
    [InlineData("<?xml version=\"1.0\"?>\n")]
    [InlineData("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n")]
    [InlineData("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n")]
    [InlineData("<?xml version='1.0' encoding='Utf-8' standalone='yes'?>\n")]
    // Only the XML declaration decides, not another instruction's own "encoding".
    [InlineData("<?xml version=\"1.0\"?>\n<?tool encoding=\"UTF-16\"?>\n")]
    [InlineData("<?xml-stylesheet encoding=\"UTF-16\"?>\n")]
    public void SaveTsxProject_Utf8WithOrWithoutBom_SavesTheEditWithTheRestUnchanged(string declaration)
    {
        foreach (var bom in new[] { false, true })
        {
            var path = Write(Encode(Tsx(declaration), Encoding.UTF8, bom));
            var pm = new ProjectManager();
            pm.LoadTsxProject(new FilePath(path));
            pm.AnimationChainListSave!.AnimationChains.Single().Frames[0].FrameLength = 0.3f;

            pm.SaveTsxProject();

            var expected = Tsx(declaration).Replace("duration=\"200\"/>", "duration=\"300\"/>");
            File.ReadAllBytes(path).ShouldBe(Encode(expected, Encoding.UTF8, bom));
        }
    }

    // Save As over a UTF-16 file: patching it would keep its UTF-16 declaration above UTF-8 bytes.
    [Fact]
    public void SaveTsxProject_OverANotUtf8Tileset_ReplacesItWithAReadableUtf8File()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(Write(Encode(Tsx(Declare("UTF-8")), Encoding.UTF8, bom: false))));
        var target = Write(Encode(Tsx(Declare("UTF-16")), Encoding.Unicode, bom: true), "Copy.tsx");

        pm.SaveTsxProject(target);

        TsxLoader.LoadTileset(target).Tiles.Single().Type.ShouldBe("Größe");
    }

    private static AnimationChainListSave WalkOnTile1()
    {
        var save = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 16, TopCoordinate = 0, RightCoordinate = 32, BottomCoordinate = 16,
        });
        save.AnimationChains.Add(chain);
        return save;
    }
}
