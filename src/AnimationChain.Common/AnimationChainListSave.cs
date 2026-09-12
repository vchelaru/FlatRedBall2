using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using FlatRedBall2.Animation;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Undefined is treated identically to Second and exists for compatibility with .achx files
/// produced by older FlatRedBall tooling.
/// </summary>
public enum TimeMeasurementUnit
{
    /// <summary>Undefined.</summary>
    Undefined,
    /// <summary>Seconds.</summary>
    Second,
    /// <summary>Milliseconds.</summary>
    Millisecond
}
/// <summary>
/// Defines how texture coordinates are interpreted (normalized 0-1 or raw pixels).
/// </summary>
public enum TextureCoordinateType
{
    /// <summary>Coordinates are normalized (0 to 1).</summary>
    UV,
    /// <summary>Coordinates are raw pixel values.</summary>
    Pixel
}

/// <summary>
/// Deserialized representation of a .achx (XML) or .achj (JSON) animation file — the two
/// on-disk dialects of the same data model. Load with <see cref="FromFile(string)"/> /
/// <see cref="FromJsonFile(string)"/> and convert to runtime types with
/// <c>ToAnimationChainList</c> (an extension method in the <c>FlatRedBall.AnimationChain.MonoGame</c>
/// package — this type itself has no MonoGame dependency so tooling, like the
/// Animation Editor, can reference it without pulling MonoGame in at all).
/// </summary>
public class AnimationChainListSave
{
    /// <summary>
    /// Whether texture file paths stored in frames are relative to the .achx file location.
    /// Set to <c>true</c> (the default in the .achx format) so the file is portable.
    /// </summary>
    public bool FileRelativeTextures = true;

    /// <summary>The unit of time used by frames in this list.</summary>
    public TimeMeasurementUnit TimeMeasurementUnit = TimeMeasurementUnit.Second;
    /// <summary>How texture coordinates in frames are specified.</summary>
    public TextureCoordinateType CoordinateType = TextureCoordinateType.UV;

    /// <summary>The list of animation chains.</summary>
    public List<AnimationChainSave> AnimationChains = new();

    /// <summary>Absolute path of the .achx file. Set automatically by <see cref="FromFile(string)"/>;
    /// tooling (Animation Editor) sets this directly when the user picks a Save-As path.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Path of the project (.gluj) file the .achx belongs to, relative to the .achx location.
    /// Persisted in the XML and round-tripped by tooling but ignored at runtime by the engine.
    /// </summary>
    public string? ProjectFile { get; set; }

    /// <summary>
    /// Loads a .achx file directly from disk via <see cref="File.OpenRead(string)"/>. Intended
    /// for tooling (file pickers in the Animation Editor) where the caller has an absolute path
    /// and needs to bypass the <c>ContentLoader</c> stream seam. Production runtime code
    /// should call <c>ContentLoader.LoadAnimationChainList</c> or pass a <c>streamProvider</c>
    /// to the other overload.
    /// </summary>
    public static AnimationChainListSave FromFile(string path)
        => FromFile(path, File.OpenRead!);

    /// <summary>
    /// Loads a .achx (XML) or .achj (JSON) file — dialect chosen by <paramref name="filePath"/>'s
    /// extension, so callers don't need their own <c>.achj</c>-vs-<c>.achx</c> branch (#887).
    /// Production code should prefer <c>ContentLoader.LoadAnimationChainList(path)</c>, which
    /// routes the read through the service's stream seam (TitleContainer on DesktopGL, HTTP
    /// fetch on Blazor). This overload exists for tooling and tests that work without a
    /// <c>ContentLoader</c>.
    /// </summary>
    /// <param name="filePath">Path to the file, interpreted by <paramref name="streamProvider"/>.
    /// A <c>.achj</c> extension (case-insensitive) selects the JSON parser; anything else parses as XML.</param>
    /// <param name="streamProvider">Byte source. Callers must supply one — there is no default — so this
    /// method has no IL-level reference to <c>TitleContainer</c> and tools that don't ship MonoGame.Framework
    /// (e.g. AnimationEditor on Avalonia) can call it without triggering an assembly load.</param>
    public static AnimationChainListSave FromFile(string filePath, Func<string, Stream> streamProvider)
    {
        using var stream = streamProvider(filePath);
        var result = IsJsonPath(filePath)
            ? ParseJson(JsonNode.Parse(stream)!.AsObject())
            : ParseXml(XDocument.Load(stream));
        result.FileName = filePath;
        return result;
    }

    /// <summary>True when <paramref name="path"/> has a .achj (JSON) extension; false for .achx or anything else.</summary>
    private static bool IsJsonPath(string path) =>
        path.EndsWith(".achj", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a .achx (XML) or .achj (JSON) stream — dialect chosen by content-sniffing the first
    /// non-whitespace character (<c>{</c> for JSON, anything else for XML), for callers with text
    /// but no path to branch on (e.g. clipboard payloads). Same trick as
    /// <c>FlatRedBall2.AnimationChain.MonoGame</c>'s <c>FromDetectedStream</c>/<c>LooksLikeJson</c>.
    /// <see cref="FileName"/> is set to <see cref="string.Empty"/>; if <see cref="FileRelativeTextures"/>
    /// is <c>true</c>, texture paths in the file are passed through as-is with no directory prefix
    /// prepended. The caller retains ownership of <paramref name="stream"/> and is responsible for disposing it.
    /// </summary>
    public static AnimationChainListSave FromStream(Stream stream)
    {
        string text;
        using (var reader = new StreamReader(stream, leaveOpen: true))
            text = reader.ReadToEnd();
        return FromString(text);
    }

    /// <summary>
    /// Parses a .achx (XML) or .achj (JSON) in-memory string — dialect chosen by content-sniffing,
    /// same as <see cref="FromStream(Stream)"/>. <see cref="FileName"/> is set to
    /// <see cref="string.Empty"/>; if <see cref="FileRelativeTextures"/> is <c>true</c>, texture
    /// paths in the file are passed through as-is with no directory prefix prepended.
    /// </summary>
    public static AnimationChainListSave FromString(string text)
        => LooksLikeJson(text) ? FromJsonString(text) : FromXmlString(text);

    private static AnimationChainListSave FromXmlString(string xml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return ParseXml(XDocument.Load(stream));
    }

    // A leading UTF-8 BOM (0xEF 0xBB 0xBF) decodes to the single char U+FEFF. StreamReader/
    // File.ReadAllText strip it automatically, but content already decoded via
    // Encoding.UTF8.GetString on raw bytes keeps it — and char.IsWhiteSpace matches U+FEFF as false,
    // so a plain TrimStart() leaves it in place and hides the '{'/'<' this check looks for.
    // Strip it explicitly so BOM-prefixed text still sniffs correctly.
    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.AsSpan().TrimStart('\uFEFF').TrimStart();
        return trimmed.Length > 0 && trimmed[0] == '{';
    }

    /// <summary>
    /// Loads a .achj file directly from disk via <see cref="File.OpenRead(string)"/>. JSON
    /// counterpart of <see cref="FromFile(string)"/>.
    /// </summary>
    public static AnimationChainListSave FromJsonFile(string path)
        => FromJsonFile(path, File.OpenRead!);

    /// <summary>
    /// Loads a .achj file via manual JSON parsing (AOT-safe). JSON counterpart of
    /// <see cref="FromFile(string, Func{string, Stream})"/> — see that overload for why
    /// <paramref name="streamProvider"/> has no default.
    /// </summary>
    public static AnimationChainListSave FromJsonFile(string filePath, Func<string, Stream> streamProvider)
    {
        using var stream = streamProvider(filePath);
        var result = ParseJson(JsonNode.Parse(stream)!.AsObject());
        result.FileName = filePath;
        return result;
    }

    /// <summary>Parses .achj JSON from an already-open <paramref name="stream"/>, same contract as <see cref="FromStream(Stream)"/>.</summary>
    public static AnimationChainListSave FromJsonStream(Stream stream)
        => ParseJson(JsonNode.Parse(stream)!.AsObject());

    /// <summary>Parses .achj JSON from an in-memory string, same contract as <see cref="FromString(string)"/>.</summary>
    public static AnimationChainListSave FromJsonString(string json)
        => ParseJson(JsonNode.Parse(StripLeadingBom(json))!.AsObject());

    // Unlike JsonNode.Parse(Stream) (which treats a UTF-8 BOM as transport-level framing and
    // skips it automatically), JsonNode.Parse(string) re-encodes the string to UTF-8 bytes and
    // feeds them to Utf8JsonReader, which rejects a leading BOM byte sequence as invalid JSON.
    // A leading U+FEFF char reaches this method whenever the caller decoded raw bytes without
    // going through StreamReader/File.ReadAllText (both of which already strip it) — e.g.
    // Encoding.UTF8.GetString, or FromString's content-sniffing dispatch. Strip it before parsing.
    private static string StripLeadingBom(string text) =>
        text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;

    private static AnimationChainListSave ParseXml(XDocument doc)
    {
        var root = doc.Root!;
        var result = new AnimationChainListSave();

        var frt = root.Element("FileRelativeTextures");
        if (frt != null)
            result.FileRelativeTextures = bool.Parse(frt.Value);

        var tmu = root.Element("TimeMeasurementUnit");
        if (tmu != null)
            result.TimeMeasurementUnit = Enum.Parse<TimeMeasurementUnit>(tmu.Value);

        var ct = root.Element("CoordinateType");
        if (ct != null)
            result.CoordinateType = Enum.Parse<TextureCoordinateType>(ct.Value);

        foreach (var chainEl in root.Elements("AnimationChain"))
        {
            var chain = new AnimationChainSave
            {
                Name = (string?)chainEl.Element("Name") ?? string.Empty,
                IsLocked = BoolEl(chainEl, "Locked"),
            };

            foreach (var frameEl in chainEl.Elements("Frame"))
                chain.Frames.Add(ParseFrame(frameEl));

            result.AnimationChains.Add(chain);
        }

        var projectFileEl = root.Element("ProjectFile");
        if (projectFileEl != null) result.ProjectFile = projectFileEl.Value;

        return result;
    }

    /// <summary>
    /// Writes this save to a .achx file using FRB1's XML element dialect — root
    /// <c>&lt;AnimationChainArraySave&gt;</c>, shape wrapper <c>&lt;ShapeCollectionSave&gt;</c>,
    /// rectangle list <c>&lt;AxisAlignedRectangleSaves&gt;</c>/<c>&lt;AxisAlignedRectangleSave&gt;</c>.
    /// Element names diverge from the C# type names (which stay terse, e.g. <see cref="AARectSave"/>)
    /// so existing .achx files in the wild keep their byte shape.
    /// </summary>
    /// <remarks>
    /// Output is byte-faithful to FRB1: no UTF-8 BOM, CRLF newlines, floats written as the shortest
    /// round-trippable decimal (the "R" format, matching FRB1's XmlConvert output), shapes in
    /// FRB1's typed lists, and per-shape Z/Alpha/Red/Green/Blue preserved verbatim (see
    /// <see cref="Frb1ShapeData"/>). Opening and re-saving an unedited file is a no-op diff.
    /// Frame defaults are omitted to keep diffs small: <c>FlipHorizontal</c>/<c>FlipVertical</c>/
    /// <c>FlipDiagonal</c> are written only when <c>true</c>; <c>RelativeX</c>/<c>RelativeY</c> only when non-zero.
    /// The <c>&lt;ShapeCollectionSave&gt;</c> wrapper is written only when a frame's
    /// <see cref="AnimationFrameSave.ShapesSave"/> is non-null, mirroring whether the source frame
    /// had one (some FRB1 files omit it for shapeless frames, others write an empty one).
    /// AxisAlignedCubeSaves and SphereSaves are FRB1 3D placeholders FRB2 does not model — always
    /// emitted empty for dialect parity.
    /// </remarks>
    public void Save(string path)
    {
        using var stream = File.Create(path);
        Save(stream);
    }

    /// <summary>
    /// Writes this save using the same FRB1-faithful dialect as <see cref="Save(string)"/>, to an
    /// already-open stream instead of a local path. For callers with no real filesystem to write
    /// to (e.g. the browser build writing to an <c>IStorageFile</c>'s stream) -- the caller owns
    /// <paramref name="stream"/> and is responsible for disposing it; this method does not close it.
    /// </summary>
    public void Save(Stream stream)
    {
        // FRB1 writes UTF-8 without a BOM; XDocument.Save(stream) would emit one, diffing every
        // file. Use an explicit BOM-less encoding. Indent/newlines match FRB1 (2 spaces, CRLF).
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "  ",
            // FRB1 .achx is CRLF. XmlWriter's default newline is platform-dependent (LF on
            // Linux/CI), which would diff every line off-Windows — pin it so output is byte-stable.
            NewLineChars = "\r\n",
        };
        using var writer = XmlWriter.Create(stream, settings);
        ToXDocument().Save(writer);
    }

    /// <summary>
    /// Async-safe counterpart to <see cref="Save(Stream)"/> for destination streams that only
    /// support async writes -- e.g. Avalonia's browser <c>IStorageFile.OpenWriteAsync()</c> stream,
    /// backed by the File System Access API. <see cref="XmlWriter"/> writes synchronously, so
    /// handing it such a stream directly throws from inside <c>XmlWriter.Dispose()</c>; on WASM
    /// that takes down the whole runtime instead of raising a catchable exception. This method
    /// serializes to an in-memory buffer first (where synchronous writes are always safe), then
    /// copies the result to <paramref name="stream"/> asynchronously.
    /// </summary>
    public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        Save(buffer);
        // MemoryStream.CopyToAsync(stream) would defeat the point of this method: it has a
        // synchronous fast path that calls stream.Write(...) directly since the source is
        // already fully in memory. Call stream.WriteAsync explicitly so an async-only
        // destination (e.g. the browser's WriteableStream) is never handed a sync write.
        await stream.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes this save to a .achx XML string using the same dialect as <see cref="Save(string)"/>.
    /// Lets the Animation Editor clipboard share one serializer with file I/O, so copy/paste of
    /// frames and chains round-trips per-frame shapes exactly as the on-disk format does. Pair
    /// with <see cref="FromString"/> to read it back.
    /// </summary>
    public string ToXmlString() => ToXDocument().ToString();

    /// <summary>
    /// Writes this save as a .achj file. JSON counterpart of <see cref="Save(string)"/>. Unlike
    /// the XML dialect, .achj has no legacy byte-format to match, so property names are camelCase
    /// and optional/default fields are omitted the same way the XML writer omits them (small diffs).
    /// </summary>
    public void SaveJson(string path)
    {
        using var stream = File.Create(path);
        SaveJson(stream);
    }

    /// <summary>
    /// Writes this save as indented .achj JSON to an already-open stream. JSON counterpart of
    /// <see cref="Save(Stream)"/> — the caller owns <paramref name="stream"/> and is responsible
    /// for disposing it; this method does not close it.
    /// </summary>
    public void SaveJson(Stream stream)
    {
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        ToJsonNode().WriteTo(writer);
    }

    /// <summary>Async-safe counterpart to <see cref="SaveJson(Stream)"/>, same contract as <see cref="SaveAsync(Stream, CancellationToken)"/>.</summary>
    public async Task SaveJsonAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        SaveJson(buffer);
        await stream.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Serializes this save to a .achj JSON string. JSON counterpart of <see cref="ToXmlString"/>.</summary>
    public string ToJsonString() => ToJsonNode().ToJsonString(new JsonSerializerOptions { WriteIndented = true });

    private JsonObject ToJsonNode()
    {
        var root = new JsonObject
        {
            ["fileRelativeTextures"] = FileRelativeTextures,
            ["timeMeasurementUnit"] = TimeMeasurementUnit.ToString(),
            ["coordinateType"] = CoordinateType.ToString(),
        };

        var chainsArray = new JsonArray();
        foreach (var chain in AnimationChains)
        {
            var framesArray = new JsonArray();
            foreach (var frame in chain.Frames)
                framesArray.Add((JsonNode)WriteFrameJson(frame));

            var chainObj = new JsonObject
            {
                ["name"] = chain.Name,
                ["frames"] = framesArray,
            };
            if (chain.IsLocked)
                chainObj["locked"] = true;
            chainsArray.Add((JsonNode)chainObj);
        }
        root["animationChains"] = chainsArray;

        if (ProjectFile != null)
            root["projectFile"] = ProjectFile;

        return root;
    }

    private static JsonObject WriteFrameJson(AnimationFrameSave frame)
    {
        var obj = new JsonObject
        {
            ["textureName"] = frame.TextureName,
            ["frameLength"] = frame.FrameLength,
            ["leftCoordinate"] = frame.LeftCoordinate,
            ["rightCoordinate"] = frame.RightCoordinate,
            ["topCoordinate"] = frame.TopCoordinate,
            ["bottomCoordinate"] = frame.BottomCoordinate,
        };
        if (frame.FlipHorizontal) obj["flipHorizontal"] = true;
        if (frame.FlipVertical) obj["flipVertical"] = true;
        if (frame.FlipDiagonal) obj["flipDiagonal"] = true;
        if (frame.RelativeX != 0f) obj["relativeX"] = frame.RelativeX;
        if (frame.RelativeY != 0f) obj["relativeY"] = frame.RelativeY;
        if (frame.Red.HasValue) obj["red"] = frame.Red.Value;
        if (frame.Green.HasValue) obj["green"] = frame.Green.Value;
        if (frame.Blue.HasValue) obj["blue"] = frame.Blue.Value;
        if (frame.Alpha.HasValue) obj["alpha"] = frame.Alpha.Value;
        if (frame.ColorOperation.HasValue) obj["colorOperation"] = frame.ColorOperation.Value.ToString();
        if (frame.ShapesSave is { } shapes) obj["shapes"] = WriteShapesJson(shapes);
        return obj;
    }

    private static JsonObject WriteShapesJson(ShapesSave shapes)
    {
        var rectsArray = new JsonArray();
        foreach (var r in shapes.AARectSaves)
            rectsArray.Add((JsonNode)new JsonObject
            {
                ["name"] = r.Name, ["x"] = r.X, ["y"] = r.Y, ["scaleX"] = r.ScaleX, ["scaleY"] = r.ScaleY,
                ["z"] = r.Z, ["alpha"] = r.Alpha, ["red"] = r.Red, ["green"] = r.Green, ["blue"] = r.Blue,
            });

        var circlesArray = new JsonArray();
        foreach (var c in shapes.CircleSaves)
            circlesArray.Add((JsonNode)new JsonObject
            {
                ["name"] = c.Name, ["x"] = c.X, ["y"] = c.Y, ["radius"] = c.Radius,
                ["z"] = c.Z, ["alpha"] = c.Alpha, ["red"] = c.Red, ["green"] = c.Green, ["blue"] = c.Blue,
            });

        var polysArray = new JsonArray();
        foreach (var p in shapes.PolygonSaves)
        {
            var pointsArray = new JsonArray();
            foreach (var v in p.Points)
                pointsArray.Add((JsonNode)new JsonObject { ["x"] = v.X, ["y"] = v.Y });

            polysArray.Add((JsonNode)new JsonObject
            {
                ["name"] = p.Name, ["x"] = p.X, ["y"] = p.Y,
                ["z"] = p.Z, ["alpha"] = p.Alpha, ["red"] = p.Red, ["green"] = p.Green, ["blue"] = p.Blue,
                ["points"] = pointsArray,
            });
        }

        return new JsonObject
        {
            ["rectangles"] = rectsArray,
            ["circles"] = circlesArray,
            ["polygons"] = polysArray,
        };
    }

    private XDocument ToXDocument()
    {
        var root = new XElement("AnimationChainArraySave",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
            new XElement("FileRelativeTextures", FileRelativeTextures ? "true" : "false"),
            new XElement("TimeMeasurementUnit", TimeMeasurementUnit.ToString()),
            new XElement("CoordinateType", CoordinateType.ToString()));

        foreach (var chain in AnimationChains)
        {
            var chainEl = new XElement("AnimationChain",
                new XElement("Name", chain.Name));
            // Tooling-only, written only when true so an unlocked/legacy chain round-trips
            // byte-identical (same convention as the frame writer's optional fields below).
            if (chain.IsLocked)
                chainEl.Add(new XElement("Locked", "true"));
            foreach (var frame in chain.Frames)
                chainEl.Add(WriteFrame(frame));
            root.Add(chainEl);
        }

        if (ProjectFile != null)
            root.Add(new XElement("ProjectFile", ProjectFile));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static XElement WriteFrame(AnimationFrameSave frame)
    {
        var el = new XElement("Frame");
        // Match FRB1's element order — FlipHorizontal appears before TextureName when set.
        // FlipDiagonal has no FRB1 precedent (new in FRB2); placed after FlipVertical since it's
        // part of the same flip-flag group.
        if (frame.FlipHorizontal) el.Add(new XElement("FlipHorizontal", "true"));
        if (frame.FlipVertical) el.Add(new XElement("FlipVertical", "true"));
        if (frame.FlipDiagonal) el.Add(new XElement("FlipDiagonal", "true"));
        el.Add(new XElement("TextureName", frame.TextureName));
        el.Add(new XElement("FrameLength", FloatStr(frame.FrameLength)));
        el.Add(new XElement("LeftCoordinate", FloatStr(frame.LeftCoordinate)));
        el.Add(new XElement("RightCoordinate", FloatStr(frame.RightCoordinate)));
        el.Add(new XElement("TopCoordinate", FloatStr(frame.TopCoordinate)));
        el.Add(new XElement("BottomCoordinate", FloatStr(frame.BottomCoordinate)));
        if (frame.RelativeX != 0f) el.Add(new XElement("RelativeX", FloatStr(frame.RelativeX)));
        if (frame.RelativeY != 0f) el.Add(new XElement("RelativeY", FloatStr(frame.RelativeY)));

        // Per-frame color/alpha channels are game-consumed and optional: write each only when set
        // so frames without color stay byte-identical.
        if (frame.Red.HasValue) el.Add(new XElement("Red", frame.Red.Value));
        if (frame.Green.HasValue) el.Add(new XElement("Green", frame.Green.Value));
        if (frame.Blue.HasValue) el.Add(new XElement("Blue", frame.Blue.Value));
        if (frame.Alpha.HasValue) el.Add(new XElement("Alpha", frame.Alpha.Value));
        if (frame.ColorOperation.HasValue) el.Add(new XElement("ColorOperation", frame.ColorOperation.Value.ToString()));

        // Preserve wrapper presence so every FRB1 file round-trips byte-identical: the loader sets
        // ShapesSave non-null iff the frame had a <ShapeCollectionSave> element. Some FRB1 files
        // omit it for shapeless frames (-> null here), others write an empty one (-> non-null,
        // zero shapes). Mirror whichever the source used instead of injecting or dropping it.
        if (frame.ShapesSave is { } shapes)
            el.Add(WriteShapes(shapes));
        return el;
    }

    private static XElement WriteShapes(ShapesSave shapes)
    {
        // FRB1 groups shapes into typed lists (not one ordered list), in this exact element
        // order: rectangles, cubes, polygons, circles, spheres. Cubes and spheres are FRB1-era
        // 3D placeholders FRB2 does not model — emitted as empty elements for dialect parity.
        var rectsEl = new XElement("AxisAlignedRectangleSaves");
        foreach (var r in shapes.AARectSaves)
            rectsEl.Add(new XElement("AxisAlignedRectangleSave",
                new XElement("X", FloatStr(r.X)),
                new XElement("Y", FloatStr(r.Y)),
                new XElement("Z", FloatStr(r.Z)),
                new XElement("ScaleX", FloatStr(r.ScaleX)),
                new XElement("ScaleY", FloatStr(r.ScaleY)),
                new XElement("Name", r.Name),
                WriteColor(r)));

        var polysEl = new XElement("PolygonSaves");
        foreach (var p in shapes.PolygonSaves)
        {
            var pointsEl = new XElement("Points");
            foreach (var v in p.Points)
                pointsEl.Add(new XElement("Vector2Save",
                    new XElement("X", FloatStr(v.X)),
                    new XElement("Y", FloatStr(v.Y))));
            polysEl.Add(new XElement("PolygonSave",
                new XElement("Name", p.Name),
                new XElement("X", FloatStr(p.X)),
                new XElement("Y", FloatStr(p.Y)),
                new XElement("Z", FloatStr(p.Z)),
                pointsEl,
                WriteColor(p)));
        }

        var circlesEl = new XElement("CircleSaves");
        foreach (var c in shapes.CircleSaves)
            circlesEl.Add(new XElement("CircleSave",
                new XElement("X", FloatStr(c.X)),
                new XElement("Y", FloatStr(c.Y)),
                new XElement("Z", FloatStr(c.Z)),
                new XElement("Radius", FloatStr(c.Radius)),
                new XElement("Name", c.Name),
                WriteColor(c)));

        return new XElement("ShapeCollectionSave",
            rectsEl,
            new XElement("AxisAlignedCubeSaves"),
            polysEl,
            circlesEl,
            new XElement("SphereSaves"));
    }

    // Alpha/Red/Green/Blue trail each FRB1 shape in this order; preserved verbatim for round-trip.
    private static object[] WriteColor(Frb1ShapeData s) =>
    [
        new XElement("Alpha", FloatStr(s.Alpha)),
        new XElement("Red", FloatStr(s.Red)),
        new XElement("Green", FloatStr(s.Green)),
        new XElement("Blue", FloatStr(s.Blue)),
    ];

    // Reproduces FRB1's float text: the shortest decimal string that round-trips to the same
    // IEEE-754 value. FRB1's XmlSerializer wrote this via .NET Framework's XmlConvert.ToString,
    // and modern .NET's "R" format produces the identical shortest form (e.g. -25.635418,
    // -5.0416665), so re-saving an unedited legacy file is a no-op diff (issue #503). The old
    // G7-then-G9 approach drifted ~45% of coordinates (-5.0416665 -> -5.04166651) because G7
    // rarely round-trips and G9 over-pads to a fixed 9 significant digits.
    private static string FloatStr(float value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private static AnimationFrameSave ParseFrame(XElement el)
    {
        var frame = new AnimationFrameSave();
        frame.TextureName = (string?)el.Element("TextureName") ?? string.Empty;
        frame.FrameLength = FloatEl(el, "FrameLength");
        frame.LeftCoordinate = FloatEl(el, "LeftCoordinate");
        frame.RightCoordinate = FloatEl(el, "RightCoordinate", 1f);
        frame.TopCoordinate = FloatEl(el, "TopCoordinate");
        frame.BottomCoordinate = FloatEl(el, "BottomCoordinate", 1f);
        frame.FlipHorizontal = BoolEl(el, "FlipHorizontal");
        frame.FlipVertical = BoolEl(el, "FlipVertical");
        frame.FlipDiagonal = BoolEl(el, "FlipDiagonal");
        frame.RelativeX = FloatEl(el, "RelativeX");
        frame.RelativeY = FloatEl(el, "RelativeY");
        frame.Red = IntElNullable(el, "Red");
        frame.Green = IntElNullable(el, "Green");
        frame.Blue = IntElNullable(el, "Blue");
        frame.Alpha = IntElNullable(el, "Alpha");
        frame.ColorOperation = ColorOperationEl(el, "ColorOperation");
        // Frame <Name>/<HasCustomName> in legacy .achx are intentionally ignored: a frame's
        // identity is its index, so the editor always shows a positional "Frame N" label.

        // Shape wrapper is always <ShapeCollectionSave>; ParseShapes accepts both this
        // project's typed-list dialect and AnimationChain.MonoGame's nested <Shapes> dialect.
        var shapesEl = el.Element("ShapeCollectionSave");
        if (shapesEl != null)
            frame.ShapesSave = ParseShapes(shapesEl);

        return frame;
    }

    private static ShapesSave ParseShapes(XElement el)
    {
        var shapes = new ShapesSave();

        // Some tooling (AnimationChain.MonoGame) nests shapes one level deeper, as untyped
        // children of a <Shapes> wrapper, instead of this dialect's typed-list containers below.
        // Accept both so a .achx saved by that tooling doesn't silently lose its shapes when
        // read here (#535 follow-up). Never written by this project's own Save/WriteShapes.
        var nestedShapesEl = el.Element("Shapes");
        if (nestedShapesEl != null)
            return ParseNestedShapesDialect(nestedShapesEl);

        // FRB1 dialect: separate typed containers. Read in write order (rects, polygons, circles)
        // so the round-trip is stable; WriteShapes re-groups by type regardless of list order.
        var rectsEl = el.Element("AxisAlignedRectangleSaves");
        if (rectsEl != null)
        {
            foreach (var r in rectsEl.Elements("AxisAlignedRectangleSave"))
            {
                var rect = new AARectSave
                {
                    Name = (string?)r.Element("Name") ?? string.Empty,
                    X = FloatEl(r, "X"),
                    Y = FloatEl(r, "Y"),
                    ScaleX = FloatEl(r, "ScaleX", 16f),
                    ScaleY = FloatEl(r, "ScaleY", 16f),
                };
                ReadColor(r, rect);
                shapes.Shapes.Add(rect);
            }
        }

        var polysEl = el.Element("PolygonSaves");
        if (polysEl != null)
        {
            foreach (var p in polysEl.Elements("PolygonSave"))
            {
                var poly = new PolygonSave
                {
                    Name = (string?)p.Element("Name") ?? string.Empty,
                    X = FloatEl(p, "X"),
                    Y = FloatEl(p, "Y"),
                };
                var pointsEl = p.Element("Points");
                if (pointsEl != null)
                {
                    foreach (var v in pointsEl.Elements("Vector2Save"))
                        poly.Points.Add(new Vector2Save { X = FloatEl(v, "X"), Y = FloatEl(v, "Y") });
                }
                ReadColor(p, poly);
                shapes.Shapes.Add(poly);
            }
        }

        var circlesEl = el.Element("CircleSaves");
        if (circlesEl != null)
        {
            foreach (var c in circlesEl.Elements("CircleSave"))
            {
                var circle = new CircleSave
                {
                    Name = (string?)c.Element("Name") ?? string.Empty,
                    X = FloatEl(c, "X"),
                    Y = FloatEl(c, "Y"),
                    Radius = FloatEl(c, "Radius", 16f),
                };
                ReadColor(c, circle);
                shapes.Shapes.Add(circle);
            }
        }

        return shapes;
    }

    // Nested dialect: untyped shape elements directly under a <Shapes> wrapper (as opposed to
    // this dialect's typed-list containers), used by AnimationChain.MonoGame. Per-shape
    // Z/Alpha/Red/Green/Blue are absent in that dialect's output; ReadColor falls back to FRB1
    // defaults in that case, same as for any legacy file missing those elements.
    private static ShapesSave ParseNestedShapesDialect(XElement nestedShapesEl)
    {
        var shapes = new ShapesSave();

        foreach (var child in nestedShapesEl.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "AxisAlignedRectangleSave":
                    var rect = new AARectSave
                    {
                        Name = (string?)child.Element("Name") ?? string.Empty,
                        X = FloatEl(child, "X"),
                        Y = FloatEl(child, "Y"),
                        ScaleX = FloatEl(child, "ScaleX", 16f),
                        ScaleY = FloatEl(child, "ScaleY", 16f),
                    };
                    ReadColor(child, rect);
                    shapes.Shapes.Add(rect);
                    break;
                case "CircleSave":
                    var circle = new CircleSave
                    {
                        Name = (string?)child.Element("Name") ?? string.Empty,
                        X = FloatEl(child, "X"),
                        Y = FloatEl(child, "Y"),
                        Radius = FloatEl(child, "Radius", 16f),
                    };
                    ReadColor(child, circle);
                    shapes.Shapes.Add(circle);
                    break;
                case "PolygonSave":
                    var poly = new PolygonSave
                    {
                        Name = (string?)child.Element("Name") ?? string.Empty,
                        X = FloatEl(child, "X"),
                        Y = FloatEl(child, "Y"),
                    };
                    var pointsEl = child.Element("Points");
                    if (pointsEl != null)
                        foreach (var v in pointsEl.Elements("Vector2Save"))
                            poly.Points.Add(new Vector2Save { X = FloatEl(v, "X"), Y = FloatEl(v, "Y") });
                    ReadColor(child, poly);
                    shapes.Shapes.Add(poly);
                    break;
            }
        }

        return shapes;
    }

    // Reads FRB1's per-shape Z/Alpha/Red/Green/Blue, falling back to FRB1 defaults when absent.
    private static void ReadColor(XElement el, Frb1ShapeData target)
    {
        target.Z = FloatEl(el, "Z");
        target.Alpha = FloatEl(el, "Alpha", 1f);
        target.Red = FloatEl(el, "Red", 1f);
        target.Green = FloatEl(el, "Green", 1f);
        target.Blue = FloatEl(el, "Blue", 1f);
    }

    private static float FloatEl(XElement parent, string name, float defaultValue = 0f)
    {
        var el = parent.Element(name);
        return el != null ? float.Parse(el.Value, CultureInfo.InvariantCulture) : defaultValue;
    }

    private static bool BoolEl(XElement parent, string name, bool defaultValue = false)
    {
        var el = parent.Element(name);
        return el != null ? bool.Parse(el.Value) : defaultValue;
    }

    private static int? IntElNullable(XElement parent, string name)
    {
        var el = parent.Element(name);
        return el != null ? int.Parse(el.Value, CultureInfo.InvariantCulture) : null;
    }

    private static ColorOperation? ColorOperationEl(XElement parent, string name)
    {
        var el = parent.Element(name);
        return el != null ? Enum.Parse<ColorOperation>(el.Value) : null;
    }

    private static AnimationChainListSave ParseJson(JsonObject root)
    {
        var result = new AnimationChainListSave();

        if (root["fileRelativeTextures"] is JsonValue frt)
            result.FileRelativeTextures = frt.GetValue<bool>();

        if (root["timeMeasurementUnit"] is JsonValue tmu)
            result.TimeMeasurementUnit = Enum.Parse<TimeMeasurementUnit>(tmu.GetValue<string>()!);

        if (root["coordinateType"] is JsonValue ct)
            result.CoordinateType = Enum.Parse<TextureCoordinateType>(ct.GetValue<string>()!);

        if (root["animationChains"] is JsonArray chainsArray)
        {
            foreach (var chainNode in chainsArray)
            {
                var chainObj = chainNode!.AsObject();
                var chain = new AnimationChainSave
                {
                    Name = chainObj["name"]?.GetValue<string>() ?? string.Empty,
                    IsLocked = BoolProp(chainObj, "locked"),
                };

                if (chainObj["frames"] is JsonArray framesArray)
                    foreach (var frameNode in framesArray)
                        chain.Frames.Add(ParseFrameJson(frameNode!.AsObject()));

                result.AnimationChains.Add(chain);
            }
        }

        if (root["projectFile"] is JsonValue pf)
            result.ProjectFile = pf.GetValue<string>();

        return result;
    }

    private static AnimationFrameSave ParseFrameJson(JsonObject el)
    {
        var frame = new AnimationFrameSave
        {
            TextureName = el["textureName"]?.GetValue<string>() ?? string.Empty,
            FrameLength = FloatProp(el, "frameLength"),
            LeftCoordinate = FloatProp(el, "leftCoordinate"),
            RightCoordinate = FloatProp(el, "rightCoordinate", 1f),
            TopCoordinate = FloatProp(el, "topCoordinate"),
            BottomCoordinate = FloatProp(el, "bottomCoordinate", 1f),
            FlipHorizontal = BoolProp(el, "flipHorizontal"),
            FlipVertical = BoolProp(el, "flipVertical"),
            FlipDiagonal = BoolProp(el, "flipDiagonal"),
            RelativeX = FloatProp(el, "relativeX"),
            RelativeY = FloatProp(el, "relativeY"),
            Red = IntPropNullable(el, "red"),
            Green = IntPropNullable(el, "green"),
            Blue = IntPropNullable(el, "blue"),
            Alpha = IntPropNullable(el, "alpha"),
            ColorOperation = ColorOperationProp(el, "colorOperation"),
        };

        if (el["shapes"] is JsonObject shapesObj)
            frame.ShapesSave = ParseShapesJson(shapesObj);

        return frame;
    }

    private static ShapesSave ParseShapesJson(JsonObject el)
    {
        var shapes = new ShapesSave();

        if (el["rectangles"] is JsonArray rectsArray)
        {
            foreach (var node in rectsArray)
            {
                var r = node!.AsObject();
                var rect = new AARectSave
                {
                    Name = r["name"]?.GetValue<string>() ?? string.Empty,
                    X = FloatProp(r, "x"),
                    Y = FloatProp(r, "y"),
                    ScaleX = FloatProp(r, "scaleX", 16f),
                    ScaleY = FloatProp(r, "scaleY", 16f),
                };
                ReadColorJson(r, rect);
                shapes.Shapes.Add(rect);
            }
        }

        if (el["circles"] is JsonArray circlesArray)
        {
            foreach (var node in circlesArray)
            {
                var c = node!.AsObject();
                var circle = new CircleSave
                {
                    Name = c["name"]?.GetValue<string>() ?? string.Empty,
                    X = FloatProp(c, "x"),
                    Y = FloatProp(c, "y"),
                    Radius = FloatProp(c, "radius", 16f),
                };
                ReadColorJson(c, circle);
                shapes.Shapes.Add(circle);
            }
        }

        if (el["polygons"] is JsonArray polysArray)
        {
            foreach (var node in polysArray)
            {
                var p = node!.AsObject();
                var poly = new PolygonSave
                {
                    Name = p["name"]?.GetValue<string>() ?? string.Empty,
                    X = FloatProp(p, "x"),
                    Y = FloatProp(p, "y"),
                };
                if (p["points"] is JsonArray pointsArray)
                    foreach (var ptNode in pointsArray)
                    {
                        var pt = ptNode!.AsObject();
                        poly.Points.Add(new Vector2Save { X = FloatProp(pt, "x"), Y = FloatProp(pt, "y") });
                    }
                ReadColorJson(p, poly);
                shapes.Shapes.Add(poly);
            }
        }

        return shapes;
    }

    // JSON counterpart of ReadColor: reads per-shape Z/Alpha/Red/Green/Blue, falling back to
    // FRB1 defaults when absent.
    private static void ReadColorJson(JsonObject el, Frb1ShapeData target)
    {
        target.Z = FloatProp(el, "z");
        target.Alpha = FloatProp(el, "alpha", 1f);
        target.Red = FloatProp(el, "red", 1f);
        target.Green = FloatProp(el, "green", 1f);
        target.Blue = FloatProp(el, "blue", 1f);
    }

    private static float FloatProp(JsonObject parent, string name, float defaultValue = 0f) =>
        parent[name] is JsonValue v ? v.GetValue<float>() : defaultValue;

    private static bool BoolProp(JsonObject parent, string name, bool defaultValue = false) =>
        parent[name] is JsonValue v ? v.GetValue<bool>() : defaultValue;

    private static int? IntPropNullable(JsonObject parent, string name) =>
        parent[name] is JsonValue v ? v.GetValue<int>() : null;

    private static ColorOperation? ColorOperationProp(JsonObject parent, string name) =>
        parent[name] is JsonValue v ? Enum.Parse<ColorOperation>(v.GetValue<string>()!) : null;
}