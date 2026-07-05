using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FlatRedBall2.Animation.Content;

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
/// Deserialized representation of a .achx animation file.
/// Load with <see cref="FromFile(string)"/> and convert to runtime types with
/// <c>ToAnimationChainList</c> (an extension method in the main FlatRedBall2 engine
/// assembly — this type itself has no MonoGame dependency so tooling, like the
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
    /// Loads a .achx file via manual XML parsing (AOT-safe). Production code should prefer
    /// <c>ContentLoader.LoadAnimationChainList(path)</c>, which routes the read through
    /// the service's stream seam (TitleContainer on DesktopGL, HTTP fetch on Blazor). This
    /// overload exists for tooling and tests that work without a <c>ContentLoader</c>.
    /// </summary>
    /// <param name="filePath">Path to the .achx file, interpreted by <paramref name="streamProvider"/>.</param>
    /// <param name="streamProvider">Byte source. Callers must supply one — there is no default — so this
    /// method has no IL-level reference to <c>TitleContainer</c> and tools that don't ship MonoGame.Framework
    /// (e.g. AnimationEditor on Avalonia) can call it without triggering an assembly load.</param>
    public static AnimationChainListSave FromFile(string filePath, Func<string, Stream> streamProvider)
    {
        using var stream = streamProvider(filePath);
        var result = ParseXml(XDocument.Load(stream));
        result.FileName = filePath;
        return result;
    }

    /// <summary>
    /// Parses .achx XML from an already-open <paramref name="stream"/>. <see cref="FileName"/>
    /// is set to <see cref="string.Empty"/>; if <see cref="FileRelativeTextures"/> is <c>true</c>,
    /// texture paths in the file are passed through as-is with no directory prefix prepended.
    /// The caller retains ownership of <paramref name="stream"/> and is responsible for disposing it.
    /// </summary>
    public static AnimationChainListSave FromStream(Stream stream)
        => ParseXml(XDocument.Load(stream));

    /// <summary>
    /// Parses .achx XML from an in-memory string. <see cref="FileName"/> is set to
    /// <see cref="string.Empty"/>; if <see cref="FileRelativeTextures"/> is <c>true</c>,
    /// texture paths in the file are passed through as-is with no directory prefix prepended.
    /// </summary>
    public static AnimationChainListSave FromString(string xml)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return FromStream(stream);
    }

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
                Name = (string?)chainEl.Element("Name") ?? string.Empty
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
    /// Serializes this save to a .achx XML string using the same dialect as <see cref="Save(string)"/>.
    /// Lets the Animation Editor clipboard share one serializer with file I/O, so copy/paste of
    /// frames and chains round-trips per-frame shapes exactly as the on-disk format does. Pair
    /// with <see cref="FromString"/> to read it back.
    /// </summary>
    public string ToXmlString() => ToXDocument().ToString();

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
}