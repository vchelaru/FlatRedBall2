using DotTiled;
using System;
using System.IO;
using System.Text;
using System.Xml;

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
/// </remarks>
public static class TsxWriter
{
    public static void Write(Tileset tileset, string path)
    {
        using var stream = File.Create(path);
        Write(tileset, stream);
    }

    public static void Write(Tileset tileset, Stream stream)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = " ",
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
        using var writer = XmlWriter.Create(stream, settings);
        writer.WriteStartDocument();
        WriteTileset(writer, tileset);
        writer.WriteEndDocument();
    }

    private static void WriteTileset(XmlWriter writer, Tileset tileset)
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

    private static void WriteAnimation(XmlWriter writer, System.Collections.Generic.List<Frame> frames)
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

    private static void WriteProperties(XmlWriter writer, System.Collections.Generic.List<IProperty> properties)
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
