using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json;
using System.Xml.Linq;

namespace AnimationEditor.DocumentMatrix;

/// <summary>
/// Every kind of document a tab can hold. <c>DocumentWriteGuardTests</c> fails when File &gt; Load opens a format that is
/// not here, and a new value here becomes a row of every action.
/// </summary>
public enum DocType { Achx, Achj, Tsx, Seqj, Untitled }

/// <summary>
/// Every action that writes or moves a document's content, each run for every (front, background) pair of
/// <see cref="DocType"/>: up to <see cref="PushToTileset"/> in <c>DocumentWriteMatrixTests</c> (Core), the rest, which
/// need the window, in <c>DocumentWriteMatrixScenarioTests</c>. A value without rows fails there.
/// </summary>
public enum DocAction
{
    Save,
    Autosave,
    SaveAs,
    SaveAsOntoAnotherFormat,
    SaveBackgroundDocument,
    Reload,
    SwitchTabs,
    WriteRecovery,
    Export,
    ConvertToAchj,
    PushToTileset,

    CutChain,
    CutChainATilesetCannotStore,
    CutFrame,
    CutShape,
    CutSequence,
    SaveAsOntoOpenTab,
    CloseTabWithUnsavedEdits,
    CloseWindowWithUnsavedEdits,
    Detach,
    LeaveDuringRename,
    ConvertOpenTabToAchj,
    RecoverAtStartup,
}

/// <summary>
/// The documents the matrix opens, and what it reads back. Content is a set of facts ("cell sheet.png 0,0",
/// "shape Hit-front", "sequence front"), so it compares across formats and between files and the editor's memory.
/// </summary>
public static class Documents
{
    // 64x64, so 4x4 tiles of 16.
    private const string SheetPng =
        "iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAYAAACqaXHeAAAAZklEQVR42u3QQREAAAQAMGFVElMGcjh7rMAiq+ezECBAgAABAgQIECBAgAABAgQIECBAgAABAgQIECBAgAABAgQIECBAgAABAgQIECBAgAABAgQIECBAgAABAgQIECBAgAABAu4HLCkwUrPqydA0AAAAAElFTkSuQmCC";

    public const string FrontName = "front";
    public const string BackgroundName = "background";

    public static string Extension(this DocType type) => type == DocType.Untitled
        ? throw new InvalidOperationException("An Untitled document has no file.")
        : "." + type.ToString().ToLowerInvariant();

    /// <summary>The rows of <paramref name="actions"/>: every pair of types <paramref name="applies"/> takes.</summary>
    public static IEnumerable<object[]> Rows(IEnumerable<DocAction> actions, Func<DocAction, DocType, DocType, bool> applies) =>
        from action in actions
        from front in Enum.GetValues<DocType>()
        from background in Enum.GetValues<DocType>()
        where applies(action, front, background)
        select new object[] { action, front, background };

    public static void WriteTextures(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (string name in new[] { "sheet.png", "other.png" })
            File.WriteAllBytes(Path.Combine(folder, name), Convert.FromBase64String(SheetPng));
    }

    /// <summary>The document's own row of tiles, so two documents never hold the same cell.</summary>
    private static int RowOf(string name) => name switch { FrontName => 0, BackgroundName => 2, _ => 3 };

    /// <summary>Two frames on the document's own tiles. A .tsx cannot store another texture or a shape.</summary>
    public static AnimationChainSave Chain(string name, string texture = "sheet.png", bool withShape = false)
    {
        int top = RowOf(name) * 16;
        var chain = new AnimationChainSave { Name = "Walk-" + name };
        foreach (int left in new[] { 0, 16 })
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName = texture,
                FrameLength = 0.1f,
                LeftCoordinate = left,
                RightCoordinate = left + 16,
                TopCoordinate = top,
                BottomCoordinate = top + 16,
            });
        if (withShape)
        {
            chain.Frames[0].ShapesSave = new ShapesSave();
            chain.Frames[0].ShapesSave!.Add(new AARectSave { Name = "Hit-" + name, ScaleX = 4, ScaleY = 4 });
        }
        return chain;
    }

    /// <summary>The top left pixel of a frame no document starts with, for an edit.</summary>
    public static (int Left, int Top) NewCell(string name) => (32, RowOf(name) * 16);

    public static string NewCellFact(string name) => $"cell sheet.png {NewCell(name).Left},{NewCell(name).Top}";

    /// <summary>Writes the document <paramref name="name"/> of <paramref name="type"/> into <paramref name="folder"/> and returns its path.</summary>
    public static string Write(string folder, DocType type, string name, AnimationChainSave? chain = null)
    {
        WriteTextures(folder);
        string path = Path.Combine(folder, name + type.Extension());
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(chain ?? Chain(name));
        switch (type)
        {
            case DocType.Achx:
                list.Save(path);
                break;
            case DocType.Achj:
                list.SaveJson(path);
                break;
            case DocType.Tsx:
                // Owned by a tile that is not its own first frame, so it survives only through the tab's own tile ids.
                int tile = RowOf(name) * 4;
                File.WriteAllText(path, $"""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <tileset version="1.10" tiledversion="1.12.2" name="{name}" tilewidth="16" tileheight="16" tilecount="16" columns="4">
                     <image source="sheet.png" width="64" height="64"/>
                     <tile id="{tile + 3}">
                      <properties>
                       <property name="Name" value="Walk-{name}"/>
                      </properties>
                      <animation>
                       <frame tileid="{tile}" duration="100"/>
                       <frame tileid="{tile + 1}" duration="100"/>
                      </animation>
                     </tile>
                    </tileset>
                    """);
                break;
            case DocType.Seqj:
                new SequenceListSave { Sequences = { new SequenceSave { Name = name } } }.Save(path);
                break;
            default:
                throw new InvalidOperationException($"The matrix has no fixture for {type}.");
        }
        return path;
    }

    /// <summary>Parses <paramref name="path"/> strictly as what its extension says and returns its content.</summary>
    public static HashSet<string> ReadAsItsOwnFormat(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".achx":
                RootOf(path).Name.LocalName.ShouldBe("AnimationChainArraySave", $"{path} is not an .achx");
                return Content(AnimationChainListSave.FromFile(path));
            case ".achj":
                JsonKeysOf(path).ShouldContain("animationchains", $"{path} is not an .achj");
                return Content(AnimationChainListSave.FromFile(path));
            case ".seqj":
                JsonKeysOf(path).ShouldContain("sequences", $"{path} is not a .seqj");
                return Content(SequenceListSave.FromFile(path));
            case ".tsx":
                XElement root = RootOf(path);
                root.Name.LocalName.ShouldBe("tileset", $"{path} is not a .tsx");
                TsxLoader.LoadTileset(path);
                return Content(root);
            default:
                throw new InvalidOperationException($"The matrix cannot read {path}.");
        }
    }

    private static XElement RootOf(string path) => XDocument.Parse(File.ReadAllText(path)).Root!;

    private static List<string> JsonKeysOf(string path)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        return json.RootElement.EnumerateObject().Select(property => property.Name.ToLowerInvariant()).ToList();
    }

    public static HashSet<string> Content(AnimationChainListSave? list)
    {
        var facts = new HashSet<string>();
        foreach (var chain in list?.AnimationChains ?? [])
        {
            facts.Add("chain " + chain.Name);
            foreach (var frame in chain.Frames)
            {
                facts.Add($"cell {Path.GetFileName(frame.TextureName)} {frame.LeftCoordinate:0},{frame.TopCoordinate:0}");
                foreach (var shape in frame.ShapesSave?.Shapes.OfType<ShapeSave>() ?? []) facts.Add("shape " + shape.Name);
            }
        }
        return facts;
    }

    public static HashSet<string> Content(SequenceListSave list) =>
        list.Sequences.Select(sequence => "sequence " + sequence.Name).ToHashSet();

    private static HashSet<string> Content(XElement tileset)
    {
        string image = Path.GetFileName((string)tileset.Element("image")!.Attribute("source")!);
        int columns = (int)tileset.Attribute("columns")!;
        var facts = new HashSet<string>();
        foreach (var tile in tileset.Elements("tile").Where(tile => tile.Element("animation") != null))
        {
            var name = tile.Element("properties")?.Elements("property").FirstOrDefault(property => (string?)property.Attribute("name") == "Name");
            if (name != null) facts.Add("chain " + (string?)name.Attribute("value"));
            foreach (int id in tile.Element("animation")!.Elements("frame").Select(frame => (int)frame.Attribute("tileid")!))
                facts.Add($"cell {image} {id % columns * 16},{id / columns * 16}");
        }
        return facts;
    }

    /// <summary>Every document file under <paramref name="folder"/> with its bytes, for "no other file changed".</summary>
    public static Dictionary<string, byte[]> FileBytes(string folder) => DocumentFiles(folder).ToDictionary(path => path, File.ReadAllBytes);

    /// <summary>What the document files under <paramref name="folder"/> hold together, each parsed as its own format.</summary>
    public static HashSet<string> FileContent(string folder) => DocumentFiles(folder).SelectMany(ReadAsItsOwnFormat).ToHashSet();

    private static IEnumerable<string> DocumentFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".achx" or ".achj" or ".tsx" or ".seqj");

    /// <summary>The names of the files in <paramref name="before"/> that are gone or hold other bytes now.</summary>
    public static List<string> Changed(Dictionary<string, byte[]> before) =>
        before.Where(file => !File.Exists(file.Key) || !File.ReadAllBytes(file.Key).AsSpan().SequenceEqual(file.Value))
            .Select(file => Path.GetFileName(file.Key)).ToList();

    /// <summary>Fails naming the facts <paramref name="content"/> lacks.</summary>
    public static void ShouldHold(this IEnumerable<string> content, IEnumerable<string> facts, string? message = null) =>
        facts.Except(content).ShouldBeEmpty(message);
}
