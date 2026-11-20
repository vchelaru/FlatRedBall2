using DotTiled;
using DotTiled.Serialization;
using FlatRedBall2.AnimationEditorCommon;
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
/// color/file/object/class/enum), and per-tile type/probability/x/y/width/height/image/animation frames.
/// Wangsets (colors, tiles, properties) are written too. DotTiled does not model a wangset's
/// <c>type</c> attribute (corner/edge/mixed), so a write to an existing file carries each
/// wangset's <c>type</c> over from that file by wangset name. Per-tile object layers are written
/// too (rectangle, ellipse, point, polygon, polyline, text and tile objects, with properties) and
/// compared by their rendered XML, so an object edit always saves. Custom class properties are
/// written with their nested members, and enum properties keep their <c>propertytype</c> (DotTiled
/// reads an enum as a plain string/int, so <see cref="TsxLoader"/> turns it into an
/// <see cref="EnumReferenceProperty"/>); the type definitions themselves live in the Tiled project,
/// not the tsx. Transformations are not supported -- writing a <see cref="Tileset"/> that uses them
/// throws <see cref="NotSupportedException"/> rather than silently dropping data.
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
        var wangsetTypes = File.Exists(path) ? ReadWangsetTypes(path) : new Dictionary<string, string>();

        AtomicFile.Write(path, stream => Write(tileset, stream, extraRootAttributes, wangsetTypes));
    }

    public static void Write(Tileset tileset, Stream stream) => Write(tileset, stream, [], new Dictionary<string, string>());

    private static void Write(
        Tileset tileset,
        Stream stream,
        IReadOnlyList<(string Name, string Value)> extraRootAttributes,
        IReadOnlyDictionary<string, string> wangsetTypes)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = " ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        using var writer = XmlWriter.Create(stream, settings);
        writer.WriteStartDocument();
        WriteTileset(writer, tileset, extraRootAttributes, wangsetTypes);
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

    /// <summary>Each wangset's <c>type</c> attribute in the file at <paramref name="path"/>, by
    /// wangset name (DotTiled drops it on load).</summary>
    private static Dictionary<string, string> ReadWangsetTypes(string path)
    {
        var types = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var root = XDocument.Load(path).Root;
            foreach (var wangset in root?.Element("wangsets")?.Elements("wangset") ?? [])
            {
                var name = wangset.Attribute("name")?.Value;
                var type = wangset.Attribute("type")?.Value;
                if (name is not null && type is not null)
                    types.TryAdd(name, type);
            }
        }
        catch
        {
            // Unreadable original: nothing to carry over.
        }
        return types;
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

        // Everything from <wangsets> on is reused verbatim (TopLevelEquals already proved the
        // model's wangsets match the file's), so the last tile's slice must stop where it starts
        // instead of running on to </tileset> and swallowing it.
        var tailStart = closingTagOffset;
        var wangsetsElement = xdoc.Root!.Element("wangsets");
        if (wangsetsElement is not null)
        {
            tailStart = lineStarts[((IXmlLineInfo)wangsetsElement).LineNumber - 1];
            if (!rawText[tailStart..].TrimStart(' ').StartsWith("<wangsets", StringComparison.Ordinal))
                return false;
        }
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
        if (tileLineStarts.Any(start => start >= tailStart))
            return false;

        var originalSlicesById = new Dictionary<uint, string>();
        for (var i = 0; i < originalTileElements.Count; i++)
        {
            var start = tileLineStarts[i];
            var end = i + 1 < tileLineStarts.Count ? tileLineStarts[i + 1] : tailStart;
            if (!TileLineLooksLikeATile(rawText, start))
                return false;

            var id = uint.Parse(originalTileElements[i].Attribute("id")!.Value);
            originalSlicesById[id] = rawText[start..end];
        }

        var prologueEnd = tileLineStarts.Count > 0 ? tileLineStarts[0] : tailStart;
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
        sb.Append(rawText[tailStart..]);

        var patchedText = sb.ToString();
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        AtomicFile.Write(path, stream =>
        {
            using var writer = new StreamWriter(stream, encoding);
            writer.Write(patchedText);
        });
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
        WangsetsEqual(a.Wangsets, b.Wangsets) &&
        a.Transformations.HasValue == b.Transformations.HasValue;

    private static bool WangsetsEqual(List<Wangset> a, List<Wangset> b) =>
        a.Count == b.Count && a.Zip(b).All(pair => WangsetEquals(pair.First, pair.Second));

    private static bool WangsetEquals(Wangset a, Wangset b) =>
        a.Name == b.Name &&
        a.Class == b.Class &&
        a.Tile == b.Tile &&
        PropertiesEqual(a.Properties, b.Properties) &&
        a.WangColors.Count == b.WangColors.Count &&
        a.WangColors.Zip(b.WangColors).All(p =>
            p.First.Name == p.Second.Name &&
            p.First.Class == p.Second.Class &&
            p.First.Color.ToString() == p.Second.Color.ToString() &&
            p.First.Tile == p.Second.Tile &&
            p.First.Probability == p.Second.Probability &&
            PropertiesEqual(p.First.Properties, p.Second.Properties)) &&
        a.WangTiles.Count == b.WangTiles.Count &&
        a.WangTiles.Zip(b.WangTiles).All(p =>
            p.First.TileID == p.Second.TileID && p.First.WangID.AsSpan().SequenceEqual(p.Second.WangID));

    // DotTiled gives ObjectLayer no equality, so it is compared by its rendered XML
    // (ObjectLayerSignature). An object edit is then an ordinary tile change: the tile is
    // regenerated, while an unedited layer keeps its original bytes with the rest of its slice.
    private static bool TileContentEquals(Tile a, Tile b) =>
        a.Type == b.Type &&
        a.Probability == b.Probability &&
        a.X == b.X &&
        a.Y == b.Y &&
        a.Width == b.Width &&
        a.Height == b.Height &&
        ObjectLayerSignature(a.ObjectLayer) == ObjectLayerSignature(b.ObjectLayer) &&
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
        ClassProperty p => $"class|{p.Name}|{p.PropertyType}|[{string.Join(";", p.Value.Select(PropertySignature))}]",
        EnumReferenceProperty p => $"enum|{p.Name}|{p.StorageType}|{p.PropertyType}|{p.Value}",
        _ => throw new NotSupportedException(
            $"TsxWriter does not yet support '{property.Type}' properties (property '{property.Name}')."),
    };

    private static void WriteTileset(
        XmlWriter writer,
        Tileset tileset,
        IReadOnlyList<(string Name, string Value)> extraRootAttributes,
        IReadOnlyDictionary<string, string> wangsetTypes)
    {
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
        WriteWangsets(writer, tileset.Wangsets, wangsetTypes);

        writer.WriteEndElement();
    }

    private static void WriteWangsets(XmlWriter writer, List<Wangset> wangsets, IReadOnlyDictionary<string, string> wangsetTypes)
    {
        if (wangsets.Count == 0)
            return;

        writer.WriteStartElement("wangsets");
        foreach (var wangset in wangsets)
        {
            writer.WriteStartElement("wangset");
            writer.WriteAttributeString("name", wangset.Name);
            if (!string.IsNullOrEmpty(wangset.Class))
                writer.WriteAttributeString("class", wangset.Class);
            if (wangsetTypes.TryGetValue(wangset.Name, out var type))
                writer.WriteAttributeString("type", type);
            writer.WriteAttributeString("tile", XmlConvert.ToString(wangset.Tile));

            WriteProperties(writer, wangset.Properties);
            foreach (var color in wangset.WangColors)
            {
                writer.WriteStartElement("wangcolor");
                writer.WriteAttributeString("name", color.Name);
                if (!string.IsNullOrEmpty(color.Class))
                    writer.WriteAttributeString("class", color.Class);
                writer.WriteAttributeString("color", color.Color.ToString());
                writer.WriteAttributeString("tile", XmlConvert.ToString(color.Tile));
                writer.WriteAttributeString("probability", XmlConvert.ToString(color.Probability));
                WriteProperties(writer, color.Properties);
                writer.WriteEndElement();
            }
            foreach (var wangTile in wangset.WangTiles)
            {
                writer.WriteStartElement("wangtile");
                writer.WriteAttributeString("tileid", XmlConvert.ToString(wangTile.TileID));
                writer.WriteAttributeString("wangid", string.Join(",", wangTile.WangID));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
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
        if (tile.ObjectLayer.HasValue)
            WriteObjectLayer(writer, tile.ObjectLayer.Value);
        WriteAnimation(writer, tile.Animation);

        writer.WriteEndElement();
    }

    /// <summary>Writes a tile's collision layer as <c>&lt;objectgroup&gt;</c>, in Tiled's own
    /// attribute order and omitting values equal to the format defaults.</summary>
    private static void WriteObjectLayer(XmlWriter writer, ObjectLayer layer)
    {
        writer.WriteStartElement("objectgroup");
        writer.WriteAttributeString("draworder", layer.DrawOrder == DrawOrder.TopDown ? "topdown" : "index");
        writer.WriteAttributeString("id", XmlConvert.ToString(layer.ID));
        if (!string.IsNullOrEmpty(layer.Name))
            writer.WriteAttributeString("name", layer.Name);
        if (!string.IsNullOrEmpty(layer.Class))
            writer.WriteAttributeString("class", layer.Class);
        if (layer.Color.HasValue)
            writer.WriteAttributeString("color", layer.Color.Value.ToString());
        if (layer.OffsetX != 0)
            writer.WriteAttributeString("offsetx", XmlConvert.ToString(layer.OffsetX));
        if (layer.OffsetY != 0)
            writer.WriteAttributeString("offsety", XmlConvert.ToString(layer.OffsetY));
        if (layer.Opacity != 1f)
            writer.WriteAttributeString("opacity", XmlConvert.ToString(layer.Opacity));
        if (!layer.Visible)
            writer.WriteAttributeString("visible", "0");
        if (layer.TintColor.HasValue)
            writer.WriteAttributeString("tintcolor", layer.TintColor.Value.ToString());
        if (layer.ParallaxX != 1f)
            writer.WriteAttributeString("parallaxx", XmlConvert.ToString(layer.ParallaxX));
        if (layer.ParallaxY != 1f)
            writer.WriteAttributeString("parallaxy", XmlConvert.ToString(layer.ParallaxY));

        WriteProperties(writer, layer.Properties);
        foreach (var obj in layer.Objects)
            WriteObject(writer, obj);
        writer.WriteEndElement();
    }

    private static void WriteObject(XmlWriter writer, DotTiled.Object obj)
    {
        writer.WriteStartElement("object");
        if (obj.ID.HasValue)
            writer.WriteAttributeString("id", XmlConvert.ToString(obj.ID.Value));
        if (!string.IsNullOrEmpty(obj.Name))
            writer.WriteAttributeString("name", obj.Name);
        if (!string.IsNullOrEmpty(obj.Type))
            writer.WriteAttributeString("type", obj.Type);
        if (obj.Template.HasValue)
            writer.WriteAttributeString("template", obj.Template.Value);
        if (obj is TileObject tileObject)
            writer.WriteAttributeString("gid", XmlConvert.ToString((uint)tileObject.GID | (uint)tileObject.FlippingFlags));
        writer.WriteAttributeString("x", XmlConvert.ToString(obj.X));
        writer.WriteAttributeString("y", XmlConvert.ToString(obj.Y));
        if (obj.Width != 0)
            writer.WriteAttributeString("width", XmlConvert.ToString(obj.Width));
        if (obj.Height != 0)
            writer.WriteAttributeString("height", XmlConvert.ToString(obj.Height));
        if (obj.Rotation != 0)
            writer.WriteAttributeString("rotation", XmlConvert.ToString(obj.Rotation));
        if (!obj.Visible)
            writer.WriteAttributeString("visible", "0");

        WriteProperties(writer, obj.Properties);
        switch (obj)
        {
            case EllipseObject:
                writer.WriteStartElement("ellipse");
                writer.WriteEndElement();
                break;
            case PointObject:
                writer.WriteStartElement("point");
                writer.WriteEndElement();
                break;
            case PolygonObject polygon:
                WritePoints(writer, "polygon", polygon.Points);
                break;
            case PolylineObject polyline:
                WritePoints(writer, "polyline", polyline.Points);
                break;
            case TextObject text:
                WriteText(writer, text);
                break;
            case RectangleObject:
            case TileObject:
                break;
            default:
                throw new NotSupportedException(
                    $"TsxWriter does not yet support '{obj.GetType().Name}' objects (object {obj.ID}).");
        }
        writer.WriteEndElement();
    }

    private static void WritePoints(XmlWriter writer, string element, List<System.Numerics.Vector2> points)
    {
        writer.WriteStartElement(element);
        writer.WriteAttributeString("points", string.Join(" ",
            points.Select(p => $"{XmlConvert.ToString(p.X)},{XmlConvert.ToString(p.Y)}")));
        writer.WriteEndElement();
    }

    private static void WriteText(XmlWriter writer, TextObject text)
    {
        writer.WriteStartElement("text");
        if (text.FontFamily != "sans-serif")
            writer.WriteAttributeString("fontfamily", text.FontFamily);
        if (text.PixelSize != 16)
            writer.WriteAttributeString("pixelsize", XmlConvert.ToString(text.PixelSize));
        if (text.Wrap)
            writer.WriteAttributeString("wrap", "1");
        if (text.Color.ToString() != "#000000")
            writer.WriteAttributeString("color", text.Color.ToString());
        if (text.Bold)
            writer.WriteAttributeString("bold", "1");
        if (text.Italic)
            writer.WriteAttributeString("italic", "1");
        if (text.Underline)
            writer.WriteAttributeString("underline", "1");
        if (text.Strikeout)
            writer.WriteAttributeString("strikeout", "1");
        if (!text.Kerning)
            writer.WriteAttributeString("kerning", "0");
        if (text.HorizontalAlignment != TextHorizontalAlignment.Left)
            writer.WriteAttributeString("halign", text.HorizontalAlignment.ToString().ToLowerInvariant());
        if (text.VerticalAlignment != TextVerticalAlignment.Top)
            writer.WriteAttributeString("valign", text.VerticalAlignment.ToString().ToLowerInvariant());
        writer.WriteString(text.Text);
        writer.WriteEndElement();
    }

    /// <summary>The layer rendered to XML text: a cheap, exact equality for content DotTiled gives
    /// no equality for. Two layers with equal signatures write identical bytes.</summary>
    private static string ObjectLayerSignature(Optional<ObjectLayer> layer)
    {
        if (!layer.HasValue)
            return "";
        var settings = new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment };
        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, settings))
            WriteObjectLayer(writer, layer.Value);
        return sb.ToString();
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

    private static void WriteProperties(XmlWriter writer, IList<IProperty> properties)
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
            case ClassProperty classProperty:
                writer.WriteAttributeString("type", "class");
                writer.WriteAttributeString("propertytype", classProperty.PropertyType);
                WriteProperties(writer, classProperty.Value);
                break;
            case EnumReferenceProperty enumProperty:
                if (enumProperty.StorageType is not null)
                    writer.WriteAttributeString("type", enumProperty.StorageType);
                writer.WriteAttributeString("propertytype", enumProperty.PropertyType);
                writer.WriteAttributeString("value", enumProperty.Value);
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
