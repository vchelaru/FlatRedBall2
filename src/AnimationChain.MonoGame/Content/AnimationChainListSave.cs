using System.Globalization;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain.Content;

/// <summary>Time unit used by frames in a .achx file.</summary>
public enum TimeMeasurementUnit
{
    /// <summary>Undefined — treated identically to <see cref="Second"/> for compatibility.</summary>
    Undefined,
    /// <summary>Seconds.</summary>
    Second,
    /// <summary>Milliseconds.</summary>
    Millisecond
}

/// <summary>How texture coordinates are interpreted.</summary>
public enum TextureCoordinateType
{
    /// <summary>Coordinates are normalized (0 to 1).</summary>
    UV,
    /// <summary>Coordinates are raw pixel values.</summary>
    Pixel
}

/// <summary>
/// Deserialized representation of a .achx animation file. Use <see cref="FromFile(string)"/>
/// to load, then <see cref="ToAnimationChainList"/> to convert to runtime types.
/// For the most common case, prefer <see cref="AchxLoader.Load(string)"/> which handles
/// both steps and caches textures automatically.
/// </summary>
public class AnimationChainListSave
{
    /// <summary>
    /// Whether texture file paths stored in frames are relative to the .achx file location.
    /// Defaults to <c>true</c> (the standard .achx convention).
    /// </summary>
    public bool FileRelativeTextures = true;

    /// <summary>The time unit used by all frames in this file.</summary>
    public TimeMeasurementUnit TimeMeasurementUnit = TimeMeasurementUnit.Second;

    /// <summary>How texture coordinates in frames are specified.</summary>
    public TextureCoordinateType CoordinateType = TextureCoordinateType.UV;

    /// <summary>All animation chains in this file.</summary>
    public List<AnimationChainSave> AnimationChains = new();

    /// <summary>
    /// Absolute path of the .achx file. Set automatically by <see cref="FromFile(string)"/>.
    /// Used to resolve relative texture paths in <see cref="ToAnimationChainList"/>.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Path of the project file the .achx belongs to, relative to the .achx location.
    /// Round-tripped by tooling but ignored at runtime.
    /// </summary>
    public string? ProjectFile { get; set; }

    /// <summary>
    /// Loads a .achx file from disk. Use this when you have an absolute path and want to
    /// bypass any custom stream provider.
    /// </summary>
    public static AnimationChainListSave FromFile(string path)
        => FromFile(path, File.OpenRead!);

    /// <summary>
    /// Loads a .achx file via a custom stream provider. Useful for non-filesystem environments
    /// (Blazor WASM, unit tests with in-memory XML) where <see cref="File.OpenRead"/> is
    /// unavailable or undesirable.
    /// </summary>
    /// <param name="filePath">Path passed to <paramref name="streamProvider"/>.</param>
    /// <param name="streamProvider">Returns a readable stream for the given path.</param>
    public static AnimationChainListSave FromFile(string filePath, Func<string, Stream> streamProvider)
    {
        using var stream = streamProvider(filePath);
        var result = ParseXml(XDocument.Load(stream));
        // Store the absolute path so ToAnimationChainList always produces absolute texture paths,
        // preventing double-resolution when callers (e.g. AchxLoader) also combine with achxDir.
        result.FileName = Path.GetFullPath(filePath);
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
        if (frt != null) result.FileRelativeTextures = bool.Parse(frt.Value);

        var tmu = root.Element("TimeMeasurementUnit");
        if (tmu != null) result.TimeMeasurementUnit = Enum.Parse<TimeMeasurementUnit>(tmu.Value);

        var ct = root.Element("CoordinateType");
        if (ct != null) result.CoordinateType = Enum.Parse<TextureCoordinateType>(ct.Value);

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
    /// Writes this save to a .achx file using the FRB1-compatible XML dialect so existing
    /// tooling and engine versions can round-trip the file.
    /// </summary>
    public void Save(string path)
    {
        var root = new XElement("AnimationChainArraySave",
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
            new XElement("FileRelativeTextures", FileRelativeTextures ? "true" : "false"),
            new XElement("TimeMeasurementUnit", TimeMeasurementUnit.ToString()),
            new XElement("CoordinateType", CoordinateType.ToString()));

        foreach (var chain in AnimationChains)
        {
            var chainEl = new XElement("AnimationChain", new XElement("Name", chain.Name));
            foreach (var frame in chain.Frames)
                chainEl.Add(WriteFrame(frame));
            root.Add(chainEl);
        }

        if (ProjectFile != null)
            root.Add(new XElement("ProjectFile", ProjectFile));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        using var stream = File.Create(path);
        doc.Save(stream);
    }

    /// <summary>
    /// Converts this save to a runtime <see cref="AnimationChainList"/>. Texture paths are
    /// resolved relative to the .achx file location (when <see cref="FileRelativeTextures"/>
    /// is <c>true</c>) and passed to <paramref name="textureLoader"/>.
    /// <para>
    /// For most callers, prefer <see cref="AchxLoader.Load(string)"/> which wraps this call
    /// and adds caching so the same spritesheet is not uploaded more than once.
    /// </para>
    /// </summary>
    /// <param name="textureLoader">
    /// Called with the resolved absolute-or-relative texture path. May return <c>null</c>
    /// if the texture is unavailable — the frame will have a <c>null</c> texture.
    /// </param>
    public AnimationChainList ToAnimationChainList(Func<string, Texture2D?> textureLoader)
    {
        string achxDir = string.IsNullOrEmpty(FileName) ? "" : Path.GetDirectoryName(FileName) ?? "";

        return BuildList(frameSave =>
        {
            if (string.IsNullOrEmpty(frameSave.TextureName)) return null;
            string texPath = FileRelativeTextures && !string.IsNullOrEmpty(achxDir)
                ? Path.Combine(achxDir, frameSave.TextureName)
                : frameSave.TextureName;
            return textureLoader(texPath);
        });
    }

    private AnimationChainList BuildList(Func<AnimationFrameSave, Texture2D?> loadTexture)
    {
        float frameLengthDivisor = TimeMeasurementUnit == TimeMeasurementUnit.Millisecond ? 1000f : 1f;
        var list = new AnimationChainList { Name = FileName };

        foreach (var chainSave in AnimationChains)
        {
            var chain = new AnimationChain { Name = chainSave.Name };

            foreach (var frameSave in chainSave.Frames)
            {
                var frame = new AnimationFrame
                {
                    TextureName = frameSave.TextureName,
                    FrameLength = TimeSpan.FromSeconds(frameSave.FrameLength / frameLengthDivisor),
                    FlipHorizontal = frameSave.FlipHorizontal,
                    FlipVertical = frameSave.FlipVertical,
                    FlipDiagonal = frameSave.FlipDiagonal,
                    RelativeX = frameSave.RelativeX,
                    RelativeY = frameSave.RelativeY,
                    Red = frameSave.Red,
                    Green = frameSave.Green,
                    Blue = frameSave.Blue,
                    Alpha = frameSave.Alpha,
                    ColorOperation = frameSave.ColorOperation,
                };

                frame.Texture = loadTexture(frameSave);

                if (frame.Texture != null)
                {
                    int left, top, width, height;
                    if (CoordinateType == TextureCoordinateType.Pixel)
                    {
                        left   = (int)frameSave.LeftCoordinate;
                        top    = (int)frameSave.TopCoordinate;
                        width  = (int)(frameSave.RightCoordinate  - frameSave.LeftCoordinate);
                        height = (int)(frameSave.BottomCoordinate - frameSave.TopCoordinate);
                    }
                    else // UV
                    {
                        left   = (int)(frameSave.LeftCoordinate   * frame.Texture.Width);
                        top    = (int)(frameSave.TopCoordinate    * frame.Texture.Height);
                        width  = (int)((frameSave.RightCoordinate  - frameSave.LeftCoordinate) * frame.Texture.Width);
                        height = (int)((frameSave.BottomCoordinate - frameSave.TopCoordinate)  * frame.Texture.Height);
                    }

                    if (width > 0 && height > 0)
                        frame.SourceRectangle = new Rectangle(left, top, width, height);
                }

                AppendShapes(frame, frameSave.ShapesSave);
                chain.Add(frame);
            }

            list.Add(chain);
        }

        return list;
    }

    private static void AppendShapes(AnimationFrame frame, ShapesSave? shapes)
    {
        if (shapes == null) return;

        foreach (var shape in shapes.Shapes)
        {
            switch (shape)
            {
                case AARectSave rect:
                    ValidateName(rect.Name, "AARectSave");
                    frame.Shapes.Add(new AnimationAARectFrame
                    {
                        Name = rect.Name,
                        RelativeX = rect.X,
                        RelativeY = rect.Y,
                        Width = rect.ScaleX * 2f,
                        Height = rect.ScaleY * 2f,
                    });
                    break;
                case CircleSave circle:
                    ValidateName(circle.Name, "CircleSave");
                    frame.Shapes.Add(new AnimationCircleFrame
                    {
                        Name = circle.Name,
                        RelativeX = circle.X,
                        RelativeY = circle.Y,
                        Radius = circle.Radius,
                    });
                    break;
                case PolygonSave poly:
                    ValidateName(poly.Name, "PolygonSave");
                    var points = new System.Numerics.Vector2[poly.Points.Count];
                    for (int i = 0; i < poly.Points.Count; i++)
                        points[i] = new System.Numerics.Vector2(poly.Points[i].X, poly.Points[i].Y);
                    frame.Shapes.Add(new AnimationPolygonFrame
                    {
                        Name = poly.Name,
                        RelativeX = poly.X,
                        RelativeY = poly.Y,
                        Points = points,
                    });
                    break;
            }
        }
    }

    private static void ValidateName(string name, string elementType)
    {
        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException(
                $"{elementType} in .achx ShapesSave is missing a Name. Per-frame shapes must have non-empty unique names.");
    }

    private static AnimationFrameSave ParseFrame(XElement el)
    {
        var frame = new AnimationFrameSave
        {
            FlipHorizontal = BoolEl(el, "FlipHorizontal"),
            FlipVertical   = BoolEl(el, "FlipVertical"),
            FlipDiagonal   = BoolEl(el, "FlipDiagonal"),
            TextureName    = (string?)el.Element("TextureName") ?? string.Empty,
            FrameLength    = FloatEl(el, "FrameLength"),
            LeftCoordinate = FloatEl(el, "LeftCoordinate"),
            RightCoordinate  = FloatEl(el, "RightCoordinate",  1f),
            TopCoordinate    = FloatEl(el, "TopCoordinate"),
            BottomCoordinate = FloatEl(el, "BottomCoordinate", 1f),
            RelativeX = FloatEl(el, "RelativeX"),
            RelativeY = FloatEl(el, "RelativeY"),
            Red   = IntElNullable(el, "Red"),
            Green = IntElNullable(el, "Green"),
            Blue  = IntElNullable(el, "Blue"),
            Alpha = IntElNullable(el, "Alpha"),
            ColorOperation = ColorOperationEl(el, "ColorOperation"),
        };

        // Frame <Name>/<HasCustomName> in legacy .achx are intentionally ignored: a frame's
        // identity is its index, so the editor always shows a positional "Frame N" label.

        // New dialect: <Shapes> wrapper; old dialect: <ShapeCollectionSave> wrapper.
        var shapesEl = el.Element("Shapes") ?? el.Element("ShapeCollectionSave");
        if (shapesEl != null)
            frame.ShapesSave = ParseShapes(shapesEl);

        return frame;
    }

    private static ShapesSave ParseShapes(XElement el)
    {
        var shapes = new ShapesSave();

        var newShapesEl = el.Element("Shapes");
        if (newShapesEl != null)
        {
            foreach (var child in newShapesEl.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "AxisAlignedRectangleSave":
                        shapes.Shapes.Add(new AARectSave
                        {
                            Name = (string?)child.Element("Name") ?? string.Empty,
                            X = FloatEl(child, "X"),
                            Y = FloatEl(child, "Y"),
                            ScaleX = FloatEl(child, "ScaleX", 16f),
                            ScaleY = FloatEl(child, "ScaleY", 16f),
                        });
                        break;
                    case "CircleSave":
                        shapes.Shapes.Add(new CircleSave
                        {
                            Name = (string?)child.Element("Name") ?? string.Empty,
                            X = FloatEl(child, "X"),
                            Y = FloatEl(child, "Y"),
                            Radius = FloatEl(child, "Radius", 16f),
                        });
                        break;
                    case "PolygonSave":
                        var poly = new PolygonSave
                        {
                            Name = (string?)child.Element("Name") ?? string.Empty,
                            X = FloatEl(child, "X"),
                            Y = FloatEl(child, "Y"),
                        };
                        var polyPointsEl = child.Element("Points");
                        if (polyPointsEl != null)
                            foreach (var v in polyPointsEl.Elements("Vector2Save"))
                                poly.Points.Add(new Vector2Save { X = FloatEl(v, "X"), Y = FloatEl(v, "Y") });
                        shapes.Shapes.Add(poly);
                        break;
                }
            }
            return shapes;
        }

        // Old format fallback: separate typed containers (rects, then circles, then polygons).
        var aarctsEl = el.Element("AxisAlignedRectangleSaves");
        if (aarctsEl != null)
            foreach (var r in aarctsEl.Elements("AxisAlignedRectangleSave"))
                shapes.Shapes.Add(new AARectSave
                {
                    Name = (string?)r.Element("Name") ?? string.Empty,
                    X = FloatEl(r, "X"), Y = FloatEl(r, "Y"),
                    ScaleX = FloatEl(r, "ScaleX", 16f), ScaleY = FloatEl(r, "ScaleY", 16f),
                });

        var circlesEl = el.Element("CircleSaves");
        if (circlesEl != null)
            foreach (var c in circlesEl.Elements("CircleSave"))
                shapes.Shapes.Add(new CircleSave
                {
                    Name = (string?)c.Element("Name") ?? string.Empty,
                    X = FloatEl(c, "X"), Y = FloatEl(c, "Y"),
                    Radius = FloatEl(c, "Radius", 16f),
                });

        var polysEl = el.Element("PolygonSaves");
        if (polysEl != null)
        {
            foreach (var p in polysEl.Elements("PolygonSave"))
            {
                var poly = new PolygonSave
                {
                    Name = (string?)p.Element("Name") ?? string.Empty,
                    X = FloatEl(p, "X"), Y = FloatEl(p, "Y"),
                };
                var pts = p.Element("Points");
                if (pts != null)
                    foreach (var v in pts.Elements("Vector2Save"))
                        poly.Points.Add(new Vector2Save { X = FloatEl(v, "X"), Y = FloatEl(v, "Y") });
                shapes.Shapes.Add(poly);
            }
        }

        return shapes;
    }

    private static XElement WriteFrame(AnimationFrameSave frame)
    {
        var el = new XElement("Frame");
        if (frame.FlipHorizontal) el.Add(new XElement("FlipHorizontal", "true"));
        if (frame.FlipVertical)   el.Add(new XElement("FlipVertical", "true"));
        if (frame.FlipDiagonal)   el.Add(new XElement("FlipDiagonal", "true"));
        el.Add(new XElement("TextureName", frame.TextureName));
        el.Add(new XElement("FrameLength", FloatStr(frame.FrameLength)));
        el.Add(new XElement("LeftCoordinate",   FloatStr(frame.LeftCoordinate)));
        el.Add(new XElement("RightCoordinate",  FloatStr(frame.RightCoordinate)));
        el.Add(new XElement("TopCoordinate",    FloatStr(frame.TopCoordinate)));
        el.Add(new XElement("BottomCoordinate", FloatStr(frame.BottomCoordinate)));
        if (frame.RelativeX != 0f) el.Add(new XElement("RelativeX", FloatStr(frame.RelativeX)));
        if (frame.RelativeY != 0f) el.Add(new XElement("RelativeY", FloatStr(frame.RelativeY)));
        if (frame.Red.HasValue)   el.Add(new XElement("Red", frame.Red.Value));
        if (frame.Green.HasValue) el.Add(new XElement("Green", frame.Green.Value));
        if (frame.Blue.HasValue)  el.Add(new XElement("Blue", frame.Blue.Value));
        if (frame.Alpha.HasValue) el.Add(new XElement("Alpha", frame.Alpha.Value));
        if (frame.ColorOperation.HasValue) el.Add(new XElement("ColorOperation", frame.ColorOperation.Value.ToString()));
        el.Add(WriteShapesElement(frame.ShapesSave));
        return el;
    }

    private static XElement WriteShapesElement(ShapesSave? shapes)
    {
        shapes ??= new ShapesSave();
        var shapesEl = new XElement("ShapeCollectionSave");
        var innerEl = new XElement("Shapes");
        foreach (var shape in shapes.Shapes)
        {
            switch (shape)
            {
                case AARectSave r:
                    innerEl.Add(new XElement("AxisAlignedRectangleSave",
                        new XElement("Name", r.Name), new XElement("X", FloatStr(r.X)), new XElement("Y", FloatStr(r.Y)),
                        new XElement("ScaleX", FloatStr(r.ScaleX)), new XElement("ScaleY", FloatStr(r.ScaleY))));
                    break;
                case CircleSave c:
                    innerEl.Add(new XElement("CircleSave",
                        new XElement("Name", c.Name), new XElement("X", FloatStr(c.X)), new XElement("Y", FloatStr(c.Y)),
                        new XElement("Radius", FloatStr(c.Radius))));
                    break;
                case PolygonSave p:
                    var polyEl = new XElement("PolygonSave",
                        new XElement("Name", p.Name), new XElement("X", FloatStr(p.X)), new XElement("Y", FloatStr(p.Y)));
                    var ptsEl = new XElement("Points");
                    foreach (var v in p.Points)
                        ptsEl.Add(new XElement("Vector2Save", new XElement("X", FloatStr(v.X)), new XElement("Y", FloatStr(v.Y))));
                    polyEl.Add(ptsEl);
                    innerEl.Add(polyEl);
                    break;
            }
        }
        shapesEl.Add(innerEl);
        return shapesEl;
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

    private static string FloatStr(float v) => v.ToString("G9", CultureInfo.InvariantCulture);
}
