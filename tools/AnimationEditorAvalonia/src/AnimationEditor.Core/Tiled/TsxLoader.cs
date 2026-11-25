using DotTiled;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// The one way this editor reads a <c>.tsx</c>. Reads what the editor works with: the tile grid, the image, and each
/// tile's type, properties and animation. The rest stays in the <see cref="TsxSource"/>, so a construct the editor
/// does not know cannot keep a file from opening.
/// </summary>
public static class TsxLoader
{
    /// <exception cref="NotSupportedException">The file is not UTF-8, is not a tileset, or holds its image.</exception>
    public static Tileset LoadTileset(string path) => ReadTileset(TsxSource.Load(path));

    /// <inheritdoc cref="LoadTileset"/>
    public static Tileset ReadTileset(TsxSource source)
    {
        var root = source.Root;
        if (root.Name != "tileset" || root.Attribute("source") is not null)
            throw new NotSupportedException("the file is not a Tiled tileset.");

        var tileset = new Tileset
        {
            Name = Required(root, "name", value => value),
            TileWidth = Required(root, "tilewidth", XmlConvert.ToInt32),
            TileHeight = Required(root, "tileheight", XmlConvert.ToInt32),
            TileCount = Required(root, "tilecount", XmlConvert.ToInt32),
            Columns = Required(root, "columns", XmlConvert.ToInt32),
            Spacing = XmlConvert.ToInt32(root.Attribute("spacing") ?? "0"),
            Margin = XmlConvert.ToInt32(root.Attribute("margin") ?? "0"),
            Tiles = source.TilesById.Select(tile => ReadTile(tile.Key, tile.Value)).ToList(),
        };
        if (root.Child("image") is { } image)
            tileset.Image = ReadImage(image);
        return tileset;
    }

    private static Tile ReadTile(uint id, TsxElement element)
    {
        // Tiled 1.9 wrote a tile's class as "class"; every other version reads and writes "type".
        var type = element.Attribute("type");
        var tile = new Tile
        {
            ID = id,
            Width = 0,
            Height = 0,
            Type = string.IsNullOrEmpty(type) ? element.Attribute("class") ?? "" : type,
            Properties = PropertyElements(element).Select(ReadProperty).ToList(),
            Animation = FrameElements(element).Select(ReadFrame).ToList(),
        };
        if (element.Child("image") is { } image)
            tile.Image = ReadImage(image);
        return tile;
    }

    internal static IEnumerable<TsxElement> PropertyElements(TsxElement tile) =>
        tile.Child("properties")?.ChildrenNamed("property") ?? [];

    internal static IEnumerable<TsxElement> FrameElements(TsxElement tile) =>
        tile.Child("animation")?.ChildrenNamed("frame") ?? [];

    internal static IProperty ReadProperty(TsxElement element)
    {
        var name = element.Attribute("name") ?? "";
        var value = element.Attribute("value") ?? element.Text;
        return (element.Attribute("type") ?? "string", element.Attribute("propertytype") ?? "") switch
        {
            ("string", "") => new StringProperty { Name = name, Value = value },
            ("int", "") when int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) =>
                new IntProperty { Name = name, Value = number },
            _ => new ForeignProperty { Name = name },
        };
    }

    internal static Frame ReadFrame(TsxElement element) => new()
    {
        TileID = Required(element, "tileid", XmlConvert.ToUInt32),
        Duration = Required(element, "duration", XmlConvert.ToInt32),
    };

    private static Image ReadImage(TsxElement element)
    {
        if (element.Child("data") is not null)
            throw new NotSupportedException("the tileset's image is embedded in the file, which AnimationEditor doesn't support yet.");

        var image = new Image();
        if (element.Attribute("source") is { } source) image.Source = source;
        if (element.Attribute("width") is { } width) image.Width = XmlConvert.ToInt32(width);
        if (element.Attribute("height") is { } height) image.Height = XmlConvert.ToInt32(height);
        return image;
    }

    internal static T Required<T>(TsxElement element, string attribute, Func<string, T> read) =>
        read(element.Attribute(attribute) ?? throw new XmlException($"Missing attribute '{attribute}' on <{element.Name}>."));
}
