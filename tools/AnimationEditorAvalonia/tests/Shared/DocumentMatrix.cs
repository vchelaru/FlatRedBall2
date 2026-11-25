using AnimationEditor.Core;
using AnimationEditor.Core.Paths;
using FlatRedBall2.Animation;
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
/// Every action that writes or moves a document's content, run for the (front, background) pairs of <see cref="DocType"/>:
/// up to <see cref="ConvertToAchj"/> in <c>DocumentWriteMatrixTests</c> (Core), the rest, which need the window, in
/// <c>DocumentWriteMatrixScenarioTests</c>. A value without rows fails there.
/// </summary>
public enum DocAction
{
    Save,
    Autosave,
    SaveAs,
    SaveAsToAnotherFolder,
    SaveAsOntoAnotherFormat,
    FailedSaveAs,
    SaveBackgroundDocument,
    Reload,
    SwitchTabs,
    UndoAfterSwitch,
    OutsideChangeOverUnsavedEdits,
    WriteRecovery,
    Export,
    ConvertToAchj,

    CutChain,
    CutChainATilesetCannotStore,
    CutFrame,
    CutShape,
    CutSequence,
    SaveAsOntoOpenTab,
    CloseTabWithUnsavedEdits,
    CloseBehindOutsideChange,
    CloseWindowWithUnsavedEdits,
    Detach,
    LeaveDuringRename,
    ConvertOpenTabToAchj,
    DuplicateWithUnsavedEdits,
    RecoverAtStartup,
}

/// <summary>
/// What one document is expected to hold, as a model of its own that the test edits in step with the action and then
/// compares, whole, with the document's file and with what its tab holds.
/// </summary>
public sealed class Expected
{
    private Expected(AnimationChainListSave? list, SequenceListSave? sequences)
    {
        List = list!;
        Sequences = sequences!;
    }

    public AnimationChainListSave List { get; }

    public SequenceListSave Sequences { get; }

    public List<AnimationChainSave> Chains => List.AnimationChains;

    public static Expected Of(AnimationChainListSave list) =>
        new(AnimationChainListSave.FromJsonString(list.ToJsonString()), null);

    public static Expected Of(SequenceListSave sequences) =>
        new(null, SequenceListSave.FromJsonString(sequences.ToJsonString()));

    /// <summary>
    /// The fixture <c>Documents.Write</c> writes for <paramref name="type"/>, not what reading it gives. It names other
    /// files by full path, so it stays the same document when that moves.
    /// </summary>
    public static Expected Fixture(string folder, DocType type, string name, AnimationChainSave chain)
    {
        if (type == DocType.Seqj) return Of(Documents.Resolved(Documents.Sequences(name), folder));
        var expected = Of(Documents.List(name, chain));
        foreach (AnimationFrameSave frame in expected.Chains.SelectMany(listed => listed.Frames)) frame.TextureName = Path.Combine(folder, frame.TextureName);
        return expected;
    }

    /// <summary>What <c>AddFrameFromPixelBounds</c> adds at <c>Documents.NewCell</c> to the first chain.</summary>
    public void AddFrame(string name)
    {
        var (left, top) = Documents.NewCell(name);
        Chains[0].Frames.Add(new AnimationFrameSave
        {
            TextureName = Chains[0].Frames[0].TextureName,
            FrameLength = 0.1f,
            LeftCoordinate = left,
            RightCoordinate = left + 16,
            TopCoordinate = top,
            BottomCoordinate = top + 16,
        });
    }

    /// <summary>The document as text, one line per chain or sequence, in order. <paramref name="folder"/> is the file's.</summary>
    public List<string> Lines(string folder) => List is null ? Documents.Lines(Sequences, folder) : Documents.Lines(List, folder);
}

/// <summary>The documents the matrix opens, and how it reads them back.</summary>
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

    /// <summary>Only what is missing: the editor may be reading a texture that is already there.</summary>
    public static void WriteTextures(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (string path in new[] { "sheet.png", "other.png" }.Select(name => Path.Combine(folder, name)))
            if (!File.Exists(path)) File.WriteAllBytes(path, Convert.FromBase64String(SheetPng));
    }

    /// <summary>The row of tiles of the document's first chain, the next one being its second's, so two open documents never hold the same cell.</summary>
    private static int RowOf(string name) => name switch { FrontName => 0, BackgroundName => 2, _ => 1 };

    /// <summary>The top left pixel of a frame no document starts with, for an edit.</summary>
    public static (int Left, int Top) NewCell(string name) => (32, RowOf(name) * 16);

    /// <summary>Two frames on the document's own tiles, with only what a .tsx stores.</summary>
    public static AnimationChainSave PlainChain(string name)
    {
        int top = RowOf(name) * 16;
        var chain = new AnimationChainSave { Name = "Walk-" + name };
        foreach (int left in new[] { 0, 16 })
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName = "sheet.png",
                FrameLength = left == 0 ? 0.1f : 0.25f,
                LeftCoordinate = left,
                RightCoordinate = left + 16,
                TopCoordinate = top,
                BottomCoordinate = top + 16,
            });
        return chain;
    }

    /// <summary>A document of <paramref name="chain"/> and a second chain, so order is always compared.</summary>
    public static AnimationChainListSave List(string name, AnimationChainSave chain)
    {
        int top = (RowOf(name) + 1) * 16;
        var idle = new AnimationChainSave { Name = "Idle-" + name };
        idle.Frames.Add(new AnimationFrameSave
        {
            TextureName = "sheet.png",
            FrameLength = 0.2f,
            LeftCoordinate = 0,
            RightCoordinate = 16,
            TopCoordinate = top,
            BottomCoordinate = top + 16,
        });
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.AddRange([chain, idle]);
        return list;
    }

    /// <summary><see cref="PlainChain"/> with a value in every kind of member a chain, frame, event and shape has.</summary>
    public static AnimationChainSave RichChain(string name, string texture = "sheet.png")
    {
        AnimationChainSave chain = PlainChain(name);
        chain.Loop = false;
        chain.PingPong = true;
        chain.Reverse = true;
        foreach (AnimationFrameSave frame in chain.Frames) frame.TextureName = texture;
        AnimationFrameSave first = chain.Frames[0], second = chain.Frames[1];
        first.FlipHorizontal = true;
        (first.RelativeX, first.RelativeY) = (3, -2);
        (first.Red, first.Green, first.Blue, first.Alpha, first.ColorOperation) = (10, 20, 30, 200, ColorOperation.Add);
        (first.OffsetInterpolation, first.OffsetEasing) = ("Cubic", "In");
        (first.ColorInterpolation, first.ColorEasing) = ("Sine", "InOut");
        first.Events.Add(new AnimationFrameEvent { Name = "Step", Data = "left" });
        first.ShapesSave = new ShapesSave();
        first.ShapesSave.Add(new AARectSave { Name = "Hit-" + name, X = 1, Y = 2, ScaleX = 4, ScaleY = 5, Red = 0.5f });
        first.ShapesSave.Add(new CircleSave { Name = "Reach-" + name, X = -1, Y = 6, Radius = 3 });
        second.FlipVertical = true;
        second.FlipDiagonal = true;
        second.SkipOffsetKeyframe = true;
        second.SkipColorKeyframe = true;
        second.ShapesSave = new ShapesSave();
        second.ShapesSave.Add(new PolygonSave
        {
            Name = "Edge-" + name,
            Points = { new Vector2Save { X = 0, Y = 0 }, new Vector2Save { X = 4, Y = 0 }, new Vector2Save { X = 0, Y = 4 } },
        });
        return chain;
    }

    /// <summary>The chain a document of <paramref name="type"/> starts with.</summary>
    public static AnimationChainSave ChainFor(DocType type, string name) => type == DocType.Tsx ? PlainChain(name) : RichChain(name);

    public static SequenceListSave Sequences(string name)
    {
        var body = new SequenceTrackSave { Name = "Body" };
        body.Chains.Add(new SequenceChainReferenceSave { Source = "clips.achx", Chain = "Walk-clips", Start = 0.5, Length = 1 });
        var sequence = new SequenceSave { Name = name, Loop = false };
        sequence.Tracks.Add(body);
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Arms", Visible = false, Continuous = true });
        return new SequenceListSave { Sources = { "clips.achx" }, Sequences = { sequence, new SequenceSave { Name = "Idle-" + name } } };
    }

    /// <summary>Writes the document <paramref name="name"/> of <paramref name="type"/> into <paramref name="folder"/> and returns its path.</summary>
    public static string Write(string folder, DocType type, string name, AnimationChainSave? chain = null)
    {
        WriteTextures(folder);
        string path = Path.Combine(folder, name + type.Extension());
        AnimationChainListSave list = List(name, chain ?? ChainFor(type, name));
        switch (type)
        {
            case DocType.Achx:
                list.Save(path);
                break;
            case DocType.Achj:
                list.SaveJson(path);
                break;
            case DocType.Tsx:
                int tile = RowOf(name) * 4;
                File.WriteAllText(path, $"""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <tileset version="1.10" tiledversion="1.12.2" name="{name}" tilewidth="16" tileheight="16" tilecount="16" columns="4">
                     <image source="sheet.png" width="64" height="64"/>
                     <tile id="{OwnerTile(name)}">
                      <properties>
                       <property name="Name" value="Walk-{name}"/>
                      </properties>
                      <animation>
                       <frame tileid="{tile}" duration="100"/>
                       <frame tileid="{tile + 1}" duration="250"/>
                      </animation>
                     </tile>
                     <tile id="{tile + 4}">
                      <properties>
                       <property name="Name" value="Idle-{name}"/>
                      </properties>
                      <animation>
                       <frame tileid="{tile + 4}" duration="200"/>
                      </animation>
                     </tile>
                    </tileset>
                    """);
                break;
            case DocType.Seqj:
                if (!File.Exists(Path.Combine(folder, "clips.achx"))) Write(folder, DocType.Achx, "clips");
                Sequences(name).Save(path);
                break;
            default:
                throw new InvalidOperationException($"The matrix has no fixture for {type}.");
        }
        return path;
    }

    // Not the chain's own first frame, so it survives only through the tab's own tile ids.
    private static int OwnerTile(string name) => RowOf(name) * 4 + 3;

    /// <summary>Reads <paramref name="path"/> strictly as what its extension says.</summary>
    public static Expected Read(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".achx":
                RootOf(path).Name.LocalName.ShouldBe("AnimationChainArraySave", $"{path} is not an .achx");
                return Expected.Of(AnimationChainListSave.FromFile(path));
            case ".achj":
                JsonKeysOf(path).ShouldContain("animationchains", $"{path} is not an .achj");
                return Expected.Of(AnimationChainListSave.FromFile(path));
            case ".seqj":
                JsonKeysOf(path).ShouldContain("sequences", $"{path} is not a .seqj");
                return Expected.Of(SequenceListSave.FromFile(path));
            case ".tsx":
                RootOf(path).Name.LocalName.ShouldBe("tileset", $"{path} is not a .tsx");
                var project = new ProjectManager();
                project.LoadTsxProject(new FilePath(path));
                return Expected.Of(project.AnimationChainListSave!);
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

    /// <summary>Fails unless the file at <paramref name="path"/> is in its own format and holds exactly <paramref name="expected"/>.</summary>
    public static void ShouldHold(string path, Expected expected)
    {
        string folder = Path.GetDirectoryName(path)!;
        Read(path).Lines(folder).ShouldBe(expected.Lines(folder), $"{Path.GetFileName(path)} on disk");
        // The tile that stores an animation is not part of the chain.
        string name = Path.GetFileNameWithoutExtension(path);
        if (path.EndsWith(".tsx") && expected.Chains.Any(chain => chain.Name == "Walk-" + name))
            TileAnimations(path).ShouldContain(tile => tile.StartsWith(OwnerTile(name) + ":"), "the animation moved to another tile");
    }

    /// <summary>"tile: tile@milliseconds ..." of each animated tile of a .tsx.</summary>
    public static List<string> TileAnimations(string path) => RootOf(path).Elements("tile").Where(tile => tile.Element("animation") != null)
        .Select(tile => $"{(int)tile.Attribute("id")!}: " + string.Join(" ", tile.Element("animation")!.Elements("frame")
            .Select(frame => $"{(int)frame.Attribute("tileid")!}@{(int)frame.Attribute("duration")!}"))).ToList();

    /// <summary>Every member of each chain, as JSON. A texture is named by its full path, since a document stores it relative to itself.</summary>
    public static List<string> Lines(AnimationChainListSave list, string folder) => list.AnimationChains.Select(chain =>
    {
        var alone = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel, TimeMeasurementUnit = list.TimeMeasurementUnit };
        alone.AnimationChains.Add(chain);
        alone = AnimationChainListSave.FromJsonString(alone.ToJsonString());
        if (alone.TimeMeasurementUnit == TimeMeasurementUnit.Undefined) alone.TimeMeasurementUnit = TimeMeasurementUnit.Second;
        foreach (AnimationFrameSave frame in alone.AnimationChains[0].Frames)
        {
            frame.TextureName = new FilePath(Path.Combine(folder, frame.TextureName)).FullPath;
            // An empty list and none are the same shapes.
            if (frame.ShapesSave?.Shapes.Count == 0) frame.ShapesSave = null;
        }
        return alone.ToJsonString();
    }).ToList();

    /// <summary>Every member of each sequence and the sources it can use, as JSON, with a source named by its full path.</summary>
    public static List<string> Lines(SequenceListSave list, string folder)
    {
        SequenceListSave resolved = Resolved(list, folder);
        return resolved.Sequences.Select(sequence => new SequenceListSave { Sources = resolved.Sources, Sequences = { sequence } }.ToJsonString()).ToList();
    }

    /// <summary>A copy of <paramref name="list"/>, in <paramref name="folder"/>, that names its sources by full path.</summary>
    public static SequenceListSave Resolved(SequenceListSave list, string folder)
    {
        SequenceListSave copy = SequenceListSave.FromJsonString(list.ToJsonString());
        string FullPath(string source) => new FilePath(Path.Combine(folder, source)).FullPath;
        copy.Sources = copy.Sources.Select(FullPath).ToList();
        foreach (var reference in copy.Sequences.SelectMany(sequence => sequence.Tracks).SelectMany(track => track.Chains)) reference.Source = FullPath(reference.Source);
        return copy;
    }

    /// <summary>The textures and sources the file at <paramref name="path"/> names that are not there.</summary>
    public static List<string> MissingReferences(string path)
    {
        Expected document = Read(path);
        IEnumerable<string> named = document.List is null ? document.Sequences.Sources
            : document.Chains.SelectMany(chain => chain.Frames).Select(frame => frame.TextureName);
        return named.Select(name => new FilePath(Path.Combine(Path.GetDirectoryName(path)!, name)).FullPath).Distinct().Where(file => !File.Exists(file)).ToList();
    }

    /// <summary>Every document file under <paramref name="folder"/> with its bytes.</summary>
    public static Dictionary<string, byte[]> FileBytes(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path).ToLowerInvariant() is ".achx" or ".achj" or ".tsx" or ".seqj")
            .ToDictionary(path => path, File.ReadAllBytes);

    /// <summary>The names of the files that are new, gone or hold other bytes than in <paramref name="before"/>.</summary>
    public static List<string> Changed(Dictionary<string, byte[]> before, string folder)
    {
        var now = FileBytes(folder);
        return before.Keys.Union(now.Keys)
            .Where(path => !before.TryGetValue(path, out var old) || !now.TryGetValue(path, out var bytes) || !bytes.AsSpan().SequenceEqual(old))
            .Select(path => Path.GetFileName(path)).Order().ToList();
    }

    /// <summary>Makes a save of <paramref name="path"/> fail. On Linux that is its whole folder.</summary>
    public static void SetWritable(string path, bool writable)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(path, writable ? FileAttributes.Normal : FileAttributes.ReadOnly);
            return;
        }
        const UnixFileMode readOnly = UnixFileMode.UserRead | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
        File.SetUnixFileMode(Path.GetDirectoryName(path)!, writable ? readOnly | UnixFileMode.UserWrite : readOnly);
    }
}
