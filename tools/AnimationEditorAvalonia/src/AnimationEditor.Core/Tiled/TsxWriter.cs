using DotTiled;
using DotTiled.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// Writes a <see cref="DotTiled.Tileset"/> back out to a Tiled <c>.tsx</c> file. DotTiled has a
/// complete read-side object model but no save/write support (see
/// https://github.com/dcronqvist/DotTiled/issues/77) -- this writer is built directly on
/// DotTiled's own types, shaped so it could later become a PR against DotTiled itself.
/// </summary>
/// <remarks>
/// Covers the subset of the format needed to round-trip a tileset: tileset attributes, the
/// tileset image, tile offset/grid, tileset- and tile-level properties (string/int/float/bool/
/// color/file/object), and per-tile type/probability/x/y/width/height/image/animation frames.
/// Wangsets, transformations, per-tile object layers, and custom class/enum properties are not
/// supported -- writing a <see cref="Tileset"/> that uses any of those throws
/// <see cref="NotSupportedException"/> rather than silently dropping data.
///
/// <para>Writing to a path that already exists patches in place: every unchanged &lt;tile&gt;
/// keeps its original file text byte-for-byte, and only tiles whose content actually differs get
/// regenerated. Real Tiled does the same -- it edits its in-memory document and only reserializes
/// what changed, so a one-tile edit costs one changed line, not a whole-file reformat. Without
/// this, every save from this writer looked like it had rewritten the entire tileset, because
/// full-model regeneration has no way to know (or preserve) how the original file happened to be
/// formatted. In-place patching only tracks tile-level content; if a tileset's top-level
/// attributes/image/grid/properties differ from the file on disk (nothing in this codebase does
/// that today), it falls back to the old full rewrite instead of guessing how to patch those.</para>
/// </remarks>
public static class TsxWriter
{
    public static void Write(Tileset tileset, string path)
    {
        if (File.Exists(path) && TryWritePatched(tileset, path))
            return;

        // The full rewrite regenerates the root element from the DotTiled model, which has no
        // slot for every attribute Tiled can put there (backgroundcolor, for one). The patch path
        // keeps the original root text verbatim, so this only matters when patching wasn't
        // possible -- carry those attributes over from the file rather than drop them.
        var extraRootAttributes = File.Exists(path) ? ReadUnmodelledRootAttributes(path) : [];

        using var stream = File.Create(path);
        Write(tileset, stream, extraRootAttributes);
    }

    public static void Write(Tileset tileset, Stream stream) => Write(tileset, stream, []);

    private static void Write(Tileset tileset, Stream stream, IReadOnlyList<(string Name, string Value)> extraRootAttributes)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = " ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        using var writer = XmlWriter.Create(stream, settings);
        writer.WriteStartDocument();
        WriteTileset(writer, tileset, extraRootAttributes);
        writer.WriteEndDocument();
    }

    /// <summary>Every attribute the writer itself emits on &lt;tileset&gt;; anything else found on
    /// an existing file's root is unknown to the DotTiled model and must be copied through.</summary>
    private static readonly HashSet<string> ModelledRootAttributes = new(StringComparer.Ordinal)
    {
        "version", "tiledversion", "name", "class", "tilewidth", "tileheight", "spacing", "margin",
        "tilecount", "columns", "objectalignment", "tilerendersize", "fillmode",
    };

    private static IReadOnlyList<(string Name, string Value)> ReadUnmodelledRootAttributes(string path)
    {
        try
        {
            var root = XDocument.Load(path).Root;
            if (root is null || root.Name.LocalName != "tileset")
                return [];
            return root.Attributes()
                .Where(a => !a.IsNamespaceDeclaration && !ModelledRootAttributes.Contains(a.Name.LocalName))
                .Select(a => (a.Name.LocalName, a.Value))
                .ToList();
        }
        catch
        {
            // An unreadable original can't have anything worth carrying; the rewrite proceeds
            // exactly as it always did.
            return [];
        }
    }

    /// <summary>
    /// Reuses <paramref name="path"/>'s original text for every &lt;tile&gt; whose content is
    /// unchanged, and only regenerates the ones that differ (added, removed, or edited). Returns
    /// false -- meaning the caller should fall back to <see cref="Write(Tileset, Stream)"/> -- when
    /// anything outside tile content changed (only tile-level patching is implemented), when the
    /// file's shape doesn't match the one-node-per-line assumption this relies on, or when
    /// anything about the existing file is unexpected in a way this hasn't anticipated. Patching is
    /// purely an optimization: any failure here must be safe to treat as "couldn't patch," never as
    /// "the save failed" or "here's some corrupted output" -- so every exit path, including
    /// exceptions, falls back to the full rewrite instead.
    /// </summary>
    private static bool TryWritePatched(Tileset tileset, string path)
    {
        try
        {
            return TryWritePatchedCore(tileset, path);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWritePatchedCore(Tileset tileset, string path)
    {
        var original = TsxLoader.LoadTileset(path);
        if (!TopLevelEquals(original, tileset))
            return false;

        var rawText = File.ReadAllText(path);
        var closingTagOffset = rawText.LastIndexOf("</tileset>", StringComparison.Ordinal);
        if (closingTagOffset < 0)
            return false;

        var lineStarts = ComputeLineStartOffsets(rawText);
        var xdoc = XDocument.Load(new StringReader(rawText), LoadOptions.SetLineInfo);
        var originalTileElements = xdoc.Root!.Elements("tile").ToList();
        // Throws (caught above) if the file has two <tile> elements sharing one id -- already
        // invalid TSX, and not something worth a bespoke recovery path for.
        var originalTilesById = original.Tiles.ToDictionary(t => t.ID);

        // Anchored on each <tile>'s own LINE START (not its "<" column) so every slice -- reused
        // or freshly rendered -- carries its own leading indentation and trailing newline the same
        // way. That symmetry is what lets tiles be concatenated back to back with no position-
        // dependent special casing (first/last/only tile all behave identically). This assumes
        // Tiled's/this writer's one-node-per-line convention; TileLineLooksLikeATile guards it, and
        // the duplicate check just below catches two <tile>s sharing one line (same line start)
        // even when TileLineLooksLikeATile can't tell them apart from the outside.
        var tileLineStarts = originalTileElements
            .Select(e => lineStarts[((IXmlLineInfo)e).LineNumber - 1])
            .ToList();
        if (tileLineStarts.Count != tileLineStarts.Distinct().Count())
            return false;

        var originalSlicesById = new Dictionary<uint, string>();
        for (var i = 0; i < originalTileElements.Count; i++)
        {
            var start = tileLineStarts[i];
            var end = i + 1 < tileLineStarts.Count ? tileLineStarts[i + 1] : closingTagOffset;
            if (!TileLineLooksLikeATile(rawText, start))
                return false;

            var id = uint.Parse(originalTileElements[i].Attribute("id")!.Value);
            originalSlicesById[id] = rawText[start..end];
        }

        var prologueEnd = tileLineStarts.Count > 0 ? tileLineStarts[0] : closingTagOffset;
        var newline = rawText.Contains("\r\n") ? "\r\n" : "\n";

        var sb = new StringBuilder(rawText[..prologueEnd]);
        foreach (var tile in tileset.Tiles)
        {
            if (originalSlicesById.TryGetValue(tile.ID, out var originalSlice)
                && originalTilesById.TryGetValue(tile.ID, out var originalTile)
                && TileContentEquals(originalTile, tile))
            {
                sb.Append(originalSlice);
                continue;
            }

            sb.Append(RenderTileFragment(tile, newline)).Append(newline);
        }
        sb.Append(rawText[closingTagOffset..]);

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return true;
    }

    /// <summary>
    /// Guards the one-node-per-line assumption <see cref="TryWritePatchedCore"/> relies on to slice
    /// by line start: a line that (after leading whitespace) doesn't actually begin with a "&lt;tile"
    /// element -- on its own, not as a prefix of some other element name like "&lt;tileoffset" --
    /// means something is sharing that line in a way this writer doesn't understand, so the caller
    /// should fall back to a full rewrite instead of slicing garbage. Without the word-boundary
    /// check, a &lt;tileoffset&gt; sharing a line with the first &lt;tile&gt; would pass this check
    /// (it does start with the literal text "&lt;tile"), and if that tile were then edited, the
    /// &lt;tileoffset&gt; sitting before it on the same line would be silently dropped -- it belongs
    /// to neither the prologue (which ends before that line) nor the regenerated tile fragment
    /// (which only knows about the tile itself).
    /// </summary>
    private static bool TileLineLooksLikeATile(string rawText, int lineStart)
    {
        var trimmed = rawText[lineStart..].TrimStart(' ');
        if (!trimmed.StartsWith("<tile", StringComparison.Ordinal))
            return false;

        var afterPrefix = trimmed.Length > 5 ? trimmed[5] : ' ';
        return afterPrefix is ' ' or '>' or '\t';
    }

    /// <summary>Renders one &lt;tile&gt; element (no trailing newline) at the indentation depth it
    /// has as a direct child of &lt;tileset&gt;, including its own leading indentation -- callers
    /// never add indentation of their own, matching how a reused original slice already carries
    /// its leading space.</summary>
    private static string RenderTileFragment(Tile tile, string newline)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = " ",
            NewLineChars = newline,
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        using var ms = new MemoryStream();
        using (var writer = XmlWriter.Create(ms, settings))
            WriteTile(writer, tile);

        var text = Encoding.UTF8.GetString(ms.ToArray()).TrimEnd('\r', '\n');
        return string.Join(newline, text.Split([newline], StringSplitOptions.None).Select(line => " " + line));
    }

    private static int[] ComputeLineStartOffsets(string text)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
            if (text[i] == '\n')
                starts.Add(i + 1);
        return [.. starts];
    }

    private static bool TopLevelEquals(Tileset a, Tileset b) =>
        a.Version == b.Version &&
        a.TiledVersion == b.TiledVersion &&
        a.Name == b.Name &&
        a.Class == b.Class &&
        a.TileWidth == b.TileWidth &&
        a.TileHeight == b.TileHeight &&
        a.Spacing == b.Spacing &&
        a.Margin == b.Margin &&
        a.TileCount == b.TileCount &&
        a.Columns == b.Columns &&
        a.ObjectAlignment == b.ObjectAlignment &&
        a.RenderSize == b.RenderSize &&
        a.FillMode == b.FillMode &&
        ImagesEqual(a.Image, b.Image) &&
        TileOffsetsEqual(a.TileOffset, b.TileOffset) &&
        GridsEqual(a.Grid, b.Grid) &&
        PropertiesEqual(a.Properties, b.Properties) &&
        a.Wangsets.Count == b.Wangsets.Count &&
        a.Transformations.HasValue == b.Transformations.HasValue;

    // ObjectLayer content itself is never compared (DotTiled gives no cheap equality for it,
    // and WriteTile throws NotSupportedException the moment it actually has to render one) --
    // only presence is. That's deliberate, not an oversight: nothing in this codebase mutates a
    // tile's collision data, so an unchanged ObjectLayer-bearing tile round-trips by reusing its
    // original slice, verbatim, without ever hitting that throw. If a future caller starts
    // editing ObjectLayer content, this stays correct (the edited tile just fails to match on
    // some other field too) right up until the day a change touches ObjectLayer and nothing
    // else, at which point it would need updating to compare it for real.
    private static bool TileContentEquals(Tile a, Tile b) =>
        a.Type == b.Type &&
        a.Probability == b.Probability &&
        a.X == b.X &&
        a.Y == b.Y &&
        a.Width == b.Width &&
        a.Height == b.Height &&
        a.ObjectLayer.HasValue == b.ObjectLayer.HasValue &&
        PropertiesEqual(a.Properties, b.Properties) &&
        ImagesEqual(a.Image, b.Image) &&
        AnimationEqual(a.Animation, b.Animation);

    private static bool ImagesEqual(Optional<Image> a, Optional<Image> b)
    {
        if (a.HasValue != b.HasValue)
            return false;
        if (!a.HasValue)
            return true;

        var x = a.Value;
        var y = b.Value;
        return x.Format == y.Format && x.Source == y.Source && x.TransparentColor == y.TransparentColor
            && x.Width == y.Width && x.Height == y.Height;
    }

    private static bool TileOffsetsEqual(Optional<TileOffset> a, Optional<TileOffset> b)
    {
        if (a.HasValue != b.HasValue)
            return false;
        return !a.HasValue || (a.Value.X == b.Value.X && a.Value.Y == b.Value.Y);
    }

    private static bool GridsEqual(Optional<Grid> a, Optional<Grid> b)
    {
        if (a.HasValue != b.HasValue)
            return false;
        return !a.HasValue
            || (a.Value.Orientation == b.Value.Orientation && a.Value.Width == b.Value.Width && a.Value.Height == b.Value.Height);
    }

    private static bool AnimationEqual(List<Frame> a, List<Frame> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => pair.First.TileID == pair.Second.TileID && pair.First.Duration == pair.Second.Duration);

    private static bool PropertiesEqual(List<IProperty> a, List<IProperty> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => PropertySignature(pair.First) == PropertySignature(pair.Second));

    private static string PropertySignature(IProperty property) => property switch
    {
        StringProperty p => $"string|{p.Name}|{p.Value}",
        IntProperty p => $"int|{p.Name}|{p.Value}",
        FloatProperty p => $"float|{p.Name}|{p.Value}",
        BoolProperty p => $"bool|{p.Name}|{p.Value}",
        ColorProperty p => $"color|{p.Name}|{(p.Value.HasValue ? p.Value.Value.ToString() : "")}",
        FileProperty p => $"file|{p.Name}|{p.Value}",
        ObjectProperty p => $"object|{p.Name}|{p.Value}",
        _ => throw new NotSupportedException(
            $"TsxWriter does not yet support '{property.Type}' properties (property '{property.Name}')."),
    };

    private static void WriteTileset(XmlWriter writer, Tileset tileset, IReadOnlyList<(string Name, string Value)> extraRootAttributes)
    {
        if (tileset.Wangsets.Count > 0)
            throw new NotSupportedException("TsxWriter does not yet support wangsets.");
        if (tileset.Transformations.HasValue)
            throw new NotSupportedException("TsxWriter does not yet support tileset transformations.");

        writer.WriteStartElement("tileset");

        if (tileset.Version.HasValue)
            writer.WriteAttributeString("version", tileset.Version.Value);
        if (tileset.TiledVersion.HasValue)
            writer.WriteAttributeString("tiledversion", tileset.TiledVersion.Value);
        writer.WriteAttributeString("name", tileset.Name);
        if (!string.IsNullOrEmpty(tileset.Class))
            writer.WriteAttributeString("class", tileset.Class);
        writer.WriteAttributeString("tilewidth", XmlConvert.ToString(tileset.TileWidth));
        writer.WriteAttributeString("tileheight", XmlConvert.ToString(tileset.TileHeight));
        if (tileset.Spacing != 0)
            writer.WriteAttributeString("spacing", XmlConvert.ToString(tileset.Spacing));
        if (tileset.Margin != 0)
            writer.WriteAttributeString("margin", XmlConvert.ToString(tileset.Margin));
        writer.WriteAttributeString("tilecount", XmlConvert.ToString(tileset.TileCount));
        writer.WriteAttributeString("columns", XmlConvert.ToString(tileset.Columns));
        if (tileset.ObjectAlignment != ObjectAlignment.Unspecified)
            writer.WriteAttributeString("objectalignment", ToAttributeString(tileset.ObjectAlignment));
        if (tileset.RenderSize != TileRenderSize.Tile)
            writer.WriteAttributeString("tilerendersize", ToAttributeString(tileset.RenderSize));
        if (tileset.FillMode != FillMode.Stretch)
            writer.WriteAttributeString("fillmode", ToAttributeString(tileset.FillMode));
        foreach (var (name, value) in extraRootAttributes)
            writer.WriteAttributeString(name, value);

        if (tileset.Image.HasValue)
            WriteImage(writer, tileset.Image.Value);
        if (tileset.TileOffset.HasValue)
            WriteTileOffset(writer, tileset.TileOffset.Value);
        if (tileset.Grid.HasValue)
            WriteGrid(writer, tileset.Grid.Value);
        WriteProperties(writer, tileset.Properties);
        foreach (var tile in tileset.Tiles)
            WriteTile(writer, tile);

        writer.WriteEndElement();
    }

    private static void WriteImage(XmlWriter writer, Image image)
    {
        writer.WriteStartElement("image");
        if (image.Format.HasValue)
            writer.WriteAttributeString("format", image.Format.Value.ToString().ToLowerInvariant());
        if (image.Source.HasValue)
            writer.WriteAttributeString("source", image.Source.Value);
        if (image.TransparentColor.HasValue)
            writer.WriteAttributeString("trans", image.TransparentColor.Value.ToString());
        if (image.Width.HasValue)
            writer.WriteAttributeString("width", XmlConvert.ToString(image.Width.Value));
        if (image.Height.HasValue)
            writer.WriteAttributeString("height", XmlConvert.ToString(image.Height.Value));
        writer.WriteEndElement();
    }

    private static void WriteTileOffset(XmlWriter writer, TileOffset tileOffset)
    {
        writer.WriteStartElement("tileoffset");
        writer.WriteAttributeString("x", XmlConvert.ToString(tileOffset.X));
        writer.WriteAttributeString("y", XmlConvert.ToString(tileOffset.Y));
        writer.WriteEndElement();
    }

    private static void WriteGrid(XmlWriter writer, Grid grid)
    {
        writer.WriteStartElement("grid");
        if (grid.Orientation != GridOrientation.Orthogonal)
            writer.WriteAttributeString("orientation", grid.Orientation.ToString().ToLowerInvariant());
        writer.WriteAttributeString("width", XmlConvert.ToString(grid.Width));
        writer.WriteAttributeString("height", XmlConvert.ToString(grid.Height));
        writer.WriteEndElement();
    }

    private static void WriteTile(XmlWriter writer, Tile tile)
    {
        if (tile.ObjectLayer.HasValue)
            throw new NotSupportedException($"TsxWriter does not yet support per-tile object layers (tile {tile.ID}).");

        writer.WriteStartElement("tile");
        writer.WriteAttributeString("id", XmlConvert.ToString(tile.ID));
        if (!string.IsNullOrEmpty(tile.Type))
            writer.WriteAttributeString("type", tile.Type);
        if (tile.Probability != 0f)
            writer.WriteAttributeString("probability", XmlConvert.ToString(tile.Probability));
        if (tile.X != 0)
            writer.WriteAttributeString("x", XmlConvert.ToString(tile.X));
        if (tile.Y != 0)
            writer.WriteAttributeString("y", XmlConvert.ToString(tile.Y));
        // Width/height are only meaningful (and only ever written by Tiled) for image-collection
        // tiles that carry their own per-tile image; a tile sharing the tileset's single image
        // has no width/height in the file and DotTiled defaults both to 0 on read.
        if (tile.Width != 0)
            writer.WriteAttributeString("width", XmlConvert.ToString(tile.Width));
        if (tile.Height != 0)
            writer.WriteAttributeString("height", XmlConvert.ToString(tile.Height));

        WriteProperties(writer, tile.Properties);
        if (tile.Image.HasValue)
            WriteImage(writer, tile.Image.Value);
        WriteAnimation(writer, tile.Animation);

        writer.WriteEndElement();
    }

    private static void WriteAnimation(XmlWriter writer, List<Frame> frames)
    {
        if (frames.Count == 0)
            return;

        writer.WriteStartElement("animation");
        foreach (var frame in frames)
        {
            writer.WriteStartElement("frame");
            writer.WriteAttributeString("tileid", XmlConvert.ToString(frame.TileID));
            writer.WriteAttributeString("duration", XmlConvert.ToString(frame.Duration));
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteProperties(XmlWriter writer, List<IProperty> properties)
    {
        if (properties.Count == 0)
            return;

        writer.WriteStartElement("properties");
        foreach (var property in properties)
            WriteProperty(writer, property);
        writer.WriteEndElement();
    }

    private static void WriteProperty(XmlWriter writer, IProperty property)
    {
        writer.WriteStartElement("property");
        writer.WriteAttributeString("name", property.Name);

        switch (property)
        {
            case StringProperty stringProperty:
                writer.WriteAttributeString("value", stringProperty.Value);
                break;
            case IntProperty intProperty:
                writer.WriteAttributeString("type", "int");
                writer.WriteAttributeString("value", XmlConvert.ToString(intProperty.Value));
                break;
            case FloatProperty floatProperty:
                writer.WriteAttributeString("type", "float");
                writer.WriteAttributeString("value", XmlConvert.ToString(floatProperty.Value));
                break;
            case BoolProperty boolProperty:
                writer.WriteAttributeString("type", "bool");
                writer.WriteAttributeString("value", XmlConvert.ToString(boolProperty.Value));
                break;
            case ColorProperty colorProperty:
                writer.WriteAttributeString("type", "color");
                writer.WriteAttributeString("value", colorProperty.Value.HasValue ? colorProperty.Value.Value.ToString() : "");
                break;
            case FileProperty fileProperty:
                writer.WriteAttributeString("type", "file");
                writer.WriteAttributeString("value", fileProperty.Value);
                break;
            case ObjectProperty objectProperty:
                writer.WriteAttributeString("type", "object");
                writer.WriteAttributeString("value", XmlConvert.ToString(objectProperty.Value));
                break;
            default:
                throw new NotSupportedException(
                    $"TsxWriter does not yet support '{property.Type}' properties (property '{property.Name}').");
        }

        writer.WriteEndElement();
    }

    private static string ToAttributeString(ObjectAlignment alignment) => alignment switch
    {
        ObjectAlignment.Unspecified => "unspecified",
        ObjectAlignment.TopLeft => "topleft",
        ObjectAlignment.Top => "top",
        ObjectAlignment.TopRight => "topright",
        ObjectAlignment.Left => "left",
        ObjectAlignment.Center => "center",
        ObjectAlignment.Right => "right",
        ObjectAlignment.BottomLeft => "bottomleft",
        ObjectAlignment.Bottom => "bottom",
        ObjectAlignment.BottomRight => "bottomright",
        _ => throw new NotSupportedException($"Unknown object alignment '{alignment}'."),
    };

    private static string ToAttributeString(TileRenderSize renderSize) => renderSize switch
    {
        TileRenderSize.Tile => "tile",
        TileRenderSize.Grid => "grid",
        _ => throw new NotSupportedException($"Unknown tile render size '{renderSize}'."),
    };

    private static string ToAttributeString(FillMode fillMode) => fillMode switch
    {
        FillMode.Stretch => "stretch",
        FillMode.PreserveAspectFit => "preserve-aspect-fit",
        _ => throw new NotSupportedException($"Unknown fill mode '{fillMode}'."),
    };
}
