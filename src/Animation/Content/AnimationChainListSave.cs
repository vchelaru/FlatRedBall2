using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaTitleContainer = Microsoft.Xna.Framework.TitleContainer;

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
/// Load with <see cref="FromFile"/> and convert to runtime types with
/// <see cref="ToAnimationChainList"/>.
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

    /// <summary>Absolute path of the .achx file. Set automatically by <see cref="FromFile"/>;
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
    /// and needs to bypass the <see cref="ContentLoader"/> stream seam. Production runtime code
    /// should call <c>ContentLoader.LoadAnimationChainList</c> or pass a <c>streamProvider</c>
    /// to the other overload.
    /// </summary>
    public static AnimationChainListSave FromFile(string path)
        => FromFile(path, File.OpenRead!);

    /// <summary>
    /// Loads a .achx file via manual XML parsing (AOT-safe). Production code should prefer
    /// <c>ContentLoader.LoadAnimationChainList(path)</c>, which routes the read through
    /// the service's stream seam (TitleContainer on DesktopGL, HTTP fetch on Blazor). This
    /// overload exists for tooling and tests that work without a <see cref="ContentLoader"/>.
    /// </summary>
    /// <param name="filePath">Path to the .achx file, relative to the title container.</param>
    /// <param name="streamProvider">Optional byte source. Defaults to <c>TitleContainer.OpenStream</c>.</param>
    public static AnimationChainListSave FromFile(string filePath, Func<string, Stream>? streamProvider)
    {
        streamProvider ??= XnaTitleContainer.OpenStream;
        using var stream = streamProvider(filePath);
        var doc = XDocument.Load(stream);
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

        result.FileName = filePath;
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
    /// Frame defaults are omitted to keep diffs small: <c>FlipHorizontal</c>/<c>FlipVertical</c>
    /// are written only when <c>true</c>; <c>RelativeX</c>/<c>RelativeY</c> only when non-zero.
    /// FRB1-only fields (Z, ScaleZ, Alpha/Red/Green/Blue on shapes; AxisAlignedCubeSaves;
    /// SphereSaves) are written as empty list elements for dialect compatibility but their
    /// values are not preserved.
    /// </remarks>
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
            var chainEl = new XElement("AnimationChain",
                new XElement("Name", chain.Name));
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

    private static XElement WriteFrame(AnimationFrameSave frame)
    {
        var el = new XElement("Frame");
        // Match FRB1's element order — FlipHorizontal appears before TextureName when set.
        if (frame.FlipHorizontal) el.Add(new XElement("FlipHorizontal", "true"));
        if (frame.FlipVertical) el.Add(new XElement("FlipVertical", "true"));
        el.Add(new XElement("TextureName", frame.TextureName));
        el.Add(new XElement("FrameLength", FloatStr(frame.FrameLength)));
        el.Add(new XElement("LeftCoordinate", FloatStr(frame.LeftCoordinate)));
        el.Add(new XElement("RightCoordinate", FloatStr(frame.RightCoordinate)));
        el.Add(new XElement("TopCoordinate", FloatStr(frame.TopCoordinate)));
        el.Add(new XElement("BottomCoordinate", FloatStr(frame.BottomCoordinate)));
        if (frame.RelativeX != 0f) el.Add(new XElement("RelativeX", FloatStr(frame.RelativeX)));
        if (frame.RelativeY != 0f) el.Add(new XElement("RelativeY", FloatStr(frame.RelativeY)));

        el.Add(WriteShapes(frame.ShapesSave));
        return el;
    }

    private static XElement WriteShapes(ShapesSave? shapes)
    {
        shapes ??= new ShapesSave();
        var rects = new XElement("AxisAlignedRectangleSaves");
        foreach (var r in shapes.AARectSaves)
        {
            rects.Add(new XElement("AxisAlignedRectangleSave",
                new XElement("X", FloatStr(r.X)),
                new XElement("Y", FloatStr(r.Y)),
                new XElement("ScaleX", FloatStr(r.ScaleX)),
                new XElement("ScaleY", FloatStr(r.ScaleY)),
                new XElement("Name", r.Name)));
        }

        var circles = new XElement("CircleSaves");
        foreach (var c in shapes.CircleSaves)
        {
            circles.Add(new XElement("CircleSave",
                new XElement("X", FloatStr(c.X)),
                new XElement("Y", FloatStr(c.Y)),
                new XElement("Radius", FloatStr(c.Radius)),
                new XElement("Name", c.Name)));
        }

        var polys = new XElement("PolygonSaves");
        foreach (var p in shapes.PolygonSaves)
        {
            var pointsEl = new XElement("Points");
            foreach (var v in p.Points)
            {
                pointsEl.Add(new XElement("Vector2Save",
                    new XElement("X", FloatStr(v.X)),
                    new XElement("Y", FloatStr(v.Y))));
            }
            polys.Add(new XElement("PolygonSave",
                new XElement("Name", p.Name),
                new XElement("X", FloatStr(p.X)),
                new XElement("Y", FloatStr(p.Y)),
                pointsEl));
        }

        // AxisAlignedCubeSaves and SphereSaves are FRB1-era 3D-shape placeholders that FRB2 does
        // not model. Emit empty elements so the dialect matches what FRB1 readers expect.
        return new XElement("ShapeCollectionSave",
            rects,
            new XElement("AxisAlignedCubeSaves"),
            polys,
            circles,
            new XElement("SphereSaves"));
    }

    private static string FloatStr(float value) => value.ToString("R", CultureInfo.InvariantCulture);

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
        frame.RelativeX = FloatEl(el, "RelativeX");
        frame.RelativeY = FloatEl(el, "RelativeY");

        // FRB1 dialect: shape wrapper is <ShapeCollectionSave>. FRB2's <ShapesSave> is not
        // accepted — no .achx files exist in the wild using it (the FRB2 writer didn't exist
        // before this branch), so the dialect is unified on FRB1's element names.
        var shapesEl = el.Element("ShapeCollectionSave");
        if (shapesEl != null)
            frame.ShapesSave = ParseShapes(shapesEl);

        return frame;
    }

    private static ShapesSave ParseShapes(XElement el)
    {
        var shapes = new ShapesSave();

        var aarctsEl = el.Element("AxisAlignedRectangleSaves");
        if (aarctsEl != null)
        {
            foreach (var r in aarctsEl.Elements("AxisAlignedRectangleSave"))
            {
                shapes.AARectSaves.Add(new AARectSave
                {
                    Name = (string?)r.Element("Name") ?? string.Empty,
                    X = FloatEl(r, "X"),
                    Y = FloatEl(r, "Y"),
                    ScaleX = FloatEl(r, "ScaleX", 16f),
                    ScaleY = FloatEl(r, "ScaleY", 16f),
                });
            }
        }

        var circlesEl = el.Element("CircleSaves");
        if (circlesEl != null)
        {
            foreach (var c in circlesEl.Elements("CircleSave"))
            {
                shapes.CircleSaves.Add(new CircleSave
                {
                    Name = (string?)c.Element("Name") ?? string.Empty,
                    X = FloatEl(c, "X"),
                    Y = FloatEl(c, "Y"),
                    Radius = FloatEl(c, "Radius", 16f),
                });
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
                    {
                        poly.Points.Add(new Vector2Save
                        {
                            X = FloatEl(v, "X"),
                            Y = FloatEl(v, "Y"),
                        });
                    }
                }

                shapes.PolygonSaves.Add(poly);
            }
        }

        return shapes;
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

    /// <summary>
    /// Converts this save object to a runtime <see cref="AnimationChainList"/>, loading
    /// all referenced textures through <see cref="ContentLoader.Load{T}"/>.
    /// Texture paths are passed as-is: if the frame's <c>TextureName</c> includes an
    /// extension (e.g. <c>"Player.png"</c>), it loads directly from disk and participates
    /// in PNG hot-reload via <see cref="ContentLoader.TryReload"/>; if there is
    /// no extension, it goes through MonoGame's compiled xnb pipeline (not hot-reloadable).
    /// </summary>
    /// <remarks>
    /// Texture names are resolved relative to the .achx file location when
    /// <see cref="FileRelativeTextures"/> is <c>true</c>.
    /// </remarks>
    public AnimationChainList ToAnimationChainList(FlatRedBall2.ContentLoader contentManager)
    {
        string achxDir = string.IsNullOrEmpty(FileName) ? "" : Path.GetDirectoryName(FileName) ?? "";

        return BuildList(frameSave =>
        {
            if (string.IsNullOrEmpty(frameSave.TextureName)) return null;

            string texPath = FileRelativeTextures && !string.IsNullOrEmpty(achxDir)
                ? Path.Combine(achxDir, frameSave.TextureName)
                : frameSave.TextureName;

            return contentManager.Load<Texture2D>(texPath.Replace('\\', '/'));
        });
    }

    // Shared chain-building logic; loadTexture maps a save frame to its Texture2D (or null).
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
                    RelativeX = frameSave.RelativeX,
                    RelativeY = frameSave.RelativeY,
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

    private static void AppendShapes(FlatRedBall2.Animation.AnimationFrame frame, ShapesSave? shapes)
    {
        if (shapes == null) return;

        foreach (var rect in shapes.AARectSaves)
        {
            ValidateName(rect.Name, "AARectSave");
            frame.Shapes.Add(new FlatRedBall2.Animation.AnimationAARectFrame
            {
                Name = rect.Name,
                RelativeX = rect.X,
                RelativeY = rect.Y,
                Width = rect.ScaleX * 2f,
                Height = rect.ScaleY * 2f,
            });
        }

        foreach (var circle in shapes.CircleSaves)
        {
            ValidateName(circle.Name, "CircleSave");
            frame.Shapes.Add(new FlatRedBall2.Animation.AnimationCircleFrame
            {
                Name = circle.Name,
                RelativeX = circle.X,
                RelativeY = circle.Y,
                Radius = circle.Radius,
            });
        }

        foreach (var poly in shapes.PolygonSaves)
        {
            ValidateName(poly.Name, "PolygonSave");
            var points = new System.Numerics.Vector2[poly.Points.Count];
            for (int i = 0; i < poly.Points.Count; i++)
                points[i] = new System.Numerics.Vector2(poly.Points[i].X, poly.Points[i].Y);
            frame.Shapes.Add(new FlatRedBall2.Animation.AnimationPolygonFrame
            {
                Name = poly.Name,
                RelativeX = poly.X,
                RelativeY = poly.Y,
                Points = points,
            });
        }
    }

    private static void ValidateName(string name, string elementType)
    {
        if (string.IsNullOrEmpty(name))
            throw new System.InvalidOperationException(
                $"{elementType} in .achx ShapesSave is missing a Name. Per-frame shapes must have non-empty unique names.");
    }
}